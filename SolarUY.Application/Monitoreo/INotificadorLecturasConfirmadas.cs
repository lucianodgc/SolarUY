namespace SolarUY.Application.Monitoreo;

public sealed record LecturaConfirmadaDto(Guid ParqueId, Guid PanelId, DateTime FechaHoraUtc);

public interface INotificadorLecturasConfirmadas
{
    Task NotificarAsync(IReadOnlyList<LecturaConfirmadaDto> lecturas, CancellationToken cancellationToken = default);
}
