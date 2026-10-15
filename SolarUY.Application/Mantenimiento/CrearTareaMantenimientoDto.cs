namespace SolarUY.Application.Mantenimiento;

public sealed class CrearTareaMantenimientoDto
{
    public Guid PanelId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTime FechaProgramada { get; set; }
    public string ResponsableId { get; set; } = string.Empty;
}
