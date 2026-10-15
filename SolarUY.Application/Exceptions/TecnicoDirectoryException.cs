namespace SolarUY.Application.Exceptions;

public sealed class TecnicoDirectoryException : MantenimientoException
{
    public TecnicoDirectoryException(string message) : base(message) { }
}
