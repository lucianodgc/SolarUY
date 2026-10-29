using SolarUY.Application.Abstractions;
using SolarUY.Domain.Monitoreo;

namespace SolarUY.Application.Monitoreo;

public sealed record PanelMonitoreoDto(Guid Id, string NumeroSerie, GeneracionPanel Generacion);
public sealed record NodoMonitoreoDto(Guid Id, string Nombre, ResumenGeneracion Generacion,
    IReadOnlyList<PanelMonitoreoDto> Paneles);
public sealed record EstadoMonitoreoDto(Guid ParqueId, string Nombre, DateTime EvaluadoUtc,
    DateTime? InstanteUtc, ResumenGeneracion Generacion, IReadOnlyList<NodoMonitoreoDto> Nodos);
public sealed record PuntoGeneracionDto(DateTime FechaHoraUtc, decimal PotenciaW, bool Parcial);

public interface IMonitoreoService
{
    Task<IReadOnlyList<ParqueMonitoreoDto>> ListarParquesAsync(CancellationToken cancellationToken = default);
    Task<EstadoMonitoreoDto> ConsultarAsync(Guid parqueId, CancellationToken cancellationToken = default);
    Task<EstadoMonitoreoDto> ReevaluarAsync(Guid parqueId, CancellationToken cancellationToken = default);
}

public sealed class MonitoreoService : IMonitoreoService
{
    private readonly IMonitoreoRepository _repository;
    private readonly IUsuarioActual _usuarioActual;
    private readonly GeneracionActualService _calculadora;
    private readonly PoliticaMonitoreo _politica;
    private readonly TimeProvider _reloj;
    private DatosMonitoreoDto? _datos;
    private long _versionConsulta;

    public MonitoreoService(IMonitoreoRepository repository, IUsuarioActual usuarioActual,
        GeneracionActualService calculadora, PoliticaMonitoreo politica, TimeProvider reloj)
    {
        _repository = repository; _usuarioActual = usuarioActual; _calculadora = calculadora;
        _politica = politica; _reloj = reloj;
    }

    public async Task<IReadOnlyList<ParqueMonitoreoDto>> ListarParquesAsync(CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken);
        return await _repository.ListarParquesAsync(cancellationToken);
    }

    public async Task<EstadoMonitoreoDto> ConsultarAsync(Guid parqueId, CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken);
        var version = Interlocked.Increment(ref _versionConsulta);
        var referencia = _reloj.GetUtcNow().UtcDateTime;
        var datos = await _repository.ObtenerDatosAsync(parqueId, referencia, cancellationToken)
            ?? throw new KeyNotFoundException("El parque seleccionado no existe.");
        if (version == Interlocked.Read(ref _versionConsulta)) _datos = datos;
        return CrearEstado(datos, _reloj.GetUtcNow().UtcDateTime);
    }

    public async Task<EstadoMonitoreoDto> ReevaluarAsync(Guid parqueId, CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken);
        var datos = _datos;
        return datos?.Parque.Id == parqueId ? CrearEstado(datos, _reloj.GetUtcNow().UtcDateTime)
            : await ConsultarAsync(parqueId, cancellationToken);
    }

    private EstadoMonitoreoDto CrearEstado(DatosMonitoreoDto datos, DateTime referencia)
    {
        var calculo = _calculadora.Calcular(datos.Nodos, datos.Paneles, datos.Lecturas, referencia, _politica);
        var porPanel = calculo.Paneles.ToDictionary(p => p.PanelId);
        var nodos = datos.Nodos.Select(n => new NodoMonitoreoDto(n.Id, n.Nombre, calculo.Nodos[n.Id],
            datos.Paneles.Where(p => p.NodoId == n.Id)
                .Select(p => new PanelMonitoreoDto(p.Id, p.NumeroSerie, porPanel[p.Id])).ToArray())).ToArray();
        return new(datos.Parque.Id, datos.Parque.Nombre, referencia, calculo.InstanteUtc, calculo.Parque, nodos);
    }

    private async Task AutorizarAsync(CancellationToken cancellationToken)
    {
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(r => r is "Administrador" or "Tecnico" or "Consulta"))
            throw new UnauthorizedAccessException("No tienes permisos para consultar el monitoreo.");
    }
}

public sealed class AccesoMonitoreoService
{
    private readonly IMonitoreoRepository _repository;
    public AccesoMonitoreoService(IMonitoreoRepository repository) => _repository = repository;

    public async Task ValidarSuscripcionAsync(UsuarioActualDto usuario, Guid parqueId, CancellationToken cancellationToken)
    {
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(r => r is "Administrador" or "Tecnico" or "Consulta"))
            throw new UnauthorizedAccessException("No tienes permisos para consultar el monitoreo.");
        if (!(await _repository.ListarParquesAsync(cancellationToken)).Any(p => p.Id == parqueId))
            throw new KeyNotFoundException("El parque seleccionado no existe.");
    }
}

public sealed class SerieMonitoreoService
{
    private readonly int _maximoPuntos;
    private readonly TimeSpan _intervalo;
    private readonly List<PuntoGeneracionDto> _puntos = [];
    private Guid _parqueId;
    private DateTime _ultimaEvaluacion;

    public SerieMonitoreoService(int maximoPuntos, TimeSpan intervalo)
    {
        if (maximoPuntos <= 0) throw new ArgumentOutOfRangeException(nameof(maximoPuntos));
        if (intervalo <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(intervalo));
        _maximoPuntos = maximoPuntos; _intervalo = intervalo;
    }

    public IReadOnlyList<PuntoGeneracionDto> Actualizar(EstadoMonitoreoDto estado)
    {
        if (_parqueId != estado.ParqueId) { _puntos.Clear(); _ultimaEvaluacion = default; _parqueId = estado.ParqueId; }
        if (estado.EvaluadoUtc < _ultimaEvaluacion) return _puntos.ToArray();
        _ultimaEvaluacion = estado.EvaluadoUtc;
        if (estado.InstanteUtc is not { } fecha || estado.Generacion.PotenciaW is not { } potencia)
            return _puntos.ToArray();
        if (_puntos.Count > 0 && fecha < _puntos[^1].FechaHoraUtc) return _puntos.ToArray();
        // No dibujar una línea continua sobre intervalos sin mediciones.
        if (_puntos.Count > 0 && fecha - _puntos[^1].FechaHoraUtc > _intervalo) _puntos.Clear();
        var punto = new PuntoGeneracionDto(fecha, potencia, !estado.Generacion.Completo);
        if (_puntos.Count > 0 && _puntos[^1].FechaHoraUtc == fecha) _puntos[^1] = punto;
        else _puntos.Add(punto);
        if (_puntos.Count > _maximoPuntos) _puntos.RemoveRange(0, _puntos.Count - _maximoPuntos);
        return _puntos.ToArray();
    }
}
