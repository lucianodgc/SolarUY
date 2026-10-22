using SolarUY.Domain.Entities;

namespace SolarUY.Application.ParquesSolares;

public interface IParqueSolarService
{
    Task<Guid> CrearParqueSolarAsync(CrearParqueSolarDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParqueSolar>> ListarParquesSolaresAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParqueSolar>> BuscarParquesSolaresAsync(
        string? criterio,
        CancellationToken cancellationToken = default);
    Task<ParqueSolar?> ObtenerParqueSolarPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task ActualizarParqueSolarAsync(
        Guid id,
        ActualizarParqueSolarDto dto,
        CancellationToken cancellationToken = default);
    Task EliminarParqueSolarAsync(Guid id, CancellationToken cancellationToken = default);
}
