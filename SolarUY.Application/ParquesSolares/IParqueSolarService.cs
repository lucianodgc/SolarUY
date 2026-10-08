using SolarUY.Domain.Entities;

namespace SolarUY.Application.ParquesSolares;

public interface IParqueSolarService
{
    Task<Guid> CrearParqueSolarAsync(CrearParqueSolarDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParqueSolar>> ListarParquesSolaresAsync(CancellationToken cancellationToken = default);
}
