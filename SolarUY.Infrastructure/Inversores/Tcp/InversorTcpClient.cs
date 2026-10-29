using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Application.Inversores;

namespace SolarUY.Infrastructure.Inversores.Tcp;

public sealed class InversorTcpClient : IInversorClient
{
    private readonly InversorTcpOptions _options;
    private readonly ILogger<InversorTcpClient> _logger;

    public InversorTcpClient(IOptions<InversorTcpOptions> options, ILogger<InversorTcpClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SincronizarInventarioAsync(IReadOnlyList<InventarioNodoInversorDto> nodos, CancellationToken cancellationToken = default)
        => EjecutarOrdenAsync(new SolicitudInversorTcp(OperacionesInversorTcp.SincronizarInventario, Nodos: nodos), cancellationToken);

    public Task<IReadOnlyList<MedicionPanelDto>> ObtenerMedicionesRegistradasAsync(
        IReadOnlyList<PanelMedicionDto> paneles, DateTime instanteUtc, CancellationToken cancellationToken = default)
        => EjecutarAsync<IReadOnlyList<MedicionPanelDto>>(
            new SolicitudInversorTcp(OperacionesInversorTcp.MedicionesRegistradas, Paneles: paneles, InstanteUtc: instanteUtc), cancellationToken);

    public Task<IReadOnlyList<string>> ListarNodosAsync(CancellationToken cancellationToken = default)
        => EjecutarAsync<IReadOnlyList<string>>(new SolicitudInversorTcp(OperacionesInversorTcp.ListarNodos), cancellationToken);

    public Task<IReadOnlyList<PanelInversorEstadoDto>> ObtenerEstadoPanelesAsync(
        string nodoId, CancellationToken cancellationToken = default)
        => EjecutarAsync<IReadOnlyList<PanelInversorEstadoDto>>(
            new SolicitudInversorTcp(OperacionesInversorTcp.ObtenerEstadoPaneles, NodoId: nodoId), cancellationToken);

    public Task<decimal> ObtenerPotenciaGeneradaAsync(string nodoId, CancellationToken cancellationToken = default)
        => EjecutarAsync<decimal>(new SolicitudInversorTcp(OperacionesInversorTcp.ObtenerPotencia, NodoId: nodoId), cancellationToken);

    public Task ActivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default)
        => EjecutarOrdenAsync(new SolicitudInversorTcp(OperacionesInversorTcp.ActivarPanel, NodoId: nodoId, PanelId: panelId), cancellationToken);

    public Task DesactivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default)
        => EjecutarOrdenAsync(new SolicitudInversorTcp(OperacionesInversorTcp.DesactivarPanel, NodoId: nodoId, PanelId: panelId), cancellationToken);

    public Task ActivarVolcadoRedAsync(CancellationToken cancellationToken = default)
        => EjecutarOrdenAsync(new SolicitudInversorTcp(OperacionesInversorTcp.ActivarVolcado), cancellationToken);

    public Task DesactivarVolcadoRedAsync(CancellationToken cancellationToken = default)
        => EjecutarOrdenAsync(new SolicitudInversorTcp(OperacionesInversorTcp.DesactivarVolcado), cancellationToken);

    public Task<bool> ObtenerEstadoVolcadoRedAsync(CancellationToken cancellationToken = default)
        => EjecutarAsync<bool>(new SolicitudInversorTcp(OperacionesInversorTcp.ObtenerEstadoVolcado), cancellationToken);

    public Task<bool> ObtenerEstadoEncendidoAsync(CancellationToken cancellationToken = default)
        => EjecutarAsync<bool>(new SolicitudInversorTcp(OperacionesInversorTcp.ObtenerEstadoEncendido), cancellationToken);

    public Task EstablecerEstadoEncendidoAsync(bool encendido, CancellationToken cancellationToken = default)
        => EjecutarOrdenAsync(new SolicitudInversorTcp(OperacionesInversorTcp.EstablecerEstadoEncendido, Activo: encendido), cancellationToken);

    private async Task EjecutarOrdenAsync(SolicitudInversorTcp solicitud, CancellationToken cancellationToken)
        => _ = await EjecutarAsync<bool>(solicitud, cancellationToken);

    private async Task<T> EjecutarAsync<T>(SolicitudInversorTcp solicitud, CancellationToken cancellationToken)
    {
        using var tiempoLimite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        tiempoLimite.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            using var cliente = new TcpClient();
            await cliente.ConnectAsync(_options.Host, _options.Port, tiempoLimite.Token);
            await using var stream = cliente.GetStream();
            await InversorTcpProtocol.EscribirLineaAsync(stream, solicitud, tiempoLimite.Token);
            var linea = await InversorTcpProtocol.LeerLineaAsync(stream, tiempoLimite.Token);
            var respuesta = JsonSerializer.Deserialize<RespuestaInversorTcp>(linea, InversorTcpProtocol.OpcionesJson)
                ?? throw new JsonException("La respuesta JSON está vacía.");

            if (!respuesta.Exito)
                LanzarErrorRemoto(respuesta);

            if (respuesta.Resultado is null)
                throw new JsonException("La respuesta del simulador no contiene un resultado.");

            return respuesta.Resultado.Value.Deserialize<T>(InversorTcpProtocol.OpcionesJson)
                ?? throw new JsonException("El resultado del simulador no coincide con el tipo esperado.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ErrorComunicacionInversorException("El simulador del inversor no respondió dentro del tiempo permitido.", exception);
        }
        catch (Exception exception) when (exception is SocketException or IOException or JsonException)
        {
            _logger.LogWarning(exception, "Falló una operación TCP contra el simulador del inversor ({Operacion}).", solicitud.Operacion);
            throw new ErrorComunicacionInversorException("No se pudo comunicar con el simulador del inversor.", exception);
        }
    }

    private static void LanzarErrorRemoto(RespuestaInversorTcp respuesta)
    {
        switch (respuesta.CodigoError)
        {
            case "no-encontrado":
                throw new KeyNotFoundException(respuesta.Mensaje ?? "No existe el identificador en el simulador.");
            case "entrada-invalida":
                throw new ArgumentException(respuesta.Mensaje ?? "La solicitud enviada al simulador no es válida.");
            default:
                throw new ErrorComunicacionInversorException("El simulador rechazó la operación solicitada.");
        }
    }
}
