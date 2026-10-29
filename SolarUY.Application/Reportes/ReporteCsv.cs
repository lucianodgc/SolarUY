using System.Globalization;
using System.Text;

namespace SolarUY.Application.Reportes;

internal static class ReporteCsv
{
    public static ArchivoReporteCsvDto Crear(ResultadoReporteDto reporte, CancellationToken cancellationToken)
    {
        var texto = new StringBuilder();
        EscribirFila(texto, ["TipoRegistro", "ParqueId", "Parque", "NodoId", "Nodo", "Periodo", "InicioPeriodo",
            "FinPeriodo", "ZonaHoraria", "EnergiaTotalKWh", "CoberturaPeriodo", "InicioIntervalo", "FinIntervalo",
            "EnergiaKWh", "Cobertura", "TareaId", "PanelId", "Panel", "TipoMantenimiento", "Descripcion",
            "Responsable", "Estado", "FechaProgramada", "FechaRealizacion"]);
        var generacion = reporte.Generacion;
        string?[] comunes = [reporte.Filtro.ParqueId.ToString("D"), reporte.Parque, reporte.Filtro.NodoId?.ToString("D"),
            reporte.Nodo, reporte.Filtro.Periodo.ToString(), FormatoReporte.FechaLocal(generacion.Consulta.InicioUtc),
            FormatoReporte.FechaLocal(generacion.Consulta.FinUtc), generacion.ZonaHoraria,
            Numero(generacion.EnergiaKWh), generacion.Cobertura.ToString()];
        foreach (var punto in generacion.Serie)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EscribirFila(texto, ["Generacion", .. comunes, FormatoReporte.FechaLocal(punto.Intervalo.InicioUtc),
                FormatoReporte.FechaLocal(punto.Intervalo.FinUtc), Numero(punto.EnergiaKWh), punto.Cobertura.ToString(),
                .. new string?[9]]);
        }
        foreach (var tarea in reporte.Mantenimientos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EscribirFila(texto, ["Mantenimiento", .. comunes, .. new string?[4], tarea.Id.ToString("D"),
                tarea.PanelId.ToString("D"), tarea.NumeroSerie, tarea.Tipo, tarea.Descripcion, tarea.ResponsableId,
                tarea.Estado.ToString(), tarea.FechaProgramadaLocal, tarea.FechaRealizacionLocal]);
        }
        if (generacion.LecturasSinNodoHistorico > 0)
            EscribirFila(texto, ["Advertencia", .. comunes, .. new string?[8],
                $"Existen {generacion.LecturasSinNodoHistorico} lecturas sin nodo histórico verificado en la base durante el período; no se atribuyen a nodos ni parques. Consultar por panel.",
                .. new string?[4]]);
        cancellationToken.ThrowIfCancellationRequested();
        var nombre = $"reporte-{generacion.Consulta.ObjetivoId:N}-{generacion.Consulta.InicioUtc:yyyyMMddHHmm}-{generacion.Consulta.FinUtc:yyyyMMddHHmm}.csv";
        return new(nombre, "text/csv;charset=utf-8", new UTF8Encoding(false).GetBytes(texto.ToString()));
    }

    private static string? Numero(decimal? valor) => valor?.ToString(CultureInfo.InvariantCulture);
    private static void EscribirFila(StringBuilder texto, string?[] valores)
        => texto.AppendJoin(';', valores.Select(valor => '"' + (valor ?? string.Empty).Replace("\"", "\"\"") + '"')).Append("\r\n");
}
