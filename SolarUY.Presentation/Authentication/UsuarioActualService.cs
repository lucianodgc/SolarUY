using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SolarUY.Application.Abstractions;

namespace SolarUY.Presentation.Authentication;

public sealed class UsuarioActualService : IUsuarioActual
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public UsuarioActualService(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var estado = await _authenticationStateProvider.GetAuthenticationStateAsync()
            .WaitAsync(cancellationToken);
        var usuario = estado.User;

        if (usuario.Identity?.IsAuthenticated != true)
            return new UsuarioActualDto(false, null, Array.Empty<string>());

        var id = usuario.FindFirst("sub")?.Value
            ?? usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roles = usuario.Identities
            .Where(identidad => identidad.IsAuthenticated)
            .SelectMany(identidad => identidad.FindAll(identidad.RoleClaimType))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new UsuarioActualDto(true, id, roles);
    }
}
