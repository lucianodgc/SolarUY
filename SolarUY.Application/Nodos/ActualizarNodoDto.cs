namespace SolarUY.Application.Nodos;

public sealed class ActualizarNodoDto
{
    public string Nombre { get; set; } = string.Empty;
    public string UbicacionEnParque { get; set; } = string.Empty;
    public int? CantidadPanelesPrevista { get; set; }
}
