using SolarUY.Domain.Entities;

namespace SolarUY.Application.Nodos;

public interface INodoRepository
{
    Task<ParqueSolar?> ObtenerParqueAsync(Guid parqueId, CancellationToken cancellationToken = default);
    Task<Nodo?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Nodo?> ObtenerParaEliminarAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Nodo>> ListarPorParqueAsync(Guid parqueId, CancellationToken cancellationToken = default);
    Task AgregarAsync(Nodo nodo, CancellationToken cancellationToken = default);
    Task ActualizarAsync(Nodo nodo, CancellationToken cancellationToken = default);
    Task EliminarEnCascadaAsync(Nodo nodo, CancellationToken cancellationToken = default);
}
