using SolarUY.Domain.Entities;

namespace SolarUY.Domain.Monitoreo;

public enum DisponibilidadMonitoreo { SinDatos, FueraDeVigencia, IntervaloIncompatible, NodoAnterior, Vigente, NodoHistoricoDesconocido }

public sealed record GeneracionPanel(Guid PanelId, DateTime? FechaHoraUtc, decimal? PotenciaW,
    DisponibilidadMonitoreo Disponibilidad);

public sealed record ResumenGeneracion(decimal? PotenciaW, int PanelesConDatos, int TotalPaneles,
    DateTime? FechaDesdeUtc, DateTime? FechaHastaUtc)
{
    public bool Completo => TotalPaneles > 0 && PanelesConDatos == TotalPaneles;
}

public sealed record ResultadoGeneracionActual(IReadOnlyList<GeneracionPanel> Paneles,
    IReadOnlyDictionary<Guid, ResumenGeneracion> Nodos, ResumenGeneracion Parque, DateTime? InstanteUtc);

public sealed class PoliticaMonitoreo
{
    public PoliticaMonitoreo(int intervaloSegundos, int intervalosVigencia)
    {
        if (intervaloSegundos <= 0) throw new ArgumentOutOfRangeException(nameof(intervaloSegundos));
        if (intervalosVigencia <= 0) throw new ArgumentOutOfRangeException(nameof(intervalosVigencia));
        Intervalo = TimeSpan.FromSeconds(intervaloSegundos);
        Vigencia = TimeSpan.FromTicks(checked(Intervalo.Ticks * intervalosVigencia));
    }

    public TimeSpan Intervalo { get; }
    public TimeSpan Vigencia { get; }
    public DateTime ObtenerIntervalo(DateTime instanteUtc)
        => new(instanteUtc.Ticks - instanteUtc.Ticks % Intervalo.Ticks, DateTimeKind.Utc);
}

public sealed class GeneracionActualService
{
    public ResultadoGeneracionActual Calcular(IReadOnlyList<Nodo> nodos, IReadOnlyList<Panel> paneles,
        IReadOnlyList<LecturaPanel> lecturas, DateTime referenciaUtc, PoliticaMonitoreo politica)
    {
        if (referenciaUtc == default || referenciaUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La evaluación requiere una referencia UTC.", nameof(referenciaUtc));
        ArgumentNullException.ThrowIfNull(politica);
        var unicos = paneles.DistinctBy(p => p.Id).ToArray();
        var ultimas = lecturas.Where(l => l.FechaHora <= referenciaUtc).GroupBy(l => l.PanelId)
            .ToDictionary(g => g.Key, g => g.MaxBy(l => l.FechaHora)!);
        var candidatas = unicos.Where(p => ultimas.TryGetValue(p.Id, out var l)
            && l.NodoId == p.NodoId && referenciaUtc - l.FechaHora < politica.Vigencia)
            .Select(p => ultimas[p.Id]).ToArray();
        DateTime? instante = candidatas.Length == 0 ? null
            : politica.ObtenerIntervalo(candidatas.Max(l => l.FechaHora));
        var resultado = unicos.Select(p => Evaluar(p, ultimas.GetValueOrDefault(p.Id), referenciaUtc, instante, politica)).ToArray();
        var porId = resultado.ToDictionary(p => p.PanelId);
        var porNodo = nodos.DistinctBy(n => n.Id).ToDictionary(n => n.Id,
            n => Resumir(unicos.Where(p => p.NodoId == n.Id).Select(p => porId[p.Id]).ToArray()));
        return new ResultadoGeneracionActual(resultado, porNodo, Resumir(resultado), instante);
    }

    private static GeneracionPanel Evaluar(Panel panel, LecturaPanel? lectura, DateTime referencia,
        DateTime? intervalo, PoliticaMonitoreo politica)
    {
        if (lectura is null) return new(panel.Id, null, null, DisponibilidadMonitoreo.SinDatos);
        var disponibilidad = lectura.NodoId is null ? DisponibilidadMonitoreo.NodoHistoricoDesconocido
            : lectura.NodoId != panel.NodoId ? DisponibilidadMonitoreo.NodoAnterior
            : referencia - lectura.FechaHora >= politica.Vigencia ? DisponibilidadMonitoreo.FueraDeVigencia
            : politica.ObtenerIntervalo(lectura.FechaHora) != intervalo ? DisponibilidadMonitoreo.IntervaloIncompatible
            : DisponibilidadMonitoreo.Vigente;
        return new(panel.Id, lectura.FechaHora,
            disponibilidad == DisponibilidadMonitoreo.Vigente ? lectura.PotenciaGeneradaW : null, disponibilidad);
    }

    private static ResumenGeneracion Resumir(IReadOnlyList<GeneracionPanel> paneles)
    {
        var vigentes = paneles.Where(p => p.PotenciaW.HasValue).ToArray();
        return new(vigentes.Length == 0 ? null : vigentes.Sum(p => p.PotenciaW!.Value),
            vigentes.Length, paneles.Count, vigentes.Length == 0 ? null : vigentes.Min(p => p.FechaHoraUtc),
            vigentes.Length == 0 ? null : vigentes.Max(p => p.FechaHoraUtc));
    }
}
