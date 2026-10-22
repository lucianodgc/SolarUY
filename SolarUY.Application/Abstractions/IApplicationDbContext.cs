using SolarUY.Domain.Entities;

namespace SolarUY.Application.Abstractions;

public interface IApplicationDbContext
{
    Task<bool> ExisteNombreParqueSolarAsync(
        string nombre,
        Guid? parqueIdExcluido = null,
        CancellationToken cancellationToken = default);
    Task<ParqueSolar?> ObtenerParqueSolarPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    void AgregarParqueSolar(ParqueSolar parque);
    void ActualizarParqueSolar(ParqueSolar parque);
    Task EliminarParqueSolarEnCascadaAsync(ParqueSolar parque, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParqueSolar>> ListarParquesSolaresAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParqueSolar>> BuscarParquesSolaresAsync(
        string? criterio,
        CancellationToken cancellationToken = default);
}
