using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SolarUY.Infrastructure.Chatbot;

// Adaptador de la API nativa de Ollama a IChatClient. Agent Framework conserva
// la coordinación y cada herramienta sigue ejecutándose mediante MCP.
public sealed class OllamaChatClient : IChatClient
{
    private readonly HttpClient _http;
    private readonly ChatbotOptions _opciones;

    public OllamaChatClient(ChatbotOptions opciones)
    {
        if (!opciones.TieneOpcionesValidas || opciones.Proveedor != "Ollama")
            throw new ArgumentException("Configuración local de Ollama inválida.", nameof(opciones));
        _opciones = opciones;
        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = new Uri(opciones.EndpointOllama), Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var mensajes = messages.ToArray();
        var nombres = mensajes.SelectMany(m => m.Contents).OfType<FunctionCallContent>()
            .GroupBy(c => c.CallId).ToDictionary(g => g.Key, g => g.Last().Name);
        var entrada = mensajes.SelectMany(m => ConvertirMensajes(m, nombres)).ToList();
        if (!string.IsNullOrWhiteSpace(options?.Instructions))
            entrada.Insert(0, new() { ["role"] = "system", ["content"] = options.Instructions });
        var herramientas = options?.Tools?.OfType<AIFunction>().Select(f => new
        { type = "function", function = new { name = f.Name, description = f.Description, parameters = f.JsonSchema } }).ToArray();
        using var respuesta = await _http.PostAsJsonAsync("api/chat", new
        {
            model = options?.ModelId ?? _opciones.Modelo, messages = entrada, tools = herramientas,
            stream = false, think = false, keep_alive = "60s",
            format = options?.ResponseFormat is ChatResponseFormatJson json
                ? (object?)json.Schema ?? "json" : null,
            // Un lote menor reduce los búferes de cálculo sin recortar contexto,
            // cambiar los pesos del modelo ni quitar herramientas.
            options = new { num_ctx = _opciones.ContextoOllama, num_batch = 128, use_mmap = true,
                num_predict = options?.MaxOutputTokens ?? 4096, temperature = 0 }
        }, CatalogoMcpChatbot.Json, cancellationToken);
        if (!respuesta.IsSuccessStatusCode)
            throw new HttpRequestException("El proveedor local no pudo completar la solicitud.", null, respuesta.StatusCode);
        using var documento = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        if (!documento.RootElement.TryGetProperty("message", out var mensaje))
            throw new InvalidOperationException("Respuesta del proveedor local inválida.");
        var contenido = new List<AIContent>();
        if (mensaje.TryGetProperty("content", out var texto) && !string.IsNullOrWhiteSpace(texto.GetString()))
            contenido.Add(new TextContent(texto.GetString()!));
        if (mensaje.TryGetProperty("tool_calls", out var llamadas))
        foreach (var llamada in llamadas.EnumerateArray())
        {
            var funcion = llamada.GetProperty("function");
            var argumentos = funcion.GetProperty("arguments");
            if (argumentos.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Argumentos locales inválidos.");
            contenido.Add(new FunctionCallContent(Guid.NewGuid().ToString("N"), funcion.GetProperty("name").GetString()!,
                argumentos.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone())));
        }
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contenido))
        { FinishReason = contenido.Any(c => c is FunctionCallContent) ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop };
    }

    private static IEnumerable<Dictionary<string, object?>> ConvertirMensajes(ChatMessage mensaje, IReadOnlyDictionary<string, string> nombres)
    {
        var respuestas = mensaje.Contents.OfType<FunctionResultContent>().ToArray();
        if (respuestas.Length > 0)
        {
            foreach (var respuesta in respuestas)
                yield return new()
                {
                    ["role"] = "tool",
                    ["tool_name"] = nombres.GetValueOrDefault(respuesta.CallId) ?? throw new InvalidOperationException("Resultado de herramienta sin invocación."),
                    ["content"] = JsonSerializer.Serialize(respuesta.Result, CatalogoMcpChatbot.Json)
                };
            yield break;
        }
        var resultado = new Dictionary<string, object?> { ["role"] = mensaje.Role.Value, ["content"] = mensaje.Text };
        var llamadas = mensaje.Contents.OfType<FunctionCallContent>().ToArray();
        if (llamadas.Length > 0)
            resultado["tool_calls"] = llamadas.Select(c => new { function = new { name = c.Name, arguments = c.Arguments } }).ToArray();
        yield return resultado;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("ollama", new Uri(_opciones.EndpointOllama), _opciones.Modelo)
            : serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() => _http.Dispose();
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    { foreach (var update in (await GetResponseAsync(messages, options, cancellationToken)).ToChatResponseUpdates()) yield return update; }
}
