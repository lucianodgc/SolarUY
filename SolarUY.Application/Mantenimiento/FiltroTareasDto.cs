using SolarUY.Domain.Enums;

namespace SolarUY.Application.Mantenimiento;

public sealed class FiltroTareasDto
{
    public EstadoTareaMantenimiento? Estado { get; set; }
    public bool SoloVencidas { get; set; }
    public string? ResponsableId { get; set; }
    public Guid? PanelId { get; set; }
}
