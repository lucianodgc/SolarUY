using SolarUY.Domain.Enums;

namespace SolarUY.Application.Nodos;

public sealed record NodoDto(Guid Id, Guid ParqueId, string Nombre, string UbicacionEnParque,
    int? CantidadPanelesPrevista, InversorNodoDto Inversor);

public sealed record InversorNodoDto(Guid Id, string Marca, string Modelo, decimal PotenciaNominalKW,
    EstadoInversor Estado, bool VolcadoRedActivo);
