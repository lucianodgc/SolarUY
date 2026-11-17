using SolarUY.Domain.Enums;

namespace SolarUY.Application.Paneles;

public class CrearPanelDto
{
    public Guid NodoId { get; set; }
    public string NumeroSerie { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public decimal PotenciaNominalW { get; set; }
    public DateTime FechaInstalacion { get; set; }
    public EstadoPanel Estado { get; set; }
}
