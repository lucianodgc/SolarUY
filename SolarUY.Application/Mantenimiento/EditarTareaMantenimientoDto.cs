namespace SolarUY.Application.Mantenimiento;

public sealed class EditarTareaMantenimientoDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTime FechaProgramada { get; set; }
    public string ResponsableId { get; set; } = string.Empty;
    public TareaMantenimientoOriginal? Original { get; set; }
}
