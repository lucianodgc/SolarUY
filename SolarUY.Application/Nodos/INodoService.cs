namespace SolarUY.Application.Nodos;

public interface INodoService
{
    Task<Guid> CrearAsync(Guid parqueId, CrearNodoDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NodoDto>> ListarPorParqueAsync(Guid parqueId, CancellationToken cancellationToken = default);
    Task<NodoDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task ActualizarAsync(Guid id, ActualizarNodoDto dto, CancellationToken cancellationToken = default);
    Task EliminarAsync(Guid id, CancellationToken cancellationToken = default);
}
