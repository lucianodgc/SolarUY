using SolarUY.Application.Abstractions;

namespace SolarUY.Application.Inversores;

public sealed class InversorService : IInversorService
{
    private readonly IInversorClient _inversor;
    private readonly IInversorInventarioRepository _inventario;
    private readonly IUsuarioActual _usuarioActual;

    public InversorService(IInversorClient inversor, IInversorInventarioRepository inventario, IUsuarioActual usuarioActual)
    {
        _inversor = inversor;
        _inventario = inventario;
        _usuarioActual = usuarioActual;
    }

    public async Task<IReadOnlyList<InventarioNodoInversorDto>> ListarNodosAsync(CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(false, cancellationToken);
        return await SincronizarInventarioAsync(cancellationToken);
    }

    public async Task<EstadoInversorDto> ConsultarAsync(string nodoId, CancellationToken cancellationToken = default)
    {
        var usuario = await AutorizarAsync(false, cancellationToken);
        var nodos = await SincronizarInventarioAsync(cancellationToken);
        var nodo = nodos.SingleOrDefault(item => item.Id == nodoId)
            ?? throw new KeyNotFoundException($"No existe el nodo '{nodoId}' en la base de datos.");
        var paneles = await _inversor.ObtenerEstadoPanelesAsync(nodoId, cancellationToken);
        var volcado = await _inversor.ObtenerEstadoVolcadoRedAsync(cancellationToken);
        var encendido = await _inversor.ObtenerEstadoEncendidoAsync(cancellationToken);
        return new EstadoInversorDto(nodoId, nodo.Nombre, nodo.Parque, paneles, paneles.Sum(panel => panel.PotenciaGeneradaW),
            volcado, encendido, usuario.Roles.Contains("Administrador"), DateTime.UtcNow);
    }

    public async Task EstablecerPanelActivoAsync(string nodoId, string panelId, bool activo,
        CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(true, cancellationToken);
        await SincronizarInventarioAsync(cancellationToken);
        if (activo) await _inversor.ActivarPanelAsync(nodoId, panelId, cancellationToken);
        else await _inversor.DesactivarPanelAsync(nodoId, panelId, cancellationToken);
    }

    public async Task EstablecerVolcadoRedAsync(bool activo, CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(true, cancellationToken);
        await SincronizarInventarioAsync(cancellationToken);
        if (activo) await _inversor.ActivarVolcadoRedAsync(cancellationToken);
        else await _inversor.DesactivarVolcadoRedAsync(cancellationToken);
    }

    public async Task EstablecerInversorEncendidoAsync(bool encendido, CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(true, cancellationToken);
        await SincronizarInventarioAsync(cancellationToken);
        await _inversor.EstablecerEstadoEncendidoAsync(encendido, cancellationToken);
    }

    private async Task<IReadOnlyList<InventarioNodoInversorDto>> SincronizarInventarioAsync(CancellationToken cancellationToken)
    {
        var nodos = await _inventario.ListarAsync(cancellationToken);
        await _inversor.SincronizarInventarioAsync(nodos, cancellationToken);
        return nodos;
    }

    private async Task<UsuarioActualDto> AutorizarAsync(bool operacion, CancellationToken cancellationToken)
    {
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(rol => rol is "Administrador" or "Tecnico" or "Consulta")
            || (operacion && !usuario.Roles.Contains("Administrador")))
            throw new UnauthorizedAccessException("No tienes permisos para realizar esta operación del simulador.");
        return usuario;
    }
}
