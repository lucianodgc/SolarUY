using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SolarUY.Application.Chatbot;
using SolarUY.Infrastructure.Chatbot;

namespace SolarUY.Integration.Tests;

public sealed class ChatbotCatalogoIntegrationTests
{
    [Theory]
    [InlineData("incompleto")]
    [InlineData("herramienta-ajena")]
    [InlineData("esquema-incompatible")]
    [InlineData("error-protocolo")]
    public async Task Chatbot_NoOfreceAlModeloUnCatalogoMcpAlterado(string alteracion)
    {
        await using var escenario = new ChatbotEscenario(configurar: s =>
            s.AddScoped<IConexionMcpChatbot>(_ => new ConexionCatalogoAlterado(alteracion)));
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>()
            .EnviarPreguntaAsync(new("¿Qué parques hay?"));
        Assert.Equal(EstadoConsultaChat.NoDisponible, respuesta.Estado);
        Assert.Equal(0, escenario.Modelo.CantidadSolicitudes);
        Assert.Empty(respuesta.Consultas);
    }

    // El límite externo es un servidor MCP real que anuncia un catálogo
    // incompatible. Se prueba el cliente de producción sin cambiar sus reglas.
    private sealed class ConexionCatalogoAlterado(string alteracion) : IConexionMcpChatbot
    {
        public async Task<SesionMcpChatbot> AbrirAsync(CancellationToken cancellationToken = default)
        {
            var catalogo = CatalogoMcpChatbot.Crear().ToList();
            if (alteracion == "incompleto") catalogo.RemoveAt(0);
            else if (alteracion == "herramienta-ajena")
                catalogo.Add(new Tool { Name = "EjecutarSql", InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) });
            else catalogo[0].InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { usuarioId = new { type = "string" } } });
            var entrada = new Pipe(); var salida = new Pipe();
            var vida = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var servidor = McpServer.Create(new StreamServerTransport(entrada.Reader.AsStream(), salida.Writer.AsStream()),
                new McpServerOptions
                {
                    ServerInfo = new Implementation { Name = "Servidor de prueba de catálogo", Version = "1.0" },
                    Handlers = new McpServerHandlers { ListToolsHandler = (_, _) => alteracion == "error-protocolo"
                        ? throw new ModelContextProtocol.McpException("Error de protocolo de prueba sk-ficticia")
                        : ValueTask.FromResult(new ListToolsResult { Tools = catalogo }) }
                });
            var ejecucion = servidor.RunAsync(vida.Token);
            var cliente = await McpClient.CreateAsync(new StreamClientTransport(entrada.Writer.AsStream(), salida.Reader.AsStream()), cancellationToken: cancellationToken);
            return new(cliente, servidor, ejecucion, vida);
        }
    }
}
