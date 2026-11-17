using SolarUY.Domain.Entities;

namespace SolarUY.Domain.Salud;

public sealed class SaludPanel
{
    private SaludPanel() { }
    public SaludPanel(Panel panel, DateTime primeraObservacionUtc)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ValidarUtc(primeraObservacionUtc);
        Id = Guid.NewGuid(); Panel = panel; PanelId = panel.Id; NodoEvaluadoId = panel.NodoId;
        PrimeraObservacionUtc = primeraObservacionUtc; EvaluadoUtc = primeraObservacionUtc;
    }
    public Guid Id { get; private set; }
    public Guid PanelId { get; private set; }
    public Panel Panel { get; private set; } = null!;
    public Guid NodoEvaluadoId { get; private set; }
    public EstadoSaludPanel? Estado { get; private set; }
    public bool Suficiente { get; private set; }
    public string Motivo { get; private set; } = "Pendiente de evidencia.";
    public DateTime PrimeraObservacionUtc { get; private set; }
    public DateTime EvaluadoUtc { get; private set; }
    public DateTime? EvidenciaUtc { get; private set; }
    public long Version { get; private set; }

    public CambioEstadoSaludPanel? Aplicar(ResultadoSalud resultado, DateTime referenciaUtc, Guid nodoActualId)
    {
        ValidarUtc(referenciaUtc);
        if (referenciaUtc < EvaluadoUtc) return null;
        if (resultado.Suficiente != resultado.Estado.HasValue) throw new ArgumentException("Clasificación inconsistente.", nameof(resultado));
        if (resultado.Estado.HasValue && !Enum.IsDefined(resultado.Estado.Value)) throw new ArgumentOutOfRangeException(nameof(resultado));
        if (string.IsNullOrWhiteSpace(resultado.Motivo) || resultado.Motivo.Length > 500) throw new ArgumentException("Motivo inválido.", nameof(resultado));
        if (resultado.EvidenciaUtc is { } evidencia)
        {
            ValidarUtc(evidencia);
            if (evidencia > referenciaUtc) throw new ArgumentException("La evidencia no puede ser futura.", nameof(resultado));
            if (NodoEvaluadoId == nodoActualId && EvidenciaUtc.HasValue && evidencia < EvidenciaUtc.Value) return null;
        }
        if (nodoActualId == Guid.Empty) throw new ArgumentException("Se requiere el nodo actual.", nameof(nodoActualId));
        var mismoNodo = NodoEvaluadoId == nodoActualId;
        if (!mismoNodo) PrimeraObservacionUtc = referenciaUtc;
        NodoEvaluadoId = nodoActualId; EvaluadoUtc = referenciaUtc; Suficiente = resultado.Suficiente;
        Motivo = resultado.Motivo;
        EvidenciaUtc = mismoNodo ? resultado.EvidenciaUtc ?? EvidenciaUtc : resultado.EvidenciaUtc;
        if (!resultado.Suficiente || resultado.Estado == Estado) return null;
        var anterior = Estado; Estado = resultado.Estado; Version++;
        return new CambioEstadoSaludPanel(Panel, anterior, Estado!.Value, referenciaUtc, Version);
    }

    internal static void ValidarUtc(DateTime fecha)
    {
        if (fecha == default || fecha.Kind != DateTimeKind.Utc) throw new ArgumentException("Se requiere un instante UTC.");
    }
}

public sealed class CambioEstadoSaludPanel
{
    private CambioEstadoSaludPanel() { }
    internal CambioEstadoSaludPanel(Panel panel, EstadoSaludPanel? anterior, EstadoSaludPanel nuevo, DateTime fechaUtc, long version)
    {
        Id = Guid.NewGuid(); Panel = panel; PanelId = panel.Id; EstadoAnterior = anterior;
        EstadoNuevo = nuevo; FechaUtc = fechaUtc; Version = version;
    }
    public Guid Id { get; private set; }
    public Guid PanelId { get; private set; }
    public Panel Panel { get; private set; } = null!;
    public EstadoSaludPanel? EstadoAnterior { get; private set; }
    public EstadoSaludPanel EstadoNuevo { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public long Version { get; private set; }
}

public sealed class AlertaPanel
{
    private AlertaPanel() { }
    public AlertaPanel(Panel panel, EstadoSaludPanel estado, DateTime deteccionUtc)
    {
        ArgumentNullException.ThrowIfNull(panel); SaludPanel.ValidarUtc(deteccionUtc);
        if (!Enum.IsDefined(estado) || estado == EstadoSaludPanel.Normal) throw new ArgumentOutOfRangeException(nameof(estado));
        Id = Guid.NewGuid(); Panel = panel; PanelId = panel.Id; Condicion = estado; DeteccionUtc = deteccionUtc;
    }
    public Guid Id { get; private set; }
    public Guid PanelId { get; private set; }
    public Panel Panel { get; private set; } = null!;
    public EstadoSaludPanel Condicion { get; private set; }
    public DateTime DeteccionUtc { get; private set; }
    public DateTime? CierreUtc { get; private set; }
    public void Cerrar(DateTime fechaUtc)
    {
        SaludPanel.ValidarUtc(fechaUtc);
        if (fechaUtc < DeteccionUtc) throw new ArgumentException("El cierre no puede preceder la detección.", nameof(fechaUtc));
        CierreUtc ??= fechaUtc;
    }
}
