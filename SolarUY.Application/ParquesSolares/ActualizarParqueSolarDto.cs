namespace SolarUY.Application.ParquesSolares;

public sealed class ActualizarParqueSolarDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Ubicacion { get; set; } = string.Empty;
    public decimal? CapacidadEstimadaMW { get; set; }
}
