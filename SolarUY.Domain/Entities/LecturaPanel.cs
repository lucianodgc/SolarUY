using SolarUY.Domain.Enums;

namespace SolarUY.Domain.Entities;

public sealed class LecturaPanel
{
    private LecturaPanel() { }

    public LecturaPanel(Panel panel, DateTime fechaHora, decimal potenciaGeneradaW, EstadoSaludLectura estadoSalud)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (panel.NodoId == Guid.Empty)
            throw new ArgumentException("El panel debe pertenecer a un nodo existente.", nameof(panel));
        if (fechaHora.Ticks < 10 || fechaHora.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha de lectura debe ser un instante UTC válido.", nameof(fechaHora));
        if (potenciaGeneradaW < 0 || potenciaGeneradaW > 999999999999.999999m
            || decimal.Round(potenciaGeneradaW, 6) != potenciaGeneradaW)
            throw new ArgumentOutOfRangeException(nameof(potenciaGeneradaW), "La potencia debe ser no negativa y representable en decimal(18,6).");
        if (!Enum.IsDefined(estadoSalud)) throw new ArgumentOutOfRangeException(nameof(estadoSalud));
        Id = Guid.NewGuid();
        Panel = panel;
        PanelId = panel.Id;
        Nodo = panel.Nodo;
        NodoId = panel.NodoId;
        FechaHora = new DateTime(fechaHora.Ticks - fechaHora.Ticks % 10, DateTimeKind.Utc);
        PotenciaGeneradaW = potenciaGeneradaW;
        EstadoSalud = estadoSalud;
    }

    public Guid Id { get; private set; }
    public Guid PanelId { get; private set; }
    public Panel Panel { get; private set; } = null!;
    // NULL identifica exclusivamente lecturas anteriores sin procedencia demostrable.
    // El constructor público siempre captura el nodo de una medición nueva.
    public Guid? NodoId { get; private set; }
    public Nodo? Nodo { get; private set; }
    public DateTime FechaHora { get; private set; }
    public decimal PotenciaGeneradaW { get; private set; }
    public EstadoSaludLectura EstadoSalud { get; private set; }
}
