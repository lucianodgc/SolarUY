namespace SolarUY.Application.Exceptions;

public sealed class ErrorComunicacionInversorException : Exception
{
    public ErrorComunicacionInversorException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
