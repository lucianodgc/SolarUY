using SolarUY.Application.Abstractions;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.Mantenimiento;

namespace SolarUY.Application.Reportes;

public sealed class ReporteHistoricoService : IReporteHistoricoService
{
    private readonly IHistoricoGeneracionService _historico;
    private readonly ITareaMantenimientoRepository _mantenimientos;
    private readonly IUsuarioActual _usuarioActual;

    public ReporteHistoricoService(IHistoricoGeneracionService historico,
        ITareaMantenimientoRepository mantenimientos, IUsuarioActual usuarioActual)
    {
        _historico = historico;
        _mantenimientos = mantenimientos;
        _usuarioActual = usuarioActual;
    }

    public async Task<IReadOnlyList<OpcionHistoricoDto>> ListarOpcionesAsync(CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken);
        return (await _historico.ListarOpcionesAsync(cancellationToken))
            .Where(opcion => opcion.Tipo is TipoObjetivoHistorico.Parque or TipoObjetivoHistorico.Nodo).ToArray();
    }

    public async Task<ResultadoReporteDto> ConsultarAsync(FiltroReporteDto filtro, CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken);
        ArgumentNullException.ThrowIfNull(filtro);
        if (filtro.ParqueId == Guid.Empty || filtro.NodoId == Guid.Empty || !Enum.IsDefined(filtro.Periodo))
            throw new ArgumentException("Seleccioná un parque, nodo y período válidos.");
        var opciones = await _historico.ListarOpcionesAsync(cancellationToken);
        var parque = opciones.SingleOrDefault(o => o.Tipo == TipoObjetivoHistorico.Parque && o.Id == filtro.ParqueId)
            ?? throw new KeyNotFoundException("El parque seleccionado no existe.");
        OpcionHistoricoDto? nodo = null;
        if (filtro.NodoId is { } nodoId)
        {
            nodo = opciones.SingleOrDefault(o => o.Tipo == TipoObjetivoHistorico.Nodo && o.Id == nodoId)
                ?? throw new KeyNotFoundException("El nodo seleccionado no existe.");
            if (nodo.ParqueId != filtro.ParqueId)
                throw new ArgumentException("El nodo seleccionado no pertenece al parque.");
        }
        var generacion = await _historico.ConsultarAsync(new(nodo is null ? TipoObjetivoHistorico.Parque : TipoObjetivoHistorico.Nodo,
            nodo?.Id ?? parque.Id, filtro.Periodo, filtro.InicioLocal, filtro.FinLocal), cancellationToken);
        var consulta = generacion.Consulta;
        var mantenimientos = await _mantenimientos.ListarRealizadosParaReporteAsync(
            new(filtro.ParqueId, filtro.NodoId, consulta.InicioUtc, consulta.FinUtc), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(filtro, parque.Etiqueta, nodo?.Etiqueta, generacion, mantenimientos);
    }

    public async Task<ArchivoReporteCsvDto> ExportarCsvAsync(ResultadoReporteDto reporte,
        CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken);
        ArgumentNullException.ThrowIfNull(reporte);
        var consulta = reporte.Generacion.Consulta;
        var tipo = reporte.Filtro.NodoId.HasValue ? TipoObjetivoHistorico.Nodo : TipoObjetivoHistorico.Parque;
        if (consulta.Tipo != tipo || consulta.ObjetivoId != (reporte.Filtro.NodoId ?? reporte.Filtro.ParqueId)
            || consulta.InicioUtc >= consulta.FinUtc || string.IsNullOrWhiteSpace(reporte.Parque))
            throw new ArgumentException("El resultado no corresponde a un reporte válido.");
        return ReporteCsv.Crear(reporte, cancellationToken);
    }

    private async Task AutorizarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(rol => rol is "Administrador" or "Tecnico" or "Consulta"))
            throw new UnauthorizedAccessException("No tenés permisos para consultar o exportar reportes.");
    }
}
