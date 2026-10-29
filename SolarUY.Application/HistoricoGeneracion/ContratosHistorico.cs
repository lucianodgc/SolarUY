namespace SolarUY.Application.HistoricoGeneracion;

public enum TipoObjetivoHistorico { Parque, Nodo, Panel }
public enum PeriodoHistorico { Diario, Mensual, Anual, Rango }
public enum CoberturaHistorico { SinDatos, Parcial, Completa }

public sealed record FiltroHistoricoDto(TipoObjetivoHistorico Tipo, Guid ObjetivoId,
    PeriodoHistorico Periodo, DateTime InicioLocal, DateTime? FinLocal = null);

public sealed record OpcionHistoricoDto(TipoObjetivoHistorico Tipo, Guid Id, string Etiqueta,
    Guid? ParqueId = null, Guid? NodoId = null);

public sealed record IntervaloHistoricoDto(DateTime InicioUtc, DateTime FinUtc, string Etiqueta);

public sealed record ConsultaHistoricoDto(TipoObjetivoHistorico Tipo, Guid ObjetivoId,
    DateTime InicioUtc, DateTime FinUtc, IReadOnlyList<IntervaloHistoricoDto> Intervalos);

public sealed record PuntoHistoricoDto(IntervaloHistoricoDto Intervalo, decimal? EnergiaKWh,
    CoberturaHistorico Cobertura, bool EsMaximo, bool EsMinimo);

public sealed record ResultadoHistoricoDto(ConsultaHistoricoDto Consulta, decimal? EnergiaKWh,
    CoberturaHistorico Cobertura, IReadOnlyList<PuntoHistoricoDto> Serie)
{
    public long LecturasSinNodoHistorico { get; init; }
    public string Unidad => "kWh";
    public string ZonaHoraria => "America/Montevideo";
    public string InicioLocal => TimeZoneInfo.ConvertTimeFromUtc(Consulta.InicioUtc, Zona).ToString("yyyy-MM-dd HH:mm");
    public string FinLocal => TimeZoneInfo.ConvertTimeFromUtc(Consulta.FinUtc, Zona).ToString("yyyy-MM-dd HH:mm");
    private static TimeZoneInfo Zona => TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");
}

public sealed record ComparacionHistoricoDto(ResultadoHistoricoDto Primero, ResultadoHistoricoDto Segundo);

public interface IHistoricoGeneracionRepository
{
    Task<bool> ExisteObjetivoAsync(TipoObjetivoHistorico tipo, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpcionHistoricoDto>> ListarOpcionesAsync(CancellationToken cancellationToken = default);
    Task<ResultadoHistoricoDto> ConsultarAsync(ConsultaHistoricoDto consulta, CancellationToken cancellationToken = default);
}

public interface IHistoricoGeneracionService
{
    Task<IReadOnlyList<OpcionHistoricoDto>> ListarOpcionesAsync(CancellationToken cancellationToken = default);
    Task<ResultadoHistoricoDto> ConsultarAsync(FiltroHistoricoDto filtro, CancellationToken cancellationToken = default);
    Task<ComparacionHistoricoDto> CompararAsync(FiltroHistoricoDto primero, FiltroHistoricoDto segundo, CancellationToken cancellationToken = default);
}
