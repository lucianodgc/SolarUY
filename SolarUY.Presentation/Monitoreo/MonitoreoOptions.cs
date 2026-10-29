namespace SolarUY.Presentation.Monitoreo;

public sealed class MonitoreoOptions
{
    public int IntervalosVigencia { get; set; } = 2;
    public int MaximoPuntosGrafico { get; set; } = 120;
    public int ReevaluacionSegundos { get; set; } = 5;
}
