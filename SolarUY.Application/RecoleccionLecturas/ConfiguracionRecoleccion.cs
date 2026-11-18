namespace SolarUY.Application.RecoleccionLecturas;

public sealed class ConfiguracionRecoleccion
{
    public ConfiguracionRecoleccion(int intervaloSegundos)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervaloSegundos);
        Intervalo = TimeSpan.FromSeconds(intervaloSegundos);
    }

    public TimeSpan Intervalo { get; }
}
