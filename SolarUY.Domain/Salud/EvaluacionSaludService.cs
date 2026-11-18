using SolarUY.Domain.Enums;

namespace SolarUY.Domain.Salud;

public sealed record EvidenciaSalud(DateTime FechaUtc, decimal PotenciaW, EstadoSaludLectura Senal,
    bool ProcedenciaValida = true, bool? GeneracionEsperada = null);
public sealed record ResultadoSalud(EstadoSaludPanel? Estado, bool Suficiente, string Motivo, DateTime? EvidenciaUtc);

public sealed class EvaluacionSaludService
{
    public ResultadoSalud Evaluar(EvidenciaSalud? evidencia, decimal nominalW, DateTime primeraObservacionUtc,
        DateTime referenciaUtc, PoliticaSalud politica)
    {
        if (referenciaUtc.Kind != DateTimeKind.Utc || primeraObservacionUtc.Kind != DateTimeKind.Utc
            || primeraObservacionUtc > referenciaUtc) throw new ArgumentException("Se requieren referencias UTC ordenadas.");
        ArgumentNullException.ThrowIfNull(politica);
        ArgumentOutOfRangeException.ThrowIfNegative(nominalW);
        if (evidencia is { } e && (e.FechaUtc.Kind != DateTimeKind.Utc || e.FechaUtc > referenciaUtc))
            return new(null, false, "La fecha de evidencia no es válida.", e.FechaUtc);
        var valida = evidencia is { ProcedenciaValida: true };
        var ultima = valida ? evidencia!.FechaUtc : primeraObservacionUtc;
        if (referenciaUtc - ultima >= politica.PlazoSinDatos)
            return new(EstadoSaludPanel.Desconectado, true, "Se superó el plazo sin lecturas válidas.", valida ? ultima : null);
        if (!valida) return new(null, false, "Sin lectura del nodo actual con procedencia verificable.", null);
        if (evidencia!.Senal == EstadoSaludLectura.Fallado)
            return new(EstadoSaludPanel.Falla, true, "Falla informada por el inversor.", ultima);
        if (evidencia.Senal != EstadoSaludLectura.Sano || evidencia.PotenciaW < 0)
            return new(null, false, "La evidencia no permite clasificar la salud.", ultima);
        return EvaluarPotencia(evidencia, nominalW, ultima, politica);
    }

    private static ResultadoSalud EvaluarPotencia(EvidenciaSalud evidencia, decimal nominalW, DateTime ultima, PoliticaSalud politica)
    {
        if (politica.EvaluarPotenciaBaja && evidencia.GeneracionEsperada == true && nominalW > 0)
        {
            var ratio = evidencia.PotenciaW / nominalW / politica.FactorEsperado;
            if (ratio < politica.UmbralFalla) return new(EstadoSaludPanel.Falla, true, "Potencia inferior al umbral de falla.", ultima);
            if (ratio < politica.UmbralDegradado) return new(EstadoSaludPanel.Degradado, true, "Potencia inferior al umbral de degradación.", ultima);
        }
        return new(EstadoSaludPanel.Normal, true, politica.EvaluarPotenciaBaja
            ? "Señal sana; potencia evaluada solo con generación esperada demostrada."
            : "Señal sana; comprobación de potencia esperada deshabilitada.", ultima);
    }
}

public sealed record SaludNodoResultado(EstadoSaludPanel? Estado, int PanelesEvaluados, int TotalPaneles)
{
    public bool Completo => TotalPaneles > 0 && PanelesEvaluados == TotalPaneles;
}

public sealed class SaludNodoService
{
    public SaludNodoResultado Agregar(IReadOnlyList<ResultadoSalud> paneles)
    {
        var suficientes = paneles.Where(p => p.Suficiente && p.Estado.HasValue).ToArray();
        EstadoSaludPanel? estado = null;
        foreach (var candidato in new[] { EstadoSaludPanel.Falla, EstadoSaludPanel.Desconectado, EstadoSaludPanel.Degradado, EstadoSaludPanel.Normal })
            if (suficientes.Any(p => p.Estado == candidato)) { estado = candidato; break; }
        return new(estado, suficientes.Length, paneles.Count);
    }
}
