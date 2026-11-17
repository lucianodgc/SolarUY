using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolarUY.Application.Chatbot;
using SolarUY.Infrastructure.Chatbot;

namespace SolarUY.Integration.Tests;

public sealed class ChatbotProveedorIntegrationTests
{
    [Fact]
    public async Task Ollama_AdaptadorNativoInvocaMcpSinClaveDeOpenAiNiCredencialesEnElPayload()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        await using var proveedor = builder.Build();
        var cuerpos = new List<string>();
        proveedor.MapPost("/api/chat", async (HttpContext context) =>
        {
            Assert.False(context.Request.Headers.ContainsKey("Authorization"));
            using var json = await JsonDocument.ParseAsync(context.Request.Body);
            cuerpos.Add(json.RootElement.GetRawText());
            Assert.Equal("qwen3:8b", json.RootElement.GetProperty("model").GetString());
            Assert.False(json.RootElement.GetProperty("think").GetBoolean());
            Assert.False(json.RootElement.GetProperty("stream").GetBoolean());
            Assert.Equal("60s", json.RootElement.GetProperty("keep_alive").GetString());
            Assert.Equal(16384, json.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32());
            Assert.Equal(128, json.RootElement.GetProperty("options").GetProperty("num_batch").GetInt32());
            Assert.True(json.RootElement.GetProperty("options").GetProperty("use_mmap").GetBoolean());
            if (json.RootElement.TryGetProperty("format", out var formato) && formato.ValueKind == JsonValueKind.Object)
            {
                Assert.False(formato.GetProperty("additionalProperties").GetBoolean());
                return Results.Json(new { message = new { role = "assistant", content = "{\"accion\":\"herramientas\",\"parques\":[]}" } });
            }
            Assert.Equal(6, json.RootElement.GetProperty("tools").GetArrayLength());
            var recibidos = json.RootElement.GetProperty("messages").EnumerateArray().ToArray();
            if (!recibidos.Any(m => m.GetProperty("role").GetString() == "tool"))
                return Results.Json(new { message = new { role = "assistant", content = "", tool_calls = new[] {
                    new { function = new { name = "ListarParquesAsync", arguments = new { } } } } } });
            var contenido = recibidos.Last(m => m.GetProperty("role").GetString() == "tool").GetProperty("content").GetString();
            using var consulta = JsonDocument.Parse(contenido!);
            Assert.Equal("Correcto", consulta.RootElement.GetProperty("estado").GetString());
            var nombres = consulta.RootElement.GetProperty("datos").EnumerateArray().Select(p => p.GetProperty("nombre").GetString());
            return Results.Json(new { message = new { role = "assistant", content = "Parques disponibles: " + string.Join(", ", nombres) } });
        });
        await proveedor.StartAsync();
        await using var escenario = new ChatbotEscenario(configurar: servicios =>
        {
            servicios.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            servicios.AddOptions<ChatbotOptions>().Configure(o => o.EndpointOllama = proveedor.Urls.Single() + "/");
            servicios.AddScoped<OpenAiModeloChatbotFactory>();
            servicios.AddScoped<IModeloChatbotFactory, ProveedorModeloChatbotFactory>();
        });
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("¿Qué parques hay?"));
        Assert.Equal(EstadoConsultaChat.Correcto, respuesta.Estado);
        Assert.Contains(escenario.Parque.Nombre, respuesta.Texto);
        Assert.Equal(3, cuerpos.Count);
        Assert.DoesNotContain(cuerpos, c => c.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
        Assert.Single(respuesta.Consultas);
        Assert.Equal(0, escenario.Inversor.Escrituras);
    }

    [Fact]
    public async Task OpenAi_OpcionalSinClaveNoImpideResolverElPanelNiUsaOllamaComoSustituto()
    {
        await using var escenario = new ChatbotEscenario(configurar: servicios =>
        {
            servicios.AddOptions<ChatbotOptions>().Configure(o => { o.Proveedor = "OpenAI"; o.Modelo = "gpt-6-luna"; });
            servicios.AddScoped<OpenAiModeloChatbotFactory>();
            servicios.AddScoped<IModeloChatbotFactory, ProveedorModeloChatbotFactory>();
        });
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("¿Qué parques hay?"));
        Assert.Equal(EstadoConsultaChat.NoDisponible, resultado.Estado);
        Assert.Empty(resultado.Consultas);
    }
}
