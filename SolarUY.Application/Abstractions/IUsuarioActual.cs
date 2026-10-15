namespace SolarUY.Application.Abstractions;

public interface IUsuarioActual
{
    Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default);
}
