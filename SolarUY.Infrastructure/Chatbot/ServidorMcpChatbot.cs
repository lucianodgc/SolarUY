using System.Globalization;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Chatbot;

namespace SolarUY.Infrastructure.Chatbot;

public sealed class ServidorMcpChatbot
{
    private readonly IConsultaChatbotService _consultas;
    private readonly IUsuarioActual _usuarioActual;

    public ServidorMcpChatbot(IConsultaChatbotService consultas, IUsuarioActual usuarioActual)
    { _consultas = consultas; _usuarioActual = usuarioActual; }

    public async Task<McpServer> CrearAsync(ITransport transporte, CancellationToken cancellationToken = default)
    {
        var propietario = await AutorizarAsync(null, cancellationToken);
        var catalogo = CatalogoMcpChatbot.Crear();
        return McpServer.Create(transporte, new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "SolarUY.Consultas", Version = "1.0.0" },
            Handlers = new McpServerHandlers
            {
                ListToolsHandler = async (_, ct) =>
                {
                    await AutorizarAsync(propietario, ct);
                    return new ListToolsResult { Tools = catalogo.ToList() };
                },
                CallToolHandler = async (contexto, ct) =>
                {
                    var nombre = contexto.Params?.Name ?? "";
                    try
                    {
                        await AutorizarAsync(propietario, ct);
                        var herramienta = catalogo.SingleOrDefault(t => t.Name == nombre)
                            ?? throw new McpException("Herramienta no permitida.");
                        var argumentos = contexto.Params?.Arguments?.ToDictionary(a => a.Key, a => a.Value);
                        Validar(herramienta, argumentos);
                        var resultado = await EjecutarAsync(nombre, argumentos, ct);
                        await AutorizarAsync(propietario, ct);
                        return Convertir(resultado);
                    }
                    catch (UnauthorizedAccessException)
                    { return Convertir(new(nombre, EstadoConsultaChat.NoAutorizado, "No tienes permiso para esta consulta.")); }
                    catch (ArgumentException)
                    { return Convertir(new(nombre, EstadoConsultaChat.EntradaInvalida, "Los argumentos de la consulta no son válidos.")); }
                }
            }
        });
    }

    private Task<ResultadoConsultaChatDto> EjecutarAsync(string nombre, IReadOnlyDictionary<string, JsonElement>? argumentos,
        CancellationToken cancellationToken) => nombre switch
    {
        "ListarParquesAsync" => _consultas.ListarParquesAsync(cancellationToken),
        "ConsultarEstadoActualAsync" => _consultas.ConsultarEstadoActualAsync(Texto(argumentos, CatalogoMcpChatbot.ArgumentoParque), cancellationToken),
        "ConsultarPanelesConFallaAsync" => _consultas.ConsultarPanelesConFallaAsync(Texto(argumentos, CatalogoMcpChatbot.ArgumentoParque), cancellationToken),
        "ConsultarGeneracionDiariaAsync" => _consultas.ConsultarGeneracionDiariaAsync(Texto(argumentos, CatalogoMcpChatbot.ArgumentoParque), Fecha(argumentos, false), cancellationToken),
        "ConsultarGeneracionHistoricaAsync" => _consultas.ConsultarGeneracionHistoricaAsync(Texto(argumentos, "objetivo"),
            Texto(argumentos, "tipo"), Texto(argumentos, "periodo"), Fecha(argumentos, true)!.Value, cancellationToken),
        "ConsultarMantenimientosPendientesAsync" => _consultas.ConsultarMantenimientosPendientesAsync(Texto(argumentos, CatalogoMcpChatbot.ArgumentoParque), cancellationToken),
        _ => throw new McpException("Herramienta no permitida.")
    };

    private static void Validar(Tool herramienta, IReadOnlyDictionary<string, JsonElement>? argumentos)
    {
        var propiedades = herramienta.InputSchema.GetProperty("properties");
        if (argumentos?.Any(a => !propiedades.TryGetProperty(a.Key, out _) || a.Value.ValueKind != JsonValueKind.String) == true)
            throw new ArgumentException("Argumentos no permitidos.");
        foreach (var requerida in herramienta.InputSchema.GetProperty("required").EnumerateArray())
            _ = Texto(argumentos, requerida.GetString()!);
    }

    private static string Texto(IReadOnlyDictionary<string, JsonElement>? argumentos, string nombre)
        => argumentos?.TryGetValue(nombre, out var valor) == true && valor.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(valor.GetString()) ? valor.GetString()! : throw new ArgumentException("Falta un argumento.");

    private static DateTime? Fecha(IReadOnlyDictionary<string, JsonElement>? argumentos, bool requerida)
    {
        if (!requerida && argumentos?.ContainsKey(CatalogoMcpChatbot.ArgumentoFechaLocal) != true) return null;
        if (DateTime.TryParseExact(Texto(argumentos, CatalogoMcpChatbot.ArgumentoFechaLocal), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var fecha)) return fecha;
        throw new ArgumentException("Fecha local inválida.");
    }

    private async Task<string> AutorizarAsync(string? propietario, CancellationToken cancellationToken)
    {
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        var identidad = usuario.Id + ":" + string.Join(",", usuario.Roles.Order());
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(r => r is "Administrador" or "Tecnico" or "Consulta")
            || (propietario is not null && identidad != propietario))
            throw new UnauthorizedAccessException("Sesión de consulta no autorizada.");
        return identidad;
    }

    private static CallToolResult Convertir(ResultadoConsultaChatDto resultado)
    {
        var json = JsonSerializer.Serialize(resultado, CatalogoMcpChatbot.Json);
        return new CallToolResult
        {
            IsError = resultado.Estado is not (EstadoConsultaChat.Correcto or EstadoConsultaChat.SinDatos),
            Content = [new TextContentBlock { Text = json }],
            StructuredContent = JsonSerializer.SerializeToElement(resultado, CatalogoMcpChatbot.Json)
        };
    }
}
