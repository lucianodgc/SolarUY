using FluentValidation;

namespace SolarUY.Application.Nodos;

public sealed class CrearNodoDtoValidator : AbstractValidator<CrearNodoDto>
{
    public CrearNodoDtoValidator()
    {
        RuleFor(dto => dto.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.");
        RuleFor(dto => dto.UbicacionEnParque).NotEmpty().WithMessage("La ubicación en el parque es obligatoria.");
        RuleFor(dto => dto.MarcaInversor).NotEmpty().WithMessage("La marca del inversor es obligatoria.");
        RuleFor(dto => dto.ModeloInversor).NotEmpty().WithMessage("El modelo del inversor es obligatorio.");
        RuleFor(dto => dto.PotenciaNominalKW).GreaterThan(0).WithMessage("La potencia nominal debe ser mayor a cero.");
        RuleFor(dto => dto.EstadoInversor).IsInEnum();
    }
}
