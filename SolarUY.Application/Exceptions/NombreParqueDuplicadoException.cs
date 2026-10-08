namespace SolarUY.Application.Exceptions;

public sealed class NombreParqueDuplicadoException : Exception
{
    public NombreParqueDuplicadoException() : base("Ya existe un parque solar con ese nombre.") { }
    public NombreParqueDuplicadoException(Exception innerException)
        : base("Ya existe un parque solar con ese nombre.", innerException) { }
}
