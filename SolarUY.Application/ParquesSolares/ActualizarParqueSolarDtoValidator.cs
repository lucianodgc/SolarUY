using FluentValidation;

namespace SolarUY.Application.ParquesSolares;

public sealed class ActualizarParqueSolarDtoValidator : AbstractValidator<ActualizarParqueSolarDto>
{
    public ActualizarParqueSolarDtoValidator()
    {
        RuleFor(dto => dto.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre admite hasta 100 caracteres.");
        RuleFor(dto => dto.Ubicacion).NotEmpty().WithMessage("La ubicación es obligatoria.")
            .MaximumLength(200).WithMessage("La ubicación admite hasta 200 caracteres.");
    }
}
