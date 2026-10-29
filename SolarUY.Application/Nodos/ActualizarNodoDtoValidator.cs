using FluentValidation;

namespace SolarUY.Application.Nodos;

public sealed class ActualizarNodoDtoValidator : AbstractValidator<ActualizarNodoDto>
{
    public ActualizarNodoDtoValidator()
    {
        RuleFor(dto => dto.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.");
        RuleFor(dto => dto.UbicacionEnParque).NotEmpty().WithMessage("La ubicación en el parque es obligatoria.");
    }
}
