using SolarUY.Domain.Enums;

namespace SolarUY.Domain.Entities;

public sealed class LecturaPanel
{
    private LecturaPanel() { }

    public LecturaPanel(Panel panel, DateTime fechaHora, decimal potenciaGeneradaW, EstadoSaludLectura estadoSalud)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (fechaHora == default || fechaHora.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha de lectura debe ser un instante UTC válido.", nameof(fechaHora));
        if (potenciaGeneradaW < 0 || potenciaGeneradaW > 999999999999.999999m
            || decimal.Round(potenciaGeneradaW, 6) != potenciaGeneradaW)
            throw new ArgumentOutOfRangeException(nameof(potenciaGeneradaW), "La potencia debe ser no negativa y representable en decimal(18,6).");
        if (!Enum.IsDefined(estadoSalud)) throw new ArgumentOutOfRangeException(nameof(estadoSalud));
        Id = Guid.NewGuid();
        Panel = panel;
        PanelId = panel.Id;
        FechaHora = fechaHora;
        PotenciaGeneradaW = potenciaGeneradaW;
        EstadoSalud = estadoSalud;
    }

    public Guid Id { get; private set; }
    public Guid PanelId { get; private set; }
    public Panel Panel { get; private set; } = null!;
    public DateTime FechaHora { get; private set; }
    public decimal PotenciaGeneradaW { get; private set; }
    public EstadoSaludLectura EstadoSalud { get; private set; }
}
