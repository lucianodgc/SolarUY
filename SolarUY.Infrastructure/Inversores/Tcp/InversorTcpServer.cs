using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Inversores;

namespace SolarUY.Infrastructure.Inversores.Tcp;

public sealed class InversorTcpServer
{
    private readonly IInversorClient _simulador;
    private readonly InversorTcpOptions _options;
    private readonly ILogger<InversorTcpServer> _logger;

    public InversorTcpServer(
        IInversorClient simulador,
        IOptions<InversorTcpOptions> options,
        ILogger<InversorTcpServer> logger)
    {
        _simulador = simulador;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EjecutarAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Parse(_options.Host), _options.Port);
        listener.Start();
        _logger.LogInformation("Simulador de inversor escuchando en {Host}:{Port}.", _options.Host, _options.Port);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var cliente = await listener.AcceptTcpClientAsync(cancellationToken);
                using var tiempoLimite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tiempoLimite.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
                try
                {
                    await ProcesarClienteAsync(cliente, tiempoLimite.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("El cliente del simulador TCP superó el tiempo de espera.");
                }
            }
        }
        finally
        {
            listener.Stop();
            _logger.LogInformation("Simulador de inversor detenido.");
        }
    }

    private async Task ProcesarClienteAsync(TcpClient cliente, CancellationToken cancellationToken)
    {
        var stream = cliente.GetStream();
        try
        {
            var linea = await InversorTcpProtocol.LeerLineaAsync(stream, cancellationToken);
            SolicitudInversorTcp? solicitud;
            try
            {
                solicitud = JsonSerializer.Deserialize<SolicitudInversorTcp>(linea, InversorTcpProtocol.OpcionesJson);
            }
            catch (JsonException)
            {
                await EnviarErrorAsync(stream, "entrada-invalida", "La solicitud no es JSON válido.", cancellationToken);
                return;
            }

            if (solicitud is null || string.IsNullOrWhiteSpace(solicitud.Operacion))
            {
                await EnviarErrorAsync(stream, "entrada-invalida", "La solicitud debe incluir una operación.", cancellationToken);
                return;
            }

            var resultado = await EjecutarOperacionAsync(solicitud, cancellationToken);
            await InversorTcpProtocol.EscribirLineaAsync(stream,
                new RespuestaInversorTcp(true, JsonSerializer.SerializeToElement<object?>(resultado, InversorTcpProtocol.OpcionesJson)),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KeyNotFoundException exception)
        {
            await IntentarEnviarErrorAsync(stream, "no-encontrado", exception.Message, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            await IntentarEnviarErrorAsync(stream, "entrada-invalida", exception.Message, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Falló el procesamiento de una solicitud del simulador TCP.");
            await IntentarEnviarErrorAsync(stream, "simulador-error", "No se pudo procesar la operación.", cancellationToken);
        }
    }

    private async Task<object?> EjecutarOperacionAsync(SolicitudInversorTcp solicitud, CancellationToken cancellationToken)
    {
        switch (solicitud.Operacion)
        {
            case OperacionesInversorTcp.SincronizarInventario:
                if (solicitud.Nodos is null)
                    throw new ArgumentException("La solicitud requiere el inventario de nodos.");
                await _simulador.SincronizarInventarioAsync(solicitud.Nodos, cancellationToken);
                return true;
            case OperacionesInversorTcp.MedicionesRegistradas:
                if (solicitud.Paneles is null || solicitud.InstanteUtc is null)
                    throw new ArgumentException("La solicitud de mediciones requiere paneles e instante UTC.");
                return await _simulador.ObtenerMedicionesRegistradasAsync(solicitud.Paneles, solicitud.InstanteUtc.Value, cancellationToken);
            case OperacionesInversorTcp.ListarNodos:
                return await _simulador.ListarNodosAsync(cancellationToken);
            case OperacionesInversorTcp.ObtenerEstadoPaneles:
                return await _simulador.ObtenerEstadoPanelesAsync(Exigir(solicitud.NodoId, "nodoId"), cancellationToken);
            case OperacionesInversorTcp.ObtenerPotencia:
                return await _simulador.ObtenerPotenciaGeneradaAsync(Exigir(solicitud.NodoId, "nodoId"), cancellationToken);
            case OperacionesInversorTcp.ActivarPanel:
                await _simulador.ActivarPanelAsync(Exigir(solicitud.NodoId, "nodoId"), Exigir(solicitud.PanelId, "panelId"), cancellationToken);
                return true;
            case OperacionesInversorTcp.DesactivarPanel:
                await _simulador.DesactivarPanelAsync(Exigir(solicitud.NodoId, "nodoId"), Exigir(solicitud.PanelId, "panelId"), cancellationToken);
                return true;
            case OperacionesInversorTcp.ActivarVolcado:
                await _simulador.ActivarVolcadoRedAsync(cancellationToken);
                return true;
            case OperacionesInversorTcp.DesactivarVolcado:
                await _simulador.DesactivarVolcadoRedAsync(cancellationToken);
                return true;
            case OperacionesInversorTcp.ObtenerEstadoVolcado:
                return await _simulador.ObtenerEstadoVolcadoRedAsync(cancellationToken);
            case OperacionesInversorTcp.ObtenerEstadoEncendido:
                return await _simulador.ObtenerEstadoEncendidoAsync(cancellationToken);
            case OperacionesInversorTcp.EstablecerEstadoEncendido:
                if (solicitud.Activo is null)
                    throw new ArgumentException("La solicitud requiere el estado de encendido.");
                await _simulador.EstablecerEstadoEncendidoAsync(solicitud.Activo.Value, cancellationToken);
                return true;
            default:
                throw new ArgumentException("La operación solicitada no está soportada.");
        }
    }

    private static string Exigir(string? valor, string nombre)
        => !string.IsNullOrWhiteSpace(valor) ? valor : throw new ArgumentException($"La solicitud requiere {nombre}.");

    private static Task EnviarErrorAsync(NetworkStream stream, string codigo, string mensaje, CancellationToken cancellationToken)
        => InversorTcpProtocol.EscribirLineaAsync(stream, new RespuestaInversorTcp(false, CodigoError: codigo, Mensaje: mensaje), cancellationToken);

    private static async Task IntentarEnviarErrorAsync(NetworkStream stream, string codigo, string mensaje, CancellationToken cancellationToken)
    {
        try
        {
            await EnviarErrorAsync(stream, codigo, mensaje, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            // El cliente puede haber cerrado la conexión antes de recibir el error.
        }
    }
}
