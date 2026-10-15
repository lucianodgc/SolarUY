using SolarUY.Domain.Entities;

namespace SolarUY.Application.Mantenimiento;

public interface ITareaMantenimientoRepository
{
    Task AgregarAsync(TareaMantenimiento tarea, CancellationToken cancellationToken = default);
    Task<TareaMantenimiento?> ObtenerAsync(Guid id, string? responsableAutorizado, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TareaMantenimiento>> ListarAsync(FiltroTareasDto filtro, string? responsableAutorizado,
        DateTime referenciaUtc, CancellationToken cancellationToken = default);
    Task<bool> GuardarAsync(TareaMantenimiento tarea, TareaMantenimientoOriginal original,
        string? responsableAutorizado, CancellationToken cancellationToken = default);
}
