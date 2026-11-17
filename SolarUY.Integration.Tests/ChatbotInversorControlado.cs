using SolarUY.Application.Abstractions;
using SolarUY.Application.Inversores;
using SolarUY.Application.RecoleccionLecturas;

namespace SolarUY.Integration.Tests;

internal sealed class InversorChatbotControlado : IInversorClient
{
    public IReadOnlyList<PanelInversorEstadoDto> Paneles { get; set; } = [];
    public int Escrituras { get; private set; }
    public bool Caido { get; set; }
    private Task Orden() { Escrituras++; throw new InvalidOperationException("El chat intentó una orden o sincronización."); }
    public Task SincronizarInventarioAsync(IReadOnlyList<InventarioNodoInversorDto> nodos, CancellationToken cancellationToken = default) => Orden();
    public Task ActivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default) => Orden();
    public Task DesactivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default) => Orden();
    public Task ActivarVolcadoRedAsync(CancellationToken cancellationToken = default) => Orden();
    public Task DesactivarVolcadoRedAsync(CancellationToken cancellationToken = default) => Orden();
    public Task EstablecerEstadoEncendidoAsync(bool encendido, CancellationToken cancellationToken = default) => Orden();
    public Task<IReadOnlyList<MedicionPanelDto>> ObtenerMedicionesRegistradasAsync(IReadOnlyList<PanelMedicionDto> paneles, DateTime instanteUtc, CancellationToken cancellationToken = default)
    { Escrituras++; throw new InvalidOperationException("El chat intentó adquirir lecturas."); }
    public Task<IReadOnlyList<string>> ListarNodosAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<PanelInversorEstadoDto>> ObtenerEstadoPanelesAsync(string nodoId, CancellationToken cancellationToken = default)
        => Caido ? throw new IOException("Simulador no disponible.") : Task.FromResult(Paneles);
    public Task<decimal> ObtenerPotenciaGeneradaAsync(string nodoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ObtenerEstadoVolcadoRedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> ObtenerEstadoEncendidoAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
