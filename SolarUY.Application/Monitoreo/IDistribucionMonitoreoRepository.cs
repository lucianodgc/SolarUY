namespace SolarUY.Application.Monitoreo;

public sealed record AvisoMonitoreoDto(long Secuencia, IReadOnlyList<LecturaConfirmadaDto> Lecturas);

public interface IDistribucionMonitoreoRepository
{
    Task<IReadOnlyList<AvisoMonitoreoDto>> LeerPendientesAsync(string instanciaId, int limite, CancellationToken cancellationToken);
    Task ConfirmarAsync(string instanciaId, long secuencia, CancellationToken cancellationToken);
}
