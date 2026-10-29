using Microsoft.Extensions.Options;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Application.Monitoreo;

namespace SolarUY.Presentation.BackgroundServices;

public sealed class RecoleccionLecturasOptions
{
    public bool Habilitada { get; set; } = true;
    public int? IntervaloSegundos { get; set; }
    // PeriodicTimer admite como máximo uint.MaxValue - 1 milisegundos.
    public bool TieneIntervaloValido => IntervaloSegundos is > 0 and <= 4_294_967;

    public TimeSpan Intervalo => TieneIntervaloValido
        ? TimeSpan.FromSeconds(IntervaloSegundos!.Value)
        : throw new InvalidOperationException("RecoleccionLecturas:IntervaloSegundos debe estar entre 1 y 4294967 segundos (límite de PeriodicTimer).");
}

public sealed class RecoleccionLecturasWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecoleccionLecturasOptions _options;
    private readonly TimeProvider _reloj;
    private readonly ILogger<RecoleccionLecturasWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly INotificadorLecturasConfirmadas? _notificador;

    public RecoleccionLecturasWorker(IServiceScopeFactory scopeFactory, IOptions<RecoleccionLecturasOptions> options,
        TimeProvider reloj, ILogger<RecoleccionLecturasWorker> logger, IHostApplicationLifetime lifetime,
        INotificadorLecturasConfirmadas? notificador = null)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _reloj = reloj;
        _logger = logger;
        _lifetime = lifetime;
        _notificador = notificador;
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
            {
                _logger.LogInformation("Recolección completada: {Cantidad} lecturas guardadas.", resultado.LecturasGuardadas);
                await NotificarConfirmadasAsync(resultado, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "No se completó la recolección. Se reintentará en el próximo ciclo.");
        }
    }

    private async Task NotificarConfirmadasAsync(ResultadoRecoleccionDto resultado, CancellationToken cancellationToken)
    {
        if (_notificador is null || resultado.LecturasConfirmadas is not { Count: > 0 } lecturas) return;
        try { await _notificador.NotificarAsync(lecturas, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Las lecturas se guardaron, pero no se pudo publicar el aviso de monitoreo.");
        }
    }
}
