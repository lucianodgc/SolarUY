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

    public ParqueSolarService(
        IApplicationDbContext context,
        IValidator<CrearParqueSolarDto> crearValidator,
        IValidator<ActualizarParqueSolarDto> actualizarValidator)
    {
        _context = context;
        _crearValidator = crearValidator;
        _actualizarValidator = actualizarValidator;
    }

    public async Task<Guid> CrearParqueSolarAsync(CrearParqueSolarDto dto, CancellationToken cancellationToken = default)
    {
        await _crearValidator.ValidateAndThrowAsync(dto, cancellationToken);
        if (await _context.ExisteNombreParqueSolarAsync(dto.Nombre, cancellationToken: cancellationToken))
            throw new NombreParqueDuplicadoException();

        var parque = new ParqueSolar(dto.Nombre, dto.Ubicacion, dto.CapacidadEstimadaMW);
        _context.AgregarParqueSolar(parque);
        await _context.SaveChangesAsync(cancellationToken);
        return parque.Id;
    }

    public Task<IReadOnlyList<ParqueSolar>> ListarParquesSolaresAsync(CancellationToken cancellationToken = default)
        => _context.ListarParquesSolaresAsync(cancellationToken);

    public Task<IReadOnlyList<ParqueSolar>> BuscarParquesSolaresAsync(
        string? criterio,
        CancellationToken cancellationToken = default)
        => _context.BuscarParquesSolaresAsync(criterio, cancellationToken);

    public Task<ParqueSolar?> ObtenerParqueSolarPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.ObtenerParqueSolarPorIdAsync(id, cancellationToken);

    public async Task ActualizarParqueSolarAsync(
        Guid id,
        ActualizarParqueSolarDto dto,
        CancellationToken cancellationToken = default)
    {
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
        var parque = await _context.ObtenerParqueSolarPorIdAsync(id, cancellationToken)
            ?? throw new ParqueSolarNoEncontradoException();

        await _context.EliminarParqueSolarEnCascadaAsync(parque, cancellationToken);
    }
}
