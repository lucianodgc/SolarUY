using SolarUY.Domain.Enums;

namespace SolarUY.Application.Nodos;

public sealed class CrearNodoDto
{
    public string Nombre { get; set; } = string.Empty;
    public string UbicacionEnParque { get; set; } = string.Empty;
    public int? CantidadPanelesPrevista { get; set; }
    public string MarcaInversor { get; set; } = string.Empty;
    public string ModeloInversor { get; set; } = string.Empty;
    public decimal PotenciaNominalKW { get; set; }
    public EstadoInversor EstadoInversor { get; set; }
    public bool VolcadoRedActivo { get; set; }
}
