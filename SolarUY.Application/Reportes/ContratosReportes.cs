using System.Globalization;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Domain.Enums;

namespace SolarUY.Application.Reportes;

public sealed record ConsultaMantenimientosReporteDto(Guid ParqueId, Guid? NodoId, DateTime InicioUtc, DateTime FinUtc);

public sealed record MantenimientoRealizadoReporteDto(Guid Id, Guid PanelId, string NumeroSerie, string Tipo,
    string Descripcion, string ResponsableId, EstadoTareaMantenimiento Estado,
    DateTime FechaProgramada, DateTime FechaRealizacion)
{
    public string FechaProgramadaLocal => FormatoReporte.FechaLocal(FechaProgramada);
    public string FechaRealizacionLocal => FormatoReporte.FechaLocal(FechaRealizacion);
}

public sealed class ResultadoReporteDto
{
    public FiltroReporteDto Filtro { get; }
    public string Parque { get; }
    public string? Nodo { get; }
    public ResultadoHistoricoDto Generacion { get; }
    public IReadOnlyList<MantenimientoRealizadoReporteDto> Mantenimientos { get; }
    public GraficoReporteDto Grafico { get; }

    internal ResultadoReporteDto(FiltroReporteDto filtro, string parque, string? nodo,
        ResultadoHistoricoDto generacion, IReadOnlyList<MantenimientoRealizadoReporteDto> mantenimientos)
    {
        Filtro = filtro;
        Parque = parque;
        Nodo = nodo;
        Generacion = generacion with { Serie = Array.AsReadOnly(generacion.Serie.ToArray()) };
        Mantenimientos = Array.AsReadOnly(mantenimientos.ToArray());
        Grafico = GraficoReporteDto.DesdeHistorico(Generacion);
    }
}

public sealed record ArchivoReporteCsvDto(string NombreArchivo, string TipoContenido, byte[] Contenido);

public interface IReporteHistoricoService
{
    Task<IReadOnlyList<OpcionHistoricoDto>> ListarOpcionesAsync(CancellationToken cancellationToken = default);
    Task<ResultadoReporteDto> ConsultarAsync(FiltroReporteDto filtro, CancellationToken cancellationToken = default);
    Task<ArchivoReporteCsvDto> ExportarCsvAsync(ResultadoReporteDto reporte, CancellationToken cancellationToken = default);
}

internal static class FormatoReporte
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");
    public static string FechaLocal(DateTime utc) => new DateTimeOffset(
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona), Zona.GetUtcOffset(utc))
        .ToString("O", CultureInfo.InvariantCulture);
}
