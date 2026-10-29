using SolarUY.Domain.Enums;

namespace SolarUY.Application.Inversores;

public sealed record InventarioNodoInversorDto(
    string Id,
    string Nombre,
    string Parque,
    IReadOnlyList<InventarioPanelInversorDto> Paneles);

public sealed record InventarioPanelInversorDto(
    string Id,
    string NumeroSerie,
    decimal PotenciaNominalW,
    EstadoPanel Estado);

public interface IInversorInventarioRepository
{
    Task<IReadOnlyList<InventarioNodoInversorDto>> ListarAsync(CancellationToken cancellationToken = default);
}
