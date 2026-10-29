using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using SolarUY.Application.Monitoreo;

namespace SolarUY.Presentation.Monitoreo;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Administrador,Tecnico,Consulta")]
public sealed class MonitoreoHub : Hub
{
    private readonly AccesoMonitoreoService _acceso;

    public MonitoreoHub(AccesoMonitoreoService acceso) => _acceso = acceso;

    public async Task SuscribirParque(Guid parqueId)
    {
        var usuario = Context.User;
        var identidad = new SolarUY.Application.Abstractions.UsuarioActualDto(usuario?.Identity?.IsAuthenticated == true,
            usuario?.FindFirst("sub")?.Value ?? usuario?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            usuario?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray() ?? []);
        try { await _acceso.ValidarSuscripcionAsync(identidad, parqueId, Context.ConnectionAborted); }
        catch (Exception exception) when (exception is UnauthorizedAccessException or KeyNotFoundException)
        { throw new HubException(exception.Message); }
        if (Context.Items.TryGetValue("Parque", out var anterior) && anterior is Guid id)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(id), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(parqueId), Context.ConnectionAborted);
        Context.Items["Parque"] = parqueId;
    }

    public async Task DesuscribirParque()
    {
        if (Context.Items.Remove("Parque", out var anterior) && anterior is Guid id)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(id), Context.ConnectionAborted);
    }

    public static string Grupo(Guid parqueId) => $"monitoreo:{parqueId:D}";
}

// Entrega exclusivamente local. El transporte entre instancias queda pendiente del acuerdo D2.
public sealed class NotificadorLecturasConfirmadas : INotificadorLecturasConfirmadas
{
    private readonly IHubContext<MonitoreoHub> _hub;
    public NotificadorLecturasConfirmadas(IHubContext<MonitoreoHub> hub) => _hub = hub;

    public async Task NotificarAsync(IReadOnlyList<LecturaConfirmadaDto> lecturas, CancellationToken cancellationToken = default)
    {
        foreach (var parqueId in lecturas.Select(l => l.ParqueId).Distinct())
            await _hub.Clients.Group(MonitoreoHub.Grupo(parqueId))
                .SendAsync("LecturasConfirmadas", parqueId, cancellationToken);
    }
}
