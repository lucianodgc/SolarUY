using SolarUY.Domain.Entities;

namespace SolarUY.Application.Inversores;

public sealed record SolicitudOrdenInversorDto(Guid Correlacion, AccionOrdenInversor Accion, string? NodoId = null, string? PanelId = null);
public sealed record ResultadoOrdenInversorDto(Guid? IntentoId, EstadoOrdenInversor Estado, string Codigo, bool AuditoriaCompleta);

public interface IOrdenInversorService
{
    Task<ResultadoOrdenInversorDto> EjecutarAsync(SolicitudOrdenInversorDto solicitud, CancellationToken cancellationToken = default);
}

public interface IIntentoOrdenInversorRepository
{
    Task<IAsyncDisposable?> IntentarBloqueoAsync(CancellationToken cancellationToken);
    Task<IntentoOrdenInversor?> BuscarAsync(string actorSub, Guid correlacion, CancellationToken cancellationToken);
    Task GuardarAsync(IntentoOrdenInversor intento, CancellationToken cancellationToken);
    Task RecuperarPendientesAsync(DateTime ahoraUtc, CancellationToken cancellationToken);
}

public sealed class OrdenInversorOptions
{
    public int CierreAuditoriaSegundos { get; set; } = 5;
    public int IntentosPersistencia { get; set; } = 2;
}
