using SolarUY.Application.Abstractions;

namespace SolarUY.Application.Inversores;

public sealed record EstadoInversorDto(
    string NodoId, string NodoNombre, string ParqueNombre, IReadOnlyList<PanelInversorEstadoDto> Paneles,
    decimal PotenciaGeneradaW, bool VolcadoRedActivo, bool InversorEncendido,
    bool PuedeOperar, DateTime FechaConsultaUtc);
