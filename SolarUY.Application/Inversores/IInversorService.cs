namespace SolarUY.Application.Inversores;

public interface IInversorService
{
    Task<IReadOnlyList<string>> ListarNodosAsync(CancellationToken cancellationToken = default);
    Task<EstadoInversorDto> ConsultarAsync(string nodoId, CancellationToken cancellationToken = default);
    Task EstablecerPanelActivoAsync(string nodoId, string panelId, bool activo, CancellationToken cancellationToken = default);
    Task EstablecerVolcadoRedAsync(bool activo, CancellationToken cancellationToken = default);
}
