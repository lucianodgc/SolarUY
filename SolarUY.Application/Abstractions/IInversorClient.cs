using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Application.Inversores;

namespace SolarUY.Application.Abstractions;

public interface IInversorClient
{
    Task SincronizarInventarioAsync(IReadOnlyList<InventarioNodoInversorDto> nodos, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MedicionPanelDto>> ObtenerMedicionesRegistradasAsync(
        IReadOnlyList<PanelMedicionDto> paneles, DateTime instanteUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListarNodosAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PanelInversorEstadoDto>> ObtenerEstadoPanelesAsync(
        string nodoId,
        CancellationToken cancellationToken = default);

    Task<decimal> ObtenerPotenciaGeneradaAsync(
        string nodoId,
        CancellationToken cancellationToken = default);

    Task ActivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default);

    Task DesactivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default);

    Task ActivarVolcadoRedAsync(CancellationToken cancellationToken = default);

    Task DesactivarVolcadoRedAsync(CancellationToken cancellationToken = default);

    Task<bool> ObtenerEstadoVolcadoRedAsync(CancellationToken cancellationToken = default);

    Task<bool> ObtenerEstadoEncendidoAsync(CancellationToken cancellationToken = default);

    Task EstablecerEstadoEncendidoAsync(bool encendido, CancellationToken cancellationToken = default);
}

public sealed record PanelInversorEstadoDto(
    string NodoId,
    string PanelId,
    bool EstaActivo,
    EstadoSaludPanel EstadoSalud,
    decimal PotenciaGeneradaW,
    string NumeroSerie = "");

public enum EstadoSaludPanel
{
    Saludable,
    Fallado
}
