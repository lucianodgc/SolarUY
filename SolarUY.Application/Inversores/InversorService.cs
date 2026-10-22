using SolarUY.Application.Abstractions;

namespace SolarUY.Application.Inversores;

public sealed class InversorService : IInversorService
{
    private readonly IInversorClient _inversor;
    private readonly IUsuarioActual _usuarioActual;

    public InversorService(IInversorClient inversor, IUsuarioActual usuarioActual)
    {
        _inversor = inversor;
        _usuarioActual = usuarioActual;
    }

    public async Task<IReadOnlyList<string>> ListarNodosAsync(CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(false, cancellationToken);
        return await _inversor.ListarNodosAsync(cancellationToken);
    }

    public async Task<EstadoInversorDto> ConsultarAsync(string nodoId, CancellationToken cancellationToken = default)
    {
        var usuario = await AutorizarAsync(false, cancellationToken);
        var paneles = await _inversor.ObtenerEstadoPanelesAsync(nodoId, cancellationToken);
        var volcado = await _inversor.ObtenerEstadoVolcadoRedAsync(cancellationToken);
        return new EstadoInversorDto(nodoId, paneles, paneles.Sum(panel => panel.PotenciaGeneradaW),
            volcado, usuario.Roles.Contains("Administrador"), DateTime.UtcNow);
    }

    public async Task EstablecerPanelActivoAsync(string nodoId, string panelId, bool activo,
        CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(true, cancellationToken);
        if (activo) await _inversor.ActivarPanelAsync(nodoId, panelId, cancellationToken);
        else await _inversor.DesactivarPanelAsync(nodoId, panelId, cancellationToken);
    }

    public async Task EstablecerVolcadoRedAsync(bool activo, CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(true, cancellationToken);
        if (activo) await _inversor.ActivarVolcadoRedAsync(cancellationToken);
        else await _inversor.DesactivarVolcadoRedAsync(cancellationToken);
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
