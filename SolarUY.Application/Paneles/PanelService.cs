using FluentValidation;
using SolarUY.Application.Abstractions;
using SolarUY.Domain.Entities;

namespace SolarUY.Application.Paneles;

public sealed class PanelService : IPanelService
{
    private readonly IPanelRepository _paneles;
    private readonly IUsuarioActual _usuarioActual;
    private readonly IValidator<CrearPanelDto> _crearValidator;
    private readonly IValidator<ActualizarPanelDto> _actualizarValidator;

    public PanelService(IPanelRepository paneles, IUsuarioActual usuarioActual, IValidator<CrearPanelDto> crearValidator,
        IValidator<ActualizarPanelDto> actualizarValidator)
    {
        _paneles = paneles;
        _usuarioActual = usuarioActual;
        _crearValidator = crearValidator;
        _actualizarValidator = actualizarValidator;
    }

    public async Task<Guid> CrearAsync(CrearPanelDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await ExigirAdministradorAsync(cancellationToken);
        await _crearValidator.ValidateAndThrowAsync(dto, cancellationToken);
        if (await _paneles.ExisteNumeroSerieAsync(dto.NumeroSerie, null, cancellationToken))
            throw new InvalidOperationException("Ya existe un panel con ese número de serie.");
        var nodo = await ObtenerNodoAsync(dto.NodoId, cancellationToken);
        var panel = CrearPanel(nodo, dto);
        await _paneles.AgregarAsync(panel, cancellationToken);
        return panel.Id;
    }

    public async Task<PanelDto?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        var panel = await _paneles.ObtenerAsync(id, cancellationToken);
        return panel is null ? null : Mapear(panel);
    }

    public async Task<IReadOnlyList<PanelDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        return await _paneles.ListarDetallesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PanelDto>> ListarPorNodoAsync(Guid nodoId, CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        _ = await ObtenerNodoAsync(nodoId, cancellationToken);
        return await _paneles.ListarDetallesPorNodoAsync(nodoId, cancellationToken);
    }

    public async Task ActualizarAsync(Guid id, ActualizarPanelDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await ExigirAdministradorAsync(cancellationToken);
        await _actualizarValidator.ValidateAndThrowAsync(dto, cancellationToken);
        var panel = await _paneles.ObtenerAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("El panel no existe o está dado de baja.");
        if (await _paneles.ExisteNumeroSerieAsync(dto.NumeroSerie, id, cancellationToken))
            throw new InvalidOperationException("Ya existe un panel con ese número de serie.");
        var nodoOriginalId = panel.NodoId;
        var nodoDestino = await ObtenerNodoAsync(dto.NodoId, cancellationToken);
        panel.Actualizar(dto.NumeroSerie, dto.Marca, dto.Modelo, dto.PotenciaNominalW, dto.FechaInstalacion, dto.Estado);
        if (nodoOriginalId != nodoDestino.Id) panel.TrasladarANodo(nodoDestino);
        await _paneles.ActualizarAsync(panel, nodoOriginalId, cancellationToken);
    }

    public async Task DarDeBajaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExigirAdministradorAsync(cancellationToken);
        var panel = await _paneles.ObtenerAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("El panel no existe o está dado de baja.");
        panel.DarDeBaja();
        await _paneles.DarDeBajaAsync(panel, cancellationToken);
    }

    private async Task<Nodo> ObtenerNodoAsync(Guid nodoId, CancellationToken cancellationToken)
        => await _paneles.ObtenerNodoAsync(nodoId, cancellationToken)
            ?? throw new KeyNotFoundException("El nodo seleccionado no existe.");

    private static Panel CrearPanel(Nodo nodo, CrearPanelDto dto)
        => new(nodo, dto.NumeroSerie, dto.Marca, dto.Modelo, dto.PotenciaNominalW, dto.FechaInstalacion, dto.Estado);

    private static PanelDto Mapear(Panel panel) => new(panel.Id, panel.NodoId, panel.Nodo.Nombre, panel.Nodo.Parque.Nombre,
        panel.NumeroSerie, panel.Marca, panel.Modelo, panel.PotenciaNominalW, panel.FechaInstalacion, panel.Estado);

    private async Task ExigirAdministradorAsync(CancellationToken cancellationToken)
    {
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id) || !usuario.Roles.Contains("Administrador"))
            throw new UnauthorizedAccessException("Esta operación requiere el rol Administrador.");
    }

    private async Task ExigirConsultaAsync(CancellationToken cancellationToken)
    {
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(rol => rol is "Administrador" or "Tecnico" or "Consulta"))
            throw new UnauthorizedAccessException("No tienes permisos para consultar paneles.");
    }
}
