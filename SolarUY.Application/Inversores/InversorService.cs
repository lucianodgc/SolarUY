using SolarUY.Application.Abstractions;

namespace SolarUY.Application.Inversores;

public sealed class InversorService : IInversorService
{
    private readonly IInversorClient _inversor;
    private readonly IInversorInventarioRepository _inventario;
    private readonly IUsuarioActual _usuarioActual;
    private readonly IOrdenInversorService _ordenes;

    public InversorService(IInversorClient inversor, IInversorInventarioRepository inventario, IUsuarioActual usuarioActual, IOrdenInversorService ordenes)
    {
        _inversor = inversor;
        _inventario = inventario;
        _usuarioActual = usuarioActual;
        _ordenes = ordenes;
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
        var resultado = await _ordenes.EjecutarAsync(new(Guid.NewGuid(), activo
            ? SolarUY.Domain.Entities.AccionOrdenInversor.ActivarPanel : SolarUY.Domain.Entities.AccionOrdenInversor.DesactivarPanel,
            nodoId, panelId), cancellationToken);
        ExigirConfirmacion(resultado);
    }

    public async Task EstablecerVolcadoRedAsync(bool activo, CancellationToken cancellationToken = default)
    {
        var resultado = await _ordenes.EjecutarAsync(new(Guid.NewGuid(), activo
            ? SolarUY.Domain.Entities.AccionOrdenInversor.ActivarVolcadoRed : SolarUY.Domain.Entities.AccionOrdenInversor.DesactivarVolcadoRed), cancellationToken);
        ExigirConfirmacion(resultado);
    }

    private static void ExigirConfirmacion(ResultadoOrdenInversorDto resultado)
    {
        if (resultado.Codigo == "sin-permiso") throw new UnauthorizedAccessException("No tienes permisos para realizar esta operación del inversor.");
        if (resultado.Codigo == "destino-no-vigente") throw new KeyNotFoundException("El destino ya no existe en ese nodo.");
        if (resultado.Estado != SolarUY.Domain.Entities.EstadoOrdenInversor.Confirmada || !resultado.AuditoriaCompleta)
            throw new InvalidOperationException($"Orden {resultado.IntentoId}: {resultado.Estado} ({resultado.Codigo}). Auditoría completa: {resultado.AuditoriaCompleta}.");
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
