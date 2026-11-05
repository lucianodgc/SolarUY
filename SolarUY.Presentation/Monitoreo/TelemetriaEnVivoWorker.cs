using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SolarUY.Application.Inversores;

namespace SolarUY.Presentation.Monitoreo;

public sealed class TelemetriaEnVivoOptions
{
    public int IntervaloSegundos { get; set; } = 2;
    public int ActualizarInventarioSegundos { get; set; } = 30;
}

/// <summary>Consulta el simulador una sola vez por ciclo y distribuye la misma muestra a ambas pantallas.</summary>
public sealed class TelemetriaEnVivoWorker : BackgroundService
{
    private readonly ITelemetriaEnVivoService _telemetria;
    private readonly NotificadorTelemetriaEnVivo _notificador;
    private readonly TelemetriaEnVivoOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<TelemetriaEnVivoWorker> _logger;

    public TelemetriaEnVivoWorker(ITelemetriaEnVivoService telemetria, NotificadorTelemetriaEnVivo notificador,
        IOptions<TelemetriaEnVivoOptions> options, TimeProvider clock, ILogger<TelemetriaEnVivoWorker> logger)
    {
        _telemetria = telemetria;
        _notificador = notificador;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetría en vivo iniciada. Intervalo configurado: {Intervalo} segundos.", _options.IntervaloSegundos);
        var primeraMuestra = true;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervaloSegundos), _clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var muestras = await _telemetria.ConsultarAsync(stoppingToken);
                    foreach (var grupo in muestras)
                        await _notificador.NotificarAsync(grupo.Key, grupo.Value, stoppingToken);
                    if (primeraMuestra)
                    {
                        _logger.LogInformation("Telemetría: primer ciclo completado. Parques: {Parques}; nodos: {Nodos}.",
                            muestras.Count, muestras.Sum(grupo => grupo.Value.Count));
                        primeraMuestra = false;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "No se pudo consultar o distribuir una muestra del simulador; se reintentará en el próximo ciclo.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
