using FluentValidation;

namespace SolarUY.Application.Paneles;

public sealed class PanelDtoValidator : AbstractValidator<CrearPanelDto>
{
    public PanelDtoValidator()
    {
        RuleFor(dto => dto.NodoId).NotEmpty();
        RuleFor(dto => dto.NumeroSerie).NotEmpty();
        RuleFor(dto => dto.Marca).NotEmpty();
        RuleFor(dto => dto.Modelo).NotEmpty();
        RuleFor(dto => dto.PotenciaNominalW).GreaterThan(0);
        RuleFor(dto => dto.FechaInstalacion).NotEqual(default(DateTime)).Must(fecha => fecha.Kind == DateTimeKind.Utc)
            .WithMessage("La fecha de instalación debe estar en UTC.");
        RuleFor(dto => dto.Estado).IsInEnum();
    }
}

public sealed class ActualizarPanelDtoValidator : AbstractValidator<ActualizarPanelDto>
{
    public ActualizarPanelDtoValidator()
    {
        Include(new PanelDtoValidator());
    }
}
