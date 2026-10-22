using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SolarUY.Application.ParquesSolares;

namespace SolarUY.Presentation.Components.Shared;

public abstract class ParqueSolarComponentBase : OwningComponentBase<IParqueSolarService>
{
    [Inject]
    private AuthenticationStateProvider CircuitAuthenticationStateProvider { get; set; } = null!;

    private IHostEnvironmentAuthenticationStateProvider? _scopedAuthenticationStateProvider;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        // El ámbito del componente conserva su DbContext, pero usa la identidad del circuito.
        _scopedAuthenticationStateProvider = ScopedServices.GetRequiredService<AuthenticationStateProvider>()
            as IHostEnvironmentAuthenticationStateProvider
            ?? throw new InvalidOperationException("El ámbito del componente requiere autenticación del servidor.");
        _scopedAuthenticationStateProvider.SetAuthenticationState(
            CircuitAuthenticationStateProvider.GetAuthenticationStateAsync());
        CircuitAuthenticationStateProvider.AuthenticationStateChanged += ActualizarAutenticacion;
    }

    private void ActualizarAutenticacion(Task<AuthenticationState> estado)
        => _scopedAuthenticationStateProvider?.SetAuthenticationState(estado);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            CircuitAuthenticationStateProvider.AuthenticationStateChanged -= ActualizarAutenticacion;
        base.Dispose(disposing);
    }
}
