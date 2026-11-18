using System.IO.Pipelines;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace SolarUY.Infrastructure.Chatbot;

public interface IConexionMcpChatbot
{
    Task<SesionMcpChatbot> AbrirAsync(CancellationToken cancellationToken = default);
}

public sealed class ConexionMcpChatbot : IConexionMcpChatbot
{
    private readonly ServidorMcpChatbot _servidor;
    public ConexionMcpChatbot(ServidorMcpChatbot servidor) => _servidor = servidor;

    public async Task<SesionMcpChatbot> AbrirAsync(CancellationToken cancellationToken = default)
    {
        var entrada = new Pipe();
        var salida = new Pipe();
        var vida = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var transporte = new StreamServerTransport(entrada.Reader.AsStream(), salida.Writer.AsStream(), "SolarUY");
        McpServer? servidor = null;
        try
        {
            servidor = await _servidor.CrearAsync(transporte, cancellationToken);
            var ejecucion = servidor.RunAsync(vida.Token);
            var cliente = await McpClient.CreateAsync(new StreamClientTransport(entrada.Writer.AsStream(), salida.Reader.AsStream()),
                cancellationToken: cancellationToken);
            return new SesionMcpChatbot(cliente, servidor, ejecucion, vida);
        }
        catch
        {
            await vida.CancelAsync();
            if (servidor is not null) await servidor.DisposeAsync();
            await transporte.DisposeAsync();
            await entrada.Writer.CompleteAsync(); await salida.Reader.CompleteAsync();
            vida.Dispose();
            throw;
        }
    }
}

public sealed class SesionMcpChatbot : IAsyncDisposable
{
    private readonly McpServer _servidor;
    private readonly Task _ejecucion;
    private readonly CancellationTokenSource _vida;
    public McpClient Cliente { get; }

    public SesionMcpChatbot(McpClient cliente, McpServer servidor, Task ejecucion, CancellationTokenSource vida)
    { Cliente = cliente; _servidor = servidor; _ejecucion = ejecucion; _vida = vida; }

    public async ValueTask DisposeAsync()
    {
        await _vida.CancelAsync();
        try { await Cliente.DisposeAsync(); }
        finally
        {
            await _servidor.DisposeAsync();
            try { await _ejecucion; }
            catch (OperationCanceledException) { /* La cancelación termina el trabajo pendiente. */ }
            finally { _vida.Dispose(); }
        }
    }
}
