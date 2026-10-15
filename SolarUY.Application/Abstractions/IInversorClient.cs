namespace SolarUY.Application.Abstractions;

public interface IInversorClient
{
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
}

public sealed record PanelInversorEstadoDto(
    string NodoId,
    string PanelId,
    bool EstaActivo,
    EstadoSaludPanel EstadoSalud,
    decimal PotenciaGeneradaW);

public enum EstadoSaludPanel
{
    Saludable,
    Fallado
}
