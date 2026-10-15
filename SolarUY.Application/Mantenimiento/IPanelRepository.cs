using SolarUY.Domain.Entities;

namespace SolarUY.Application.Mantenimiento;

public interface IPanelRepository
{
    Task<Panel?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PanelDto>> ListarAsync(CancellationToken cancellationToken = default);
}
