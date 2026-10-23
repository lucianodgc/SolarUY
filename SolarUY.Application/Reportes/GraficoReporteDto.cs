using SolarUY.Application.HistoricoGeneracion;

namespace SolarUY.Application.Reportes;

public sealed record PuntoGraficoReporteDto(DateTime InstanteUtc, string Etiqueta, decimal EnergiaKWh);

public sealed record GraficoReporteDto(IReadOnlyList<PuntoGraficoReporteDto> Puntos,
    IReadOnlyList<IReadOnlyList<PuntoGraficoReporteDto>> Segmentos, decimal MaximoEjeKWh)
{
    public decimal PasoEjeKWh => MaximoEjeKWh / 4m;

    public static GraficoReporteDto DesdeHistorico(ResultadoHistoricoDto resultado)
    {
        var puntos = new List<PuntoGraficoReporteDto>();
        var segmentos = new List<IReadOnlyList<PuntoGraficoReporteDto>>();
        var actual = new List<PuntoGraficoReporteDto>();
        foreach (var punto in resultado.Serie)
        {
            if (punto.EnergiaKWh is not { } energia)
            {
                if (actual.Count > 0) segmentos.Add(actual.AsReadOnly());
                actual = [];
                continue;
            }
            var dato = new PuntoGraficoReporteDto(punto.Intervalo.InicioUtc, punto.Intervalo.Etiqueta, energia);
            puntos.Add(dato);
            actual.Add(dato);
        }
        if (actual.Count > 0) segmentos.Add(actual.AsReadOnly());
        var maximo = puntos.Select(p => p.EnergiaKWh).DefaultIfEmpty(0m).Max();
        return new(puntos.AsReadOnly(), segmentos.AsReadOnly(), Math.Max(1m, decimal.Ceiling(maximo)));
    }
}
