using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SolarUY.Application.Chatbot;

namespace SolarUY.Infrastructure.Chatbot;

internal sealed class ConsultaMcpFunction : AIFunction
{
    private readonly Tool _herramienta;
    private readonly McpClient _cliente;
    private readonly List<ResultadoConsultaChatDto> _resultados;
    public ConsultaMcpFunction(Tool herramienta, McpClient cliente, List<ResultadoConsultaChatDto> resultados)
    { _herramienta = herramienta; _cliente = cliente; _resultados = resultados; }
    public override string Name => _herramienta.Name;
    public override string Description => _herramienta.Description ?? "Consulta de SolarUY";
    public override JsonElement JsonSchema => _herramienta.InputSchema;

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var respuesta = await _cliente.CallToolAsync(Name,
            arguments.ToDictionary(a => a.Key, a => a.Value), cancellationToken: cancellationToken);
        var resultado = respuesta.StructuredContent?.Deserialize<ResultadoConsultaChatDto>(CatalogoMcpChatbot.Json)
            ?? throw new InvalidOperationException("Respuesta MCP inválida.");
        _resultados.Add(resultado);
        return resultado;
    }
}
