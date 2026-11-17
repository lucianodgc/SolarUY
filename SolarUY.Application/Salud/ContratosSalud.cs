using SolarUY.Domain.Salud;

namespace SolarUY.Application.Salud;

public sealed record AvisoSaludConfirmada(Guid ParqueId, Guid? PanelId, long Version);
public sealed record SaludPanelDto(Guid PanelId, string NumeroSerie, Guid NodoId, string Nodo,
    EstadoSaludPanel? Estado, bool Vigente, string Motivo, DateTime? EvidenciaUtc, DateTime? EvaluadoUtc, long Version);
public sealed record SaludNodoDto(Guid NodoId, string Nombre, EstadoSaludPanel? Estado, int Evaluados, int Total);
public sealed record AlertaActivaDto(Guid Id, Guid PanelId, string NumeroSerie, Guid NodoId, string Nodo,
    Guid ParqueId, string Parque, EstadoSaludPanel Condicion, DateTime DeteccionUtc, bool Vigente);
public sealed record CambioEstadoSaludDto(EstadoSaludPanel? EstadoAnterior, EstadoSaludPanel EstadoNuevo,
    DateTime FechaUtc, long Version);
public sealed record PaginaCambiosSaludDto(IReadOnlyList<CambioEstadoSaludDto> Elementos, int Total);
public sealed record SaludParqueDto(Guid ParqueId, IReadOnlyList<SaludPanelDto> Paneles,
    IReadOnlyList<SaludNodoDto> Nodos, IReadOnlyList<AlertaActivaDto> Alertas);
public sealed record DatosConsultaSaludDto(IReadOnlyList<SaludPanelDto> Paneles,
    IReadOnlyList<(Guid Id, string Nombre)> Nodos, IReadOnlyList<AlertaActivaDto> Alertas);

public interface ISaludPanelRepository
{
    Task<IReadOnlyList<Guid>> ListarPanelesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AvisoSaludConfirmada>> EvaluarLoteAsync(IReadOnlyList<Guid> paneles,
        DateTime referenciaUtc, PoliticaSalud politica, CancellationToken cancellationToken);
    Task<DatosConsultaSaludDto?> ConsultarAsync(Guid parqueId, DateTime referenciaUtc,
        TimeSpan vigenciaEvaluacion, CancellationToken cancellationToken);
    Task<PaginaCambiosSaludDto> HistorialAsync(Guid parqueId, Guid panelId, int pagina, int tamano,
        CancellationToken cancellationToken);
}
public interface ISaludPanelService
{
    Task<IReadOnlyList<AvisoSaludConfirmada>> EvaluarAsync(CancellationToken cancellationToken = default);
}
public interface IConsultaSaludService
{
    SaludParqueDto Reevaluar(SaludParqueDto estado);
    Task<SaludParqueDto> ConsultarAsync(Guid parqueId, CancellationToken cancellationToken = default);
    Task<PaginaCambiosSaludDto> HistorialAsync(Guid parqueId, Guid panelId, int pagina = 0, int tamano = 20,
        CancellationToken cancellationToken = default);
}
public interface INotificadorSaludConfirmada
{
    Task NotificarAsync(IReadOnlyList<AvisoSaludConfirmada> avisos, CancellationToken cancellationToken = default);
}
