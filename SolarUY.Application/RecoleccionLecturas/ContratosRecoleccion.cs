using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;

namespace SolarUY.Application.RecoleccionLecturas;

public sealed record PanelMedicionDto(Guid Id, Guid NodoId, string NumeroSerie, decimal PotenciaNominalW, EstadoPanel Estado);
public sealed record MedicionPanelDto(Guid PanelId, decimal PotenciaGeneradaW, EstadoSaludLectura EstadoSalud);
public sealed record ResultadoRecoleccionDto(bool AdquirioExclusion, int LecturasGuardadas,
    IReadOnlyList<SolarUY.Application.Monitoreo.LecturaConfirmadaDto>? LecturasConfirmadas = null);

public interface IRecoleccionLecturasRepository
{
    Task<ISesionRecoleccionLecturas?> IntentarAbrirSesionAsync(CancellationToken cancellationToken = default);
}

public interface ISesionRecoleccionLecturas : IAsyncDisposable
{
    Task<IReadOnlyList<Panel>> ListarPendientesAsync(DateTime instanteUtc, CancellationToken cancellationToken = default);
    Task GuardarAsync(IReadOnlyList<LecturaPanel> lecturas, CancellationToken cancellationToken = default);
}

public interface IRecoleccionLecturasService
{
    Task<ResultadoRecoleccionDto> RecolectarAsync(CancellationToken cancellationToken = default);
}
