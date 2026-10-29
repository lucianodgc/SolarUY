using Microsoft.Extensions.Options;
using SolarUY.Application.Monitoreo;

namespace SolarUY.Presentation.Monitoreo;

public sealed class DistribucionMonitoreoOptions
{
    public string InstanciaId { get; set; } = "solaruy-local";
    public int IntervaloSegundos { get; set; } = 1;
    public int TamanoLote { get; set; } = 100;
}

public sealed class DistribucionMonitoreoWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly INotificadorLecturasConfirmadas _notificador;
    private readonly DistribucionMonitoreoOptions _opciones;
    private readonly ILogger<DistribucionMonitoreoWorker> _logger;

    public DistribucionMonitoreoWorker(IServiceScopeFactory scopes, INotificadorLecturasConfirmadas notificador,
        IOptions<DistribucionMonitoreoOptions> opciones, ILogger<DistribucionMonitoreoWorker> logger)
    { _scopes = scopes; _notificador = notificador; _opciones = opciones.Value; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_opciones.IntervaloSegundos));
        try
        {
            do
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IDistribucionMonitoreoRepository>();
                    var avisos = await repository.LeerPendientesAsync(_opciones.InstanciaId, _opciones.TamanoLote, stoppingToken);
                    foreach (var aviso in avisos)
                    {
                        await _notificador.NotificarAsync(aviso.Lecturas, stoppingToken);
                        await repository.ConfirmarAsync(_opciones.InstanciaId, aviso.Secuencia, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex) { _logger.LogError(ex, "No se distribuyeron todos los avisos; el cursor permite reintentarlos."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
