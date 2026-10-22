using Microsoft.Extensions.Configuration;
using SolarUY.Application.Abstractions;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Domain.Enums;

namespace SolarUY.Infrastructure.Inversores;

public sealed class InversorSimulador : IInversorClient
{
    private const decimal PotenciaMaximaPanelW = 400m;
    private const double HoraInicioSolar = 6d;
    private const double HoraFinSolar = 18d;

    private readonly object _sync = new();
    private readonly Dictionary<string, HashSet<string>> _panelesPorNodo;
    private readonly HashSet<string> _panelesConFalla;
    private readonly HashSet<(string NodoId, string PanelId)> _panelesDesactivados = [];
    private readonly TimeProvider _timeProvider;
    private bool _volcadoRedActivo;

    public InversorSimulador(IConfiguration configuration, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _panelesPorNodo = CargarPanelesPorNodo(configuration);
        _panelesConFalla = CargarPanelesConFalla(configuration);
    }

    public Task<IReadOnlyList<string>> ListarNodosAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> nodos = _panelesPorNodo.Keys.Order(StringComparer.Ordinal).ToArray();
        return Task.FromResult(nodos);
    }

    public Task<IReadOnlyList<MedicionPanelDto>> ObtenerMedicionesRegistradasAsync(
        IReadOnlyList<PanelMedicionDto> paneles, DateTime instanteUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (instanteUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("La medición requiere un instante UTC.", nameof(instanteUtc));
        var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");
        var horaLocal = TimeZoneInfo.ConvertTime(new DateTimeOffset(instanteUtc), zona);
        IReadOnlyList<MedicionPanelDto> resultado = paneles.Select(panel => MedirPanelRegistrado(panel, horaLocal)).ToArray();
        return Task.FromResult(resultado);
    }

    private MedicionPanelDto MedirPanelRegistrado(PanelMedicionDto panel, DateTimeOffset horaLocal)
    {
        if (panel.PotenciaNominalW < 0) throw new InvalidOperationException($"El panel {panel.Id} tiene una potencia nominal inválida.");
        var fallado = panel.Estado == EstadoPanel.Falla || _panelesConFalla.Contains(panel.NumeroSerie)
            || _panelesConFalla.Contains(panel.Id.ToString("D"));
        var potencia = panel.Estado == EstadoPanel.Activo && !fallado
            ? decimal.Round(panel.PotenciaNominalW * CalcularProporcionSolar(horaLocal), 6) : 0m;
        return new MedicionPanelDto(panel.Id, potencia, fallado ? EstadoSaludLectura.Fallado : EstadoSaludLectura.Sano);
    }

    public Task<IReadOnlyList<PanelInversorEstadoDto>> ObtenerEstadoPanelesAsync(
        string nodoId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var horaLocal = _timeProvider.GetLocalNow();
            IReadOnlyList<PanelInversorEstadoDto> estado = ObtenerPaneles(nodoId)
                .Order(StringComparer.Ordinal)
                .Select(panelId => CrearEstadoPanel(nodoId, panelId, horaLocal))
                .ToArray();
            return Task.FromResult(estado);
        }
    }

    public Task<decimal> ObtenerPotenciaGeneradaAsync(
        string nodoId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var horaLocal = _timeProvider.GetLocalNow();
            var potencia = ObtenerPaneles(nodoId)
                .Sum(panelId => CalcularPotencia(panelId, nodoId, horaLocal));
            return Task.FromResult(potencia);
        }
    }

    public Task ActivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ValidarPanel(nodoId, panelId);
            _panelesDesactivados.Remove((nodoId, panelId));
        }

        return Task.CompletedTask;
    }

    public Task DesactivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ValidarPanel(nodoId, panelId);
            _panelesDesactivados.Add((nodoId, panelId));
        }

        return Task.CompletedTask;
    }

    public Task ActivarVolcadoRedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            _volcadoRedActivo = true;
        return Task.CompletedTask;
    }

    public Task DesactivarVolcadoRedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            _volcadoRedActivo = false;
        return Task.CompletedTask;
    }

    public Task<bool> ObtenerEstadoVolcadoRedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            return Task.FromResult(_volcadoRedActivo);
    }

    private PanelInversorEstadoDto CrearEstadoPanel(string nodoId, string panelId, DateTimeOffset horaLocal)
    {
        var estaActivo = !_panelesDesactivados.Contains((nodoId, panelId));
        var estaFallado = _panelesConFalla.Contains(panelId);
        var salud = estaFallado ? EstadoSaludPanel.Fallado : EstadoSaludPanel.Saludable;
        var potencia = estaActivo ? CalcularPotencia(panelId, nodoId, horaLocal) : 0m;
        return new PanelInversorEstadoDto(nodoId, panelId, estaActivo, salud, potencia);
    }

    private decimal CalcularPotencia(string panelId, string nodoId, DateTimeOffset horaLocal)
    {
        if (_panelesDesactivados.Contains((nodoId, panelId)) || _panelesConFalla.Contains(panelId))
            return 0m;

        return decimal.Round(PotenciaMaximaPanelW * CalcularProporcionSolar(horaLocal), 2);
    }

    private static decimal CalcularProporcionSolar(DateTimeOffset horaLocal)
    {
        var horaDecimal = horaLocal.TimeOfDay.TotalHours;
        if (horaDecimal <= HoraInicioSolar || horaDecimal >= HoraFinSolar)
            return 0m;

        var proporcion = Math.Sin(Math.PI * (horaDecimal - HoraInicioSolar) / (HoraFinSolar - HoraInicioSolar));
        return (decimal)Math.Max(0d, proporcion);
    }

    private HashSet<string> ObtenerPaneles(string nodoId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodoId);
        if (!_panelesPorNodo.TryGetValue(nodoId, out var paneles))
            throw new KeyNotFoundException($"No existe el nodo simulado '{nodoId}'.");
        return paneles;
    }

    private void ValidarPanel(string nodoId, string panelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        if (!ObtenerPaneles(nodoId).Contains(panelId))
            throw new KeyNotFoundException($"No existe el panel simulado '{panelId}' en el nodo '{nodoId}'.");
    }

    private static Dictionary<string, HashSet<string>> CargarPanelesPorNodo(IConfiguration configuration)
    {
        var nodos = configuration.GetSection("InversorSimulador:Nodos").GetChildren()
            .ToDictionary(
                nodo => nodo.Key,
                nodo => (nodo.Value ?? string.Empty)
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        if (nodos.Count > 0)
            return nodos;

        return new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["nodo-1"] = ["panel-1", "panel-2", "panel-3", "panel-4"]
        };
    }

    private static HashSet<string> CargarPanelesConFalla(IConfiguration configuration)
    {
        return (configuration["InversorSimulador:PanelesConFalla"] ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
    }
}
