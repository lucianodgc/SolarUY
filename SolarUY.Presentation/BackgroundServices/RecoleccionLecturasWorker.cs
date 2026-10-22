using Microsoft.Extensions.Options;
using SolarUY.Application.RecoleccionLecturas;

namespace SolarUY.Presentation.BackgroundServices;

public sealed class RecoleccionLecturasOptions
{
    public bool Habilitada { get; set; } = true;
    public TimeSpan Intervalo { get; set; } = TimeSpan.FromMinutes(1);
}

public sealed class RecoleccionLecturasWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecoleccionLecturasOptions _options;
    private readonly TimeProvider _reloj;
    private readonly ILogger<RecoleccionLecturasWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public RecoleccionLecturasWorker(IServiceScopeFactory scopeFactory, IOptions<RecoleccionLecturasOptions> options,
        TimeProvider reloj, ILogger<RecoleccionLecturasWorker> logger, IHostApplicationLifetime lifetime)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _reloj = reloj;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Habilitada) return;
        try
        {
            await EsperarArranqueAsync(stoppingToken);
            stoppingToken.ThrowIfCancellationRequested();
            using var timer = new PeriodicTimer(_options.Intervalo, _reloj);
            _logger.LogInformation("Recolección de paneles registrados habilitada cada {Intervalo}. Fuente actual: simulador.", _options.Intervalo);
            do { await RecolectarAsync(stoppingToken); }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task EsperarArranqueAsync(CancellationToken cancellationToken)
    {
        var iniciado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registro = _lifetime.ApplicationStarted.Register(() => iniciado.TrySetResult());
        await iniciado.Task.WaitAsync(cancellationToken);
    }

    private async Task RecolectarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var servicio = scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>();
            var resultado = await servicio.RecolectarAsync(cancellationToken);
            if (resultado.LecturasGuardadas > 0)
                _logger.LogInformation("Recolección completada: {Cantidad} lecturas guardadas.", resultado.LecturasGuardadas);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se completó la recolección. Se reintentará en el próximo ciclo.");
        }
    }
}
