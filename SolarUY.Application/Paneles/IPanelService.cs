namespace SolarUY.Application.Paneles;

public interface IPanelService
{
    Task<Guid> CrearAsync(CrearPanelDto dto, CancellationToken cancellationToken = default);
    Task<PanelDto?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PanelDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PanelDto>> ListarPorNodoAsync(Guid nodoId, CancellationToken cancellationToken = default);
    Task ActualizarAsync(Guid id, ActualizarPanelDto dto, CancellationToken cancellationToken = default);
    Task DarDeBajaAsync(Guid id, CancellationToken cancellationToken = default);
}
