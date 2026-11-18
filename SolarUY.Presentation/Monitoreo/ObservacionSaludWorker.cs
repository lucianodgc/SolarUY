using Microsoft.Extensions.Options;
using SolarUY.Application.Salud;

namespace SolarUY.Presentation.Monitoreo;

public sealed class SaludDistribucionOptions
{
    public bool Habilitada { get; set; } = true;
    public string InstanciaId { get; set; } = "solaruy-local";
    public int IntervaloSegundos { get; set; } = 1;
}

public sealed class ObservacionSaludWorker(IServiceScopeFactory scopes, INotificadorSaludConfirmada notificador,
    IOptions<SaludDistribucionOptions> options, ILogger<ObservacionSaludWorker> logger) : BackgroundService
{
    private Dictionary<Guid, FirmaSaludDto> _confirmados = [];
    private bool _iniciado;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Habilitada) return;
        logger.LogInformation("Observación de salud compartida activa en {Instancia} cada {Intervalo}s.",
            options.Value.InstanciaId, options.Value.IntervaloSegundos);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervaloSegundos));
        try
        {
            do
            {
                try { await ObservarAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                { logger.LogWarning(ex, "No se confirmó la entrega de salud en {Instancia}; se reintentará el snapshot.", options.Value.InstanciaId); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { /* La cancelación termina el trabajo pendiente. */ }
    }

    private async Task ObservarAsync(CancellationToken cancellationToken)
    {
        Dictionary<Guid, FirmaSaludDto> actuales;
        IReadOnlyList<Guid> parques;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IObservacionSaludRepository>();
            var snapshot = await repository.LeerSnapshotAsync(cancellationToken);
            actuales = snapshot.Paneles.ToDictionary(p => p.PanelId);
            parques = snapshot.Parques;
        }
        var avisos = new List<AvisoSaludConfirmada>();
        if (!_iniciado)
            avisos.AddRange(parques.Select(parque => new AvisoSaludConfirmada(parque, null, 0)));
        foreach (var actual in actuales.Values)
        {
            if (_confirmados.TryGetValue(actual.PanelId, out var anterior))
            {
                if (actual == anterior) continue;
                if (actual.ParqueId != anterior.ParqueId)
                    avisos.Add(new(anterior.ParqueId, actual.PanelId, actual.Version));
            }
            avisos.Add(new(actual.ParqueId, actual.PanelId, actual.Version));
        }
        foreach (var anterior in _confirmados.Values.Where(a => !actuales.ContainsKey(a.PanelId)))
            avisos.Add(new(anterior.ParqueId, anterior.PanelId, anterior.Version));
        if (avisos.Count > 0) await notificador.NotificarAsync(avisos, cancellationToken);
        // Solo después de la entrega completa. Reiniciar el proceso permite repetir sin perder el estado vigente.
        _confirmados = actuales;
        _iniciado = true;
    }
}
