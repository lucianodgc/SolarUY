using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;

namespace SolarUY.Application.Mantenimiento;

public sealed class TareaMantenimientoService : ITareaMantenimientoService
{
    private readonly ITareaMantenimientoRepository _tareas;
    private readonly IPanelRepository _paneles;
    private readonly ITecnicoDirectory _tecnicos;
    private readonly IUsuarioActual _usuarioActual;

    public TareaMantenimientoService(ITareaMantenimientoRepository tareas, IPanelRepository paneles,
        ITecnicoDirectory tecnicos, IUsuarioActual usuarioActual)
    {
        _tareas = tareas;
        _paneles = paneles;
        _tecnicos = tecnicos;
        _usuarioActual = usuarioActual;
    }

    public async Task<Guid> CrearAsync(CrearTareaMantenimientoDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ExigirAdministrador(await ObtenerUsuarioAsync(cancellationToken));
        var panel = await _paneles.ObtenerAsync(dto.PanelId, cancellationToken)
            ?? throw new MantenimientoException("El panel seleccionado no existe.");
        await ValidarTecnicoAsync(dto.ResponsableId, cancellationToken);
        TareaMantenimiento tarea;
        try { tarea = new TareaMantenimiento(panel, dto.Tipo, dto.Descripcion, dto.FechaProgramada, dto.ResponsableId); }
        catch (ArgumentException exception) { throw new MantenimientoException(exception.Message); }
        await _tareas.AgregarAsync(tarea, cancellationToken);
        return tarea.Id;
    }

    public Task AsignarAsync(Guid id, string responsableId, CancellationToken cancellationToken = default)
        => ModificarAsync(id, true, responsableId, tarea => tarea.AsignarResponsable(responsableId), cancellationToken);

    public Task EditarAsync(Guid id, EditarTareaMantenimientoDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return ModificarAsync(id, true, dto.ResponsableId,
            tarea => tarea.Editar(dto.Tipo, dto.Descripcion, dto.FechaProgramada, dto.ResponsableId), cancellationToken, dto.Original);
    }

    public Task IniciarAsync(Guid id, CancellationToken cancellationToken = default)
        => ModificarAsync(id, false, null, tarea => tarea.Iniciar(), cancellationToken);

    public Task CompletarAsync(Guid id, CancellationToken cancellationToken = default)
        => ModificarAsync(id, false, null, tarea => tarea.Completar(DateTime.UtcNow), cancellationToken);

    public Task CancelarAsync(Guid id, CancellationToken cancellationToken = default)
        => ModificarAsync(id, false, null, tarea => tarea.Cancelar(), cancellationToken);

    public async Task<TareaMantenimientoDto> ObtenerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var usuario = await ObtenerUsuarioAsync(cancellationToken);
        var tarea = await CargarAsync(id, usuario, cancellationToken);
        return Proyectar(tarea, usuario, DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<TareaMantenimientoDto>> ListarAsync(FiltroTareasDto filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var usuario = await ObtenerUsuarioAsync(cancellationToken);
        if (filtro.Estado is { } estado && !Enum.IsDefined(estado))
            throw new MantenimientoException("El estado de consulta no es válido.");
        var referencia = DateTime.UtcNow;
        var tareas = await _tareas.ListarAsync(filtro, Alcance(usuario), referencia, cancellationToken);
        return tareas.Select(tarea => Proyectar(tarea, usuario, referencia)).ToArray();
    }

    public async Task<IReadOnlyList<PanelDto>> ListarPanelesAsync(CancellationToken cancellationToken = default)
    {
        var usuario = await ObtenerUsuarioAsync(cancellationToken);
        if (EsAdministrador(usuario)) return await _paneles.ListarAsync(cancellationToken);
        var tareas = await _tareas.ListarAsync(new FiltroTareasDto(), usuario.Id, DateTime.UtcNow, cancellationToken);
        return tareas.Select(tarea => tarea.Panel).DistinctBy(panel => panel.Id)
            .Select(panel => new PanelDto(panel.Id, panel.NumeroSerie, panel.Nodo.Nombre, panel.Nodo.Parque.Nombre)).ToArray();
    }

    public async Task<IReadOnlyList<TecnicoDto>> ListarTecnicosAsync(CancellationToken cancellationToken = default)
    {
        ExigirAdministrador(await ObtenerUsuarioAsync(cancellationToken));
        return await _tecnicos.ListarAsync(cancellationToken);
    }

    private async Task ModificarAsync(Guid id, bool soloAdministrador, string? responsableDestino,
        Action<TareaMantenimiento> cambiar, CancellationToken cancellationToken, TareaMantenimientoOriginal? originalEsperado = null)
    {
        var usuario = await ObtenerUsuarioAsync(cancellationToken);
        if (soloAdministrador) ExigirAdministrador(usuario);
        var tarea = await CargarAsync(id, usuario, cancellationToken);
        var original = originalEsperado ?? new TareaMantenimientoOriginal(tarea.Estado, tarea.ResponsableId, tarea.Tipo,
            tarea.Descripcion, tarea.FechaProgramada, tarea.FechaRealizacion);
        if (responsableDestino is not null) await ValidarTecnicoAsync(responsableDestino, cancellationToken);
        try { cambiar(tarea); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { throw new MantenimientoException(exception.Message); }
        if (!await _tareas.GuardarAsync(tarea, original, Alcance(usuario), cancellationToken))
            throw new MantenimientoConflictoException();
    }

    private async Task<TareaMantenimiento> CargarAsync(Guid id, UsuarioActualDto usuario, CancellationToken cancellationToken)
        => await _tareas.ObtenerAsync(id, Alcance(usuario), cancellationToken)
            ?? throw new MantenimientoException("La tarea no existe o no tienes permiso para acceder.");

    private async Task ValidarTecnicoAsync(string responsableId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(responsableId)
            || !await _tecnicos.EsTecnicoHabilitadoAsync(responsableId, cancellationToken))
            throw new MantenimientoException("El responsable debe ser un usuario habilitado con rol Tecnico en Keycloak.");
    }

    private async Task<UsuarioActualDto> ObtenerUsuarioAsync(CancellationToken cancellationToken)
    {
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(rol => rol is "Administrador" or "Tecnico"))
            throw new MantenimientoException("No tienes permisos para gestionar mantenimiento.");
        return usuario;
    }

    private static bool EsAdministrador(UsuarioActualDto usuario) => usuario.Roles.Contains("Administrador");
    private static string? Alcance(UsuarioActualDto usuario) => EsAdministrador(usuario) ? null : usuario.Id;

    private static void ExigirAdministrador(UsuarioActualDto usuario)
    {
        if (!EsAdministrador(usuario)) throw new MantenimientoException("Esta operación requiere el rol Administrador.");
    }

    private static TareaMantenimientoDto Proyectar(TareaMantenimiento tarea, UsuarioActualDto usuario, DateTime referencia)
    {
        var editable = tarea.Estado is EstadoTareaMantenimiento.Pendiente or EstadoTareaMantenimiento.EnCurso;
        return new(tarea.Id, tarea.PanelId, tarea.Panel.NumeroSerie, tarea.Tipo, tarea.Descripcion,
            tarea.FechaProgramada, tarea.FechaRealizacion, tarea.ResponsableId, tarea.Estado, tarea.EstaVencida(referencia),
            tarea.Estado == EstadoTareaMantenimiento.Pendiente, tarea.Estado == EstadoTareaMantenimiento.EnCurso,
            editable, editable && EsAdministrador(usuario));
    }
}
