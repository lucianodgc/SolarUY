namespace SolarUY.Application.Exceptions;

public sealed class OrdenInversorRechazadaException : Exception
{
    public OrdenInversorRechazadaException() : base("El inversor rechazó la solicitud.") { }
}
