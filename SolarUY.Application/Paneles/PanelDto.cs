using SolarUY.Domain.Enums;

namespace SolarUY.Application.Paneles;

public sealed record PanelDto(Guid Id, Guid NodoId, string Nodo, string Parque, string NumeroSerie, string Marca,
    string Modelo, decimal PotenciaNominalW, DateTime FechaInstalacion, EstadoPanel Estado);
