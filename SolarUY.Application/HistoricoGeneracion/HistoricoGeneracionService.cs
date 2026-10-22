using SolarUY.Application.Abstractions;

namespace SolarUY.Application.HistoricoGeneracion;

public sealed class HistoricoGeneracionService : IHistoricoGeneracionService
{
    private readonly IHistoricoGeneracionRepository _repository;
    private readonly IUsuarioActual _usuarioActual;
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");

    public HistoricoGeneracionService(IHistoricoGeneracionRepository repository, IUsuarioActual usuarioActual)
    {
        _repository = repository;
        _usuarioActual = usuarioActual;
    }

    public async Task<IReadOnlyList<OpcionHistoricoDto>> ListarOpcionesAsync(CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken);
        return await _repository.ListarOpcionesAsync(cancellationToken);
    }

    public async Task<ResultadoHistoricoDto> ConsultarAsync(FiltroHistoricoDto filtro, CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken);
        return await ConsultarValidadoAsync(filtro, cancellationToken);
    }

    public async Task<ComparacionHistoricoDto> CompararAsync(FiltroHistoricoDto primero, FiltroHistoricoDto segundo,
        CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken);
        ArgumentNullException.ThrowIfNull(primero);
        ArgumentNullException.ThrowIfNull(segundo);
        if (primero.Tipo != segundo.Tipo || primero.ObjetivoId != segundo.ObjetivoId)
            throw new ArgumentException("La comparación debe consultar el mismo parque, nodo o panel.");
        // Validar ambos antes de acceder a persistencia.
        CrearConsulta(primero);
        CrearConsulta(segundo);
        var resultadoPrimero = await ConsultarValidadoAsync(primero, cancellationToken);
        var resultadoSegundo = await ConsultarValidadoAsync(segundo, cancellationToken);
        return new ComparacionHistoricoDto(resultadoPrimero, resultadoSegundo);
    }

    private async Task AutorizarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(rol => rol is "Administrador" or "Tecnico" or "Consulta"))
            throw new UnauthorizedAccessException("No tienes permisos para consultar el histórico.");
    }

    private async Task<ResultadoHistoricoDto> ConsultarValidadoAsync(FiltroHistoricoDto filtro, CancellationToken cancellationToken)
    {
        var consulta = CrearConsulta(filtro);
        if (!await _repository.ExisteObjetivoAsync(consulta.Tipo, consulta.ObjetivoId, cancellationToken))
            throw new KeyNotFoundException("El parque, nodo o panel seleccionado no existe.");
        return await _repository.ConsultarAsync(consulta, cancellationToken);
    }

    private static ConsultaHistoricoDto CrearConsulta(FiltroHistoricoDto filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        if (!Enum.IsDefined(filtro.Tipo) || !Enum.IsDefined(filtro.Periodo) || filtro.ObjetivoId == Guid.Empty)
            throw new ArgumentException("Selecciona un objetivo y un período válidos.");
        if (filtro.InicioLocal == default || filtro.InicioLocal.Kind != DateTimeKind.Unspecified
            || (filtro.FinLocal.HasValue && filtro.FinLocal.Value.Kind != DateTimeKind.Unspecified))
            throw new ArgumentException("Las fechas de consulta deben expresarse en hora local de Uruguay.");
        try
        {
            var (inicio, fin) = ObtenerLimites(filtro);
            if (inicio >= fin) throw new ArgumentException("La fecha inicial debe ser anterior al fin exclusivo.");
            var inicioUtc = ConvertirAUtc(inicio);
            var finUtc = ConvertirAUtc(fin);
            var intervalos = CrearIntervalos(inicioUtc, finUtc, filtro.Periodo);
            return new ConsultaHistoricoDto(filtro.Tipo, filtro.ObjetivoId, inicioUtc, finUtc, intervalos);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentException("Las fechas exceden el rango admitido para el período seleccionado.");
        }
    }

    private static (DateTime Inicio, DateTime Fin) ObtenerLimites(FiltroHistoricoDto filtro)
    {
        var fecha = filtro.InicioLocal;
        return filtro.Periodo switch
        {
            PeriodoHistorico.Diario => (fecha.Date, fecha.Date.AddDays(1)),
            PeriodoHistorico.Mensual => (new(fecha.Year, fecha.Month, 1), new DateTime(fecha.Year, fecha.Month, 1).AddMonths(1)),
            PeriodoHistorico.Anual => (new(fecha.Year, 1, 1), new DateTime(fecha.Year, 1, 1).AddYears(1)),
            _ => (fecha, filtro.FinLocal ?? throw new ArgumentException("Indica el fin exclusivo del rango."))
        };
    }

    private static DateTime ConvertirAUtc(DateTime fecha)
    {
        if (Zona.IsInvalidTime(fecha) || Zona.IsAmbiguousTime(fecha))
            throw new ArgumentException("La fecha local es inexistente o ambigua por un cambio de horario de Uruguay.");
        return TimeZoneInfo.ConvertTimeToUtc(fecha, Zona);
    }

    private static IReadOnlyList<IntervaloHistoricoDto> CrearIntervalos(DateTime inicioUtc, DateTime finUtc, PeriodoHistorico periodo)
    {
        var intervalos = new List<IntervaloHistoricoDto>();
        var inicio = inicioUtc;
        while (inicio < finUtc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(inicio, Zona);
            var siguiente = periodo switch
            {
                PeriodoHistorico.Diario => inicio.AddHours(1),
                PeriodoHistorico.Anual => ConvertirAUtc(new DateTime(local.Year, local.Month, 1).AddMonths(1)),
                _ => ConvertirAUtc(local.Date.AddDays(1))
            };
            var fin = siguiente < finUtc ? siguiente : finUtc;
            var etiqueta = periodo switch
            {
                PeriodoHistorico.Diario => new DateTimeOffset(local, Zona.GetUtcOffset(inicio)).ToString("yyyy-MM-dd HH:mm zzz"),
                PeriodoHistorico.Anual => local.ToString("yyyy-MM"),
                _ => local.ToString("yyyy-MM-dd")
            };
            intervalos.Add(new IntervaloHistoricoDto(inicio, fin, etiqueta));
            inicio = fin;
        }
        return intervalos;
    }
}
