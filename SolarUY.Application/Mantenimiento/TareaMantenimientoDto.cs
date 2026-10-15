using SolarUY.Domain.Enums;

namespace SolarUY.Application.Mantenimiento;

public sealed record TareaMantenimientoDto(
    Guid Id, Guid PanelId, string NumeroSerie, string Tipo, string Descripcion,
    DateTime FechaProgramada, DateTime? FechaRealizacion, string ResponsableId,
    EstadoTareaMantenimiento Estado, bool Vencida,
    bool PuedeIniciar, bool PuedeCompletar, bool PuedeCancelar, bool PuedeEditar);
