using SolarUY.Application.Abstractions;

namespace SolarUY.Application.Inversores;

public sealed record EstadoInversorDto(
    string NodoId, IReadOnlyList<PanelInversorEstadoDto> Paneles,
    decimal PotenciaGeneradaW, bool VolcadoRedActivo, bool PuedeOperar, DateTime FechaConsultaUtc);
