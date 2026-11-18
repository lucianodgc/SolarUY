namespace SolarUY.Presentation.Monitoreo;

public sealed class MonitoreoOptions
{
    public int IntervalosVigencia { get; set; } = 2;
    public int MaximoPuntosGrafico { get; set; } = 120;
    public int ReevaluacionSegundos { get; set; } = 5;

    internal bool EsValida(TimeSpan intervaloRecoleccion)
        => IntervalosVigencia > 0 && MaximoPuntosGrafico > 0
            && IntervalosVigencia <= long.MaxValue / intervaloRecoleccion.Ticks
            && ReevaluacionSegundos is > 0 and <= 4_294_967;
}
