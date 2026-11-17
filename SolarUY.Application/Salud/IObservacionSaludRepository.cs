namespace SolarUY.Application.Salud;

public sealed record FirmaSaludDto(Guid PanelId, Guid NodoId, Guid ParqueId, long Version,
    DateTime? EvaluadoUtc, Guid? NodoEvaluadoId, bool Suficiente);
public sealed record SnapshotSaludDto(IReadOnlyList<Guid> Parques, IReadOnlyList<FirmaSaludDto> Paneles);

public interface IObservacionSaludRepository
{
    Task<SnapshotSaludDto> LeerSnapshotAsync(CancellationToken cancellationToken = default);
}
