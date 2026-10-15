namespace SolarUY.Application.Mantenimiento;

public interface ITecnicoDirectory
{
    Task<IReadOnlyList<TecnicoDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<bool> EsTecnicoHabilitadoAsync(string responsableId, CancellationToken cancellationToken = default);
}
