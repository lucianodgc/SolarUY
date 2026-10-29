using FluentValidation;
using SolarUY.Application.Abstractions;
using SolarUY.Domain.Entities;

namespace SolarUY.Application.Nodos;

public sealed class NodoService : INodoService
{
    private readonly INodoRepository _nodos;
    private readonly IUsuarioActual _usuarioActual;
    private readonly IValidator<CrearNodoDto> _crearValidator;
    private readonly IValidator<ActualizarNodoDto> _actualizarValidator;

    public NodoService(INodoRepository nodos, IUsuarioActual usuarioActual,
        IValidator<CrearNodoDto> crearValidator, IValidator<ActualizarNodoDto> actualizarValidator)
    {
        _nodos = nodos;
        _usuarioActual = usuarioActual;
        _crearValidator = crearValidator;
        _actualizarValidator = actualizarValidator;
    }

    public async Task<Guid> CrearAsync(Guid parqueId, CrearNodoDto dto, CancellationToken cancellationToken = default)
    {
        await ExigirAdministradorAsync(cancellationToken);
        await _crearValidator.ValidateAndThrowAsync(dto, cancellationToken);
        var parque = await _nodos.ObtenerParqueAsync(parqueId, cancellationToken)
            ?? throw new KeyNotFoundException("El parque solar no existe.");
        var nodo = new Nodo(parque, dto.Nombre, dto.UbicacionEnParque, dto.CantidadPanelesPrevista);
        _ = new Inversor(nodo, dto.MarcaInversor, dto.ModeloInversor, dto.PotenciaNominalKW,
            dto.EstadoInversor, dto.VolcadoRedActivo);
        await _nodos.AgregarAsync(nodo, cancellationToken);
        return nodo.Id;
    }

    public async Task<IReadOnlyList<NodoDto>> ListarPorParqueAsync(Guid parqueId, CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        return (await _nodos.ListarPorParqueAsync(parqueId, cancellationToken)).Select(Mapear).ToArray();
    }

    public async Task<NodoDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        var nodo = await _nodos.ObtenerPorIdAsync(id, cancellationToken);
        return nodo is null ? null : Mapear(nodo);
    }

    public async Task ActualizarAsync(Guid id, ActualizarNodoDto dto, CancellationToken cancellationToken = default)
    {
        await ExigirAdministradorAsync(cancellationToken);
        await _actualizarValidator.ValidateAndThrowAsync(dto, cancellationToken);
        var nodo = await _nodos.ObtenerPorIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("El nodo no existe.");
        nodo.ActualizarDatos(dto.Nombre, dto.UbicacionEnParque, dto.CantidadPanelesPrevista);
        await _nodos.ActualizarAsync(nodo, cancellationToken);
    }

    public async Task EliminarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExigirAdministradorAsync(cancellationToken);
        var nodo = await _nodos.ObtenerParaEliminarAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("El nodo no existe.");
        nodo.Parque.QuitarNodo(nodo);
        await _nodos.EliminarEnCascadaAsync(nodo, cancellationToken);
    }

    private static NodoDto Mapear(Nodo nodo) => new(nodo.Id, nodo.ParqueId, nodo.Nombre, nodo.UbicacionEnParque,
        nodo.CantidadPanelesPrevista, new(nodo.Inversor.Id, nodo.Inversor.Marca, nodo.Inversor.Modelo,
            nodo.Inversor.PotenciaNominalKW, nodo.Inversor.Estado, nodo.Inversor.VolcadoRedActivo));

    private async Task ExigirAdministradorAsync(CancellationToken cancellationToken)
    {
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id) || !usuario.Roles.Contains("Administrador"))
            throw new UnauthorizedAccessException("No tienes permisos para gestionar nodos.");
    }

    private async Task ExigirConsultaAsync(CancellationToken cancellationToken)
    {
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(rol => rol is "Administrador" or "Tecnico" or "Consulta"))
            throw new UnauthorizedAccessException("No tienes permisos para consultar nodos.");
    }
}
