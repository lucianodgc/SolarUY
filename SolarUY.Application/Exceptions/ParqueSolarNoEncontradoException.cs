namespace SolarUY.Application.Exceptions;

public sealed class ParqueSolarNoEncontradoException : Exception
{
    public ParqueSolarNoEncontradoException()
        : base("El parque solar indicado no existe.") { }
}
