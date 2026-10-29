namespace SolarUY.Application.Exceptions;

public sealed class LecturaDuplicadaException : Exception
{
    public LecturaDuplicadaException(Exception innerException)
        : base("Ya existe una medición para el panel y el instante UTC; el lote no se guardó.", innerException) { }
}
