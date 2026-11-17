using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SolarUY.Application.Salud;

namespace SolarUY.Presentation.Monitoreo;

public sealed class SaludPanelOptions
{
    public bool Habilitada { get; set; } = true;
    public int IntervalosDesconexion { get; set; } = 3;
    public int EvaluacionSegundos { get; set; } = 5;
    public decimal UmbralFalla { get; set; } = 0.5m;
    public decimal UmbralDegradado { get; set; } = 0.8m;
    public decimal FactorEsperado { get; set; } = 1m;
    public bool EvaluarPotenciaBaja { get; set; }
}

public sealed class NotificadorSaludConfirmada(IHubContext<MonitoreoHub> hub) : INotificadorSaludConfirmada
{
    // Entrega a los grupos locales; ObservacionSaludWorker recibe los cambios compartidos en cada instancia.
    public async Task NotificarAsync(IReadOnlyList<AvisoSaludConfirmada> avisos, CancellationToken cancellationToken = default)
    {
        foreach (var parque in avisos.Select(a => a.ParqueId).Distinct())
            await hub.Clients.Group(MonitoreoHub.Grupo(parque)).SendAsync("SaludActualizada", parque, cancellationToken);
    }
}

public sealed class SaludPanelWorker(IServiceScopeFactory scopes, INotificadorSaludConfirmada notificador,
    IOptions<SaludPanelOptions> options, ILogger<SaludPanelWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Habilitada) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.EvaluacionSegundos));
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var avisos = await scope.ServiceProvider.GetRequiredService<ISaludPanelService>().EvaluarAsync(stoppingToken);
                    try { await notificador.NotificarAsync(avisos, stoppingToken); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    { logger.LogWarning(ex, "La salud se confirmó, pero falló el aviso local; se reconciliará en el próximo ciclo."); }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { logger.LogError(ex, "No se completó la evaluación de salud; se reintentará sin adquirir nuevas lecturas."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
