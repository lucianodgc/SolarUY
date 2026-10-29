using Microsoft.Extensions.Configuration;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Inversores;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Domain.Enums;

namespace SolarUY.Infrastructure.Inversores;

public sealed class InversorSimulador : IInversorClient
{
    private readonly object _sync = new();
    private Dictionary<string, InventarioNodoInversorDto> _nodos = new(StringComparer.Ordinal);
    private readonly HashSet<string> _panelesConFalla;
    private readonly HashSet<(string NodoId, string PanelId)> _panelesDesactivados = [];
    private readonly HashSet<(string NodoId, string PanelId)> _panelesActivados = [];
    private bool _volcadoRedActivo;
    private bool _inversorEncendido = true;

    public InversorSimulador(IConfiguration configuration)
    {
        _panelesConFalla = CargarPanelesConFalla(configuration);
    }

    public Task SincronizarInventarioAsync(IReadOnlyList<InventarioNodoInversorDto> nodos, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(nodos);
        var nuevoInventario = nodos.ToDictionary(nodo => nodo.Id, StringComparer.Ordinal);
        if (nuevoInventario.Values.Any(nodo => string.IsNullOrWhiteSpace(nodo.Id)
            || nodo.Paneles.Any(panel => string.IsNullOrWhiteSpace(panel.Id) || panel.PotenciaNominalW < 0)))
            throw new ArgumentException("El inventario contiene identificadores o potencias inválidos.", nameof(nodos));

        lock (_sync)
        {
            _nodos = nuevoInventario;
            var panelesValidos = _nodos.Values.SelectMany(nodo => nodo.Paneles.Select(panel => (NodoId: nodo.Id, PanelId: panel.Id))).ToHashSet();
            _panelesDesactivados.RemoveWhere(panel => !panelesValidos.Contains(panel));
            _panelesActivados.RemoveWhere(panel => !panelesValidos.Contains(panel));
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListarNodosAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> nodos;
        lock (_sync)
            nodos = _nodos.Keys.Order(StringComparer.Ordinal).ToArray();
        return Task.FromResult(nodos);
    }

    public Task<IReadOnlyList<MedicionPanelDto>> ObtenerMedicionesRegistradasAsync(
        IReadOnlyList<PanelMedicionDto> paneles, DateTime instanteUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (instanteUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("La medición requiere un instante UTC.", nameof(instanteUtc));
        lock (_sync)
        {
            IReadOnlyList<MedicionPanelDto> resultado = paneles.Select(MedirPanelRegistrado).ToArray();
            return Task.FromResult(resultado);
        }
    }

    private MedicionPanelDto MedirPanelRegistrado(PanelMedicionDto panel)
    {
        if (panel.PotenciaNominalW < 0) throw new InvalidOperationException($"El panel {panel.Id} tiene una potencia nominal inválida.");
        var fallado = panel.Estado == EstadoPanel.Falla || _panelesConFalla.Contains(panel.NumeroSerie)
            || _panelesConFalla.Contains(panel.Id.ToString("D"));
        var potenciaBase = _inversorEncendido && panel.Estado == EstadoPanel.Activo && !fallado
            ? panel.PotenciaNominalW : 0m;
        var potencia = AplicarVariacionAleatoria(potenciaBase, 6);
        return new MedicionPanelDto(panel.Id, potencia, fallado ? EstadoSaludLectura.Fallado : EstadoSaludLectura.Sano);
    }

    public Task<IReadOnlyList<PanelInversorEstadoDto>> ObtenerEstadoPanelesAsync(
        string nodoId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            IReadOnlyList<PanelInversorEstadoDto> estado = ObtenerNodo(nodoId).Paneles
                .OrderBy(panel => panel.NumeroSerie, StringComparer.Ordinal)
                .Select(panel => CrearEstadoPanel(nodoId, panel))
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
            var potencia = ObtenerNodo(nodoId).Paneles
                .Sum(panel => CalcularPotencia(nodoId, panel));
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
            _panelesActivados.Add((nodoId, panelId));
        }

        return Task.CompletedTask;
    }

    public Task DesactivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ValidarPanel(nodoId, panelId);
            _panelesActivados.Remove((nodoId, panelId));
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

    public Task<bool> ObtenerEstadoEncendidoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            return Task.FromResult(_inversorEncendido);
    }

    public Task EstablecerEstadoEncendidoAsync(bool encendido, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            _inversorEncendido = encendido;
        return Task.CompletedTask;
    }

    private PanelInversorEstadoDto CrearEstadoPanel(string nodoId, InventarioPanelInversorDto panel)
    {
        var estaActivo = _panelesActivados.Contains((nodoId, panel.Id))
            || (!_panelesDesactivados.Contains((nodoId, panel.Id)) && panel.Estado == EstadoPanel.Activo);
        var estaFallado = panel.Estado == EstadoPanel.Falla || _panelesConFalla.Contains(panel.Id)
            || _panelesConFalla.Contains(panel.NumeroSerie);
        var salud = estaFallado ? EstadoSaludPanel.Fallado : EstadoSaludPanel.Saludable;
        var potencia = estaActivo && _inversorEncendido ? CalcularPotencia(nodoId, panel) : 0m;
        return new PanelInversorEstadoDto(nodoId, panel.Id, estaActivo, salud, potencia, panel.NumeroSerie);
    }

    private decimal CalcularPotencia(string nodoId, InventarioPanelInversorDto panel)
    {
        if (!_inversorEncendido || _panelesDesactivados.Contains((nodoId, panel.Id))
            || panel.Estado != EstadoPanel.Activo || panel.Estado == EstadoPanel.Falla
            || _panelesConFalla.Contains(panel.Id) || _panelesConFalla.Contains(panel.NumeroSerie))
            return 0m;

        return AplicarVariacionAleatoria(panel.PotenciaNominalW, 2);
    }

    private static decimal AplicarVariacionAleatoria(decimal potenciaBase, int decimales)
    {
        if (potenciaBase <= 0m) return 0m;
        var factor = 0.90d + Random.Shared.NextDouble() * 0.10d;
        return decimal.Round(potenciaBase * (decimal)factor, decimales);
    }

    private InventarioNodoInversorDto ObtenerNodo(string nodoId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodoId);
        if (!_nodos.TryGetValue(nodoId, out var nodo))
            throw new KeyNotFoundException($"No existe el nodo '{nodoId}' en el inventario sincronizado.");
        return nodo;
    }

    private void ValidarPanel(string nodoId, string panelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        if (!ObtenerNodo(nodoId).Paneles.Any(panel => panel.Id == panelId))
            throw new KeyNotFoundException($"No existe el panel '{panelId}' en el nodo '{nodoId}' del inventario sincronizado.");
    }

    private static HashSet<string> CargarPanelesConFalla(IConfiguration configuration)
    {
        return (configuration["InversorSimulador:PanelesConFalla"] ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
    }
}
