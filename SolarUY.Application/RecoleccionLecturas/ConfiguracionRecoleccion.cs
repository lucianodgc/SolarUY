namespace SolarUY.Application.RecoleccionLecturas;

public sealed class ConfiguracionRecoleccion
{
    public ConfiguracionRecoleccion(int intervaloSegundos)
    {
        if (intervaloSegundos <= 0) throw new ArgumentOutOfRangeException(nameof(intervaloSegundos));
        Intervalo = TimeSpan.FromSeconds(intervaloSegundos);
    }

    public TimeSpan Intervalo { get; }
}
