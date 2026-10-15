namespace SolarUY.Application.Exceptions;

public sealed class MantenimientoConflictoException : MantenimientoException
{
    public MantenimientoConflictoException()
        : base("La tarea cambió desde que se consultó. Actualiza los datos e intenta nuevamente.") { }
}
