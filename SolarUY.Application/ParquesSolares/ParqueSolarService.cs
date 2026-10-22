using FluentValidation;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Domain.Entities;

namespace SolarUY.Application.ParquesSolares;

public sealed class ParqueSolarService : IParqueSolarService
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<CrearParqueSolarDto> _crearValidator;
    private readonly IValidator<ActualizarParqueSolarDto> _actualizarValidator;
    private readonly IUsuarioActual _usuarioActual;

    public ParqueSolarService(
        IApplicationDbContext context,
        IValidator<CrearParqueSolarDto> crearValidator,
        IValidator<ActualizarParqueSolarDto> actualizarValidator,
        IUsuarioActual usuarioActual)
    {
        _context = context;
        _crearValidator = crearValidator;
        _actualizarValidator = actualizarValidator;
        _usuarioActual = usuarioActual;
    }

    public async Task<Guid> CrearParqueSolarAsync(CrearParqueSolarDto dto, CancellationToken cancellationToken = default)
    {
        await ExigirAdministradorAsync(cancellationToken);
        await _crearValidator.ValidateAndThrowAsync(dto, cancellationToken);
        if (await _context.ExisteNombreParqueSolarAsync(dto.Nombre, cancellationToken: cancellationToken))
            throw new NombreParqueDuplicadoException();

        var parque = new ParqueSolar(dto.Nombre, dto.Ubicacion, dto.CapacidadEstimadaMW);
        _context.AgregarParqueSolar(parque);
        await _context.SaveChangesAsync(cancellationToken);
        return parque.Id;
    }

    public async Task<IReadOnlyList<ParqueSolar>> ListarParquesSolaresAsync(CancellationToken cancellationToken = default)
    {
        await ExigirAdministradorAsync(cancellationToken);
        return await _context.ListarParquesSolaresAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParqueSolar>> BuscarParquesSolaresAsync(
        string? criterio,
        CancellationToken cancellationToken = default)
    {
        await ExigirAdministradorAsync(cancellationToken);
        return await _context.BuscarParquesSolaresAsync(criterio, cancellationToken);
    }

    public async Task<ParqueSolar?> ObtenerParqueSolarPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExigirAdministradorAsync(cancellationToken);
        return await _context.ObtenerParqueSolarPorIdAsync(id, cancellationToken);
    }

    public async Task ActualizarParqueSolarAsync(
        Guid id,
        ActualizarParqueSolarDto dto,
        CancellationToken cancellationToken = default)
    {
        await ExigirAdministradorAsync(cancellationToken);
        await _actualizarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var parque = await _context.ObtenerParqueSolarPorIdAsync(id, cancellationToken)
            ?? throw new ParqueSolarNoEncontradoException();

        if (await _context.ExisteNombreParqueSolarAsync(dto.Nombre, id, cancellationToken))
            throw new NombreParqueDuplicadoException();

        parque.ActualizarDatos(dto.Nombre, dto.Ubicacion, dto.CapacidadEstimadaMW);
        _context.ActualizarParqueSolar(parque);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task EliminarParqueSolarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExigirAdministradorAsync(cancellationToken);
        var parque = await _context.ObtenerParqueSolarPorIdAsync(id, cancellationToken)
            ?? throw new ParqueSolarNoEncontradoException();

        await _context.EliminarParqueSolarEnCascadaAsync(parque, cancellationToken);
    }

    private async Task ExigirAdministradorAsync(CancellationToken cancellationToken)
    {
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Contains("Administrador"))
            throw new UnauthorizedAccessException("No tienes permisos para gestionar parques solares.");
    }
}
