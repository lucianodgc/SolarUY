using SolarUY.Domain.Entities;

namespace SolarUY.Application.Abstractions;

public interface IApplicationDbContext
{
    Task<bool> ExisteNombreParqueSolarAsync(string nombre, CancellationToken cancellationToken = default);
    void AgregarParqueSolar(ParqueSolar parque);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParqueSolar>> ListarParquesSolaresAsync(CancellationToken cancellationToken = default);
}
