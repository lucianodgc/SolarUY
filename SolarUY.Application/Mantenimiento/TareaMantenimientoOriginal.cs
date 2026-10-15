using SolarUY.Domain.Enums;

namespace SolarUY.Application.Mantenimiento;

public sealed record TareaMantenimientoOriginal(
    EstadoTareaMantenimiento Estado, string ResponsableId, string Tipo,
    string Descripcion, DateTime FechaProgramada, DateTime? FechaRealizacion);
