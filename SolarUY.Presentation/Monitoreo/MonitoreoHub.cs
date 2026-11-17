using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using SolarUY.Application.Monitoreo;
using SolarUY.Application.Inversores;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace SolarUY.Presentation.Monitoreo;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Administrador,Tecnico,Consulta")]
public sealed class MonitoreoHub : Hub
{
    private readonly AccesoMonitoreoService _acceso;
    private readonly NotificadorTelemetriaEnVivo? _telemetria;

    public MonitoreoHub(AccesoMonitoreoService acceso, NotificadorTelemetriaEnVivo? telemetria = null)
    { _acceso = acceso; _telemetria = telemetria; }

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
        if (_telemetria is not null)
            await _telemetria.EnviarUltimaAsync(parqueId, Clients.Caller, Context.ConnectionAborted);
    }

    public async Task DesuscribirParque()
    {
        if (Context.Items.Remove("Parque", out var anterior) && anterior is Guid id)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(id), Context.ConnectionAborted);
    }

    public static string Grupo(Guid parqueId) => $"monitoreo:{parqueId:D}";
}

public sealed class NotificadorTelemetriaEnVivo
{
    private readonly IHubContext<MonitoreoHub> _hub;
    private readonly TimeProvider _reloj;
    private readonly TimeSpan _vigencia;
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<TelemetriaNodoDto>> _ultimas = new();
    public NotificadorTelemetriaEnVivo(IHubContext<MonitoreoHub> hub, TimeProvider? reloj = null,
        IOptions<TelemetriaEnVivoOptions>? opciones = null)
    {
        _hub = hub; _reloj = reloj ?? TimeProvider.System;
        _vigencia = TimeSpan.FromSeconds(3 * (opciones?.Value.IntervaloSegundos ?? 2));
    }

    public Task NotificarAsync(Guid parqueId, IReadOnlyList<TelemetriaNodoDto> nodos, CancellationToken cancellationToken)
    {
        _ultimas[parqueId] = nodos.ToArray();
        return _hub.Clients.Group(MonitoreoHub.Grupo(parqueId))
            .SendAsync("TelemetriaActualizada", nodos, cancellationToken);
    }

    public Task EnviarUltimaAsync(Guid parqueId, IClientProxy cliente, CancellationToken cancellationToken)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        if (!_ultimas.TryGetValue(parqueId, out var nodos) || nodos.Count == 0
            || nodos.Any(n => n.ParqueId != parqueId || n.FechaConsultaUtc > ahora || ahora - n.FechaConsultaUtc > _vigencia))
            return Task.CompletedTask;
        return cliente.SendAsync("TelemetriaActualizada", nodos, cancellationToken);
    }
}

// Avisa directamente a los usuarios de esta instancia después de guardar las lecturas.
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
