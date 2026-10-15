namespace SolarUY.Application.Mantenimiento;

public interface ITareaMantenimientoService
{
    Task<Guid> CrearAsync(CrearTareaMantenimientoDto dto, CancellationToken cancellationToken = default);
    Task AsignarAsync(Guid id, string responsableId, CancellationToken cancellationToken = default);
    Task EditarAsync(Guid id, EditarTareaMantenimientoDto dto, CancellationToken cancellationToken = default);
    Task IniciarAsync(Guid id, CancellationToken cancellationToken = default);
    Task CompletarAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelarAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TareaMantenimientoDto> ObtenerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TareaMantenimientoDto>> ListarAsync(FiltroTareasDto filtro, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PanelDto>> ListarPanelesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TecnicoDto>> ListarTecnicosAsync(CancellationToken cancellationToken = default);
}
