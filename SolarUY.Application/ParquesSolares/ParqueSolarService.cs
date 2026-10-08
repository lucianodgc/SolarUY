using FluentValidation;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Domain.Entities;

namespace SolarUY.Application.ParquesSolares;

public sealed class ParqueSolarService : IParqueSolarService
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<CrearParqueSolarDto> _validator;

    public ParqueSolarService(IApplicationDbContext context, IValidator<CrearParqueSolarDto> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Guid> CrearParqueSolarAsync(CrearParqueSolarDto dto, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(dto, cancellationToken);
        if (await _context.ExisteNombreParqueSolarAsync(dto.Nombre, cancellationToken))
            throw new NombreParqueDuplicadoException();

        var parque = new ParqueSolar(dto.Nombre, dto.Ubicacion, dto.CapacidadEstimadaMW);
        _context.AgregarParqueSolar(parque);
        await _context.SaveChangesAsync(cancellationToken);
        return parque.Id;
    }

    public Task<IReadOnlyList<ParqueSolar>> ListarParquesSolaresAsync(CancellationToken cancellationToken = default)
        => _context.ListarParquesSolaresAsync(cancellationToken);
}
