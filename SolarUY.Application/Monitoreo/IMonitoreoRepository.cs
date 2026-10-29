using SolarUY.Domain.Entities;

namespace SolarUY.Application.Monitoreo;

public sealed record ParqueMonitoreoDto(Guid Id, string Nombre);

public sealed record DatosMonitoreoDto(
    ParqueSolar Parque,
    IReadOnlyList<Nodo> Nodos,
    IReadOnlyList<Panel> Paneles,
    IReadOnlyList<LecturaPanel> Lecturas);

public interface IMonitoreoRepository
{
    Task<IReadOnlyList<ParqueMonitoreoDto>> ListarParquesAsync(CancellationToken cancellationToken = default);
    Task<DatosMonitoreoDto?> ObtenerDatosAsync(Guid parqueId, DateTime referenciaUtc,
        CancellationToken cancellationToken = default);
}
