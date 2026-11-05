using SolarUY.Domain.Enums;
using SolarUY.Application.Abstractions;

namespace SolarUY.Application.Inversores;

public sealed record InventarioNodoInversorDto(
    string Id,
    string Nombre,
    string Parque,
    Guid ParqueId,
    IReadOnlyList<InventarioPanelInversorDto> Paneles);

public sealed record TelemetriaNodoDto(
    Guid ParqueId,
    string Parque,
    string NodoId,
    string NodoNombre,
    IReadOnlyList<PanelInversorEstadoDto> Paneles,
    decimal PotenciaGeneradaW,
    bool VolcadoRedActivo,
    bool InversorEncendido,
    DateTime FechaConsultaUtc);

public sealed record InventarioPanelInversorDto(
    string Id,
    string NumeroSerie,
    decimal PotenciaNominalW,
    EstadoPanel Estado);

public interface IInversorInventarioRepository
{
    Task<IReadOnlyList<InventarioNodoInversorDto>> ListarAsync(CancellationToken cancellationToken = default);
}

public interface ITelemetriaEnVivoService
{
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<TelemetriaNodoDto>>> ConsultarAsync(CancellationToken cancellationToken = default);
}

public sealed class TelemetriaEnVivoService : ITelemetriaEnVivoService
{
    private readonly IInversorInventarioRepository _inventarioRepository;
    private readonly IInversorClient _inversor;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _intervaloInventario;
    private readonly SemaphoreSlim _semaforo = new(1, 1);
    private IReadOnlyList<InventarioNodoInversorDto>? _inventario;
    private DateTime _proximaActualizacion;

    public TelemetriaEnVivoService(IInversorInventarioRepository inventarioRepository, IInversorClient inversor,
        TimeProvider clock, TimeSpan intervaloInventario)
    {
        _inventarioRepository = inventarioRepository;
        _inversor = inversor;
        _clock = clock;
        _intervaloInventario = intervaloInventario > TimeSpan.Zero
            ? intervaloInventario : throw new ArgumentOutOfRangeException(nameof(intervaloInventario));
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TelemetriaNodoDto>>> ConsultarAsync(
        CancellationToken cancellationToken = default)
    {
        await _semaforo.WaitAsync(cancellationToken);
        try
        {
            var ahora = _clock.GetUtcNow().UtcDateTime;
            if (_inventario is null || ahora >= _proximaActualizacion)
            {
                _inventario = await _inventarioRepository.ListarAsync(cancellationToken);
                await _inversor.SincronizarInventarioAsync(_inventario, cancellationToken);
                _proximaActualizacion = ahora.Add(_intervaloInventario);
            }

            var grupos = new Dictionary<Guid, List<TelemetriaNodoDto>>();
            foreach (var nodo in _inventario)
            {
                var paneles = await _inversor.ObtenerEstadoPanelesAsync(nodo.Id, cancellationToken);
                var lectura = new TelemetriaNodoDto(nodo.ParqueId, nodo.Parque, nodo.Id, nodo.Nombre, paneles,
                    paneles.Sum(p => p.PotenciaGeneradaW), await _inversor.ObtenerEstadoVolcadoRedAsync(cancellationToken),
                    await _inversor.ObtenerEstadoEncendidoAsync(cancellationToken), ahora);
                if (!grupos.TryGetValue(nodo.ParqueId, out var lista)) grupos[nodo.ParqueId] = lista = [];
                lista.Add(lectura);
            }
            return grupos.ToDictionary(g => g.Key, g => (IReadOnlyList<TelemetriaNodoDto>)g.Value);
        }
        finally { _semaforo.Release(); }
    }
}
