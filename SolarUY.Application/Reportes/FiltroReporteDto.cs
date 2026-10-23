using SolarUY.Application.HistoricoGeneracion;

namespace SolarUY.Application.Reportes;

public sealed record FiltroReporteDto(Guid ParqueId, Guid? NodoId, PeriodoHistorico Periodo,
    DateTime InicioLocal, DateTime? FinLocal = null);
