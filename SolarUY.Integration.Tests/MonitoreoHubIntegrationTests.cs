using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolarUY.Application.Abstractions;
using SolarUY.Domain.Monitoreo;
using SolarUY.Application.Monitoreo;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Infrastructure;
using SolarUY.Presentation.BackgroundServices;
using SolarUY.Presentation.Monitoreo;
using SolarUY.Presentation.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class MonitoreoHubIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task Pantalla_RenderizaEstadoInicialYGraficoConRecursosLocales()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        await using var app = await CrearHostAsync(pagina: true);
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (await context.LecturasPanel.CountAsync(limite.Token) < 4) await Task.Delay(50, limite.Token);
        using var cliente = Cliente(app, out _);
        await cliente.PostAsync("/pruebas/ingresar/Consulta", null);
        var respuesta = await cliente.GetAsync("/monitoreo");
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.True(respuesta.IsSuccessStatusCode, html);
        Assert.Contains("Monitoreo en vivo", html);
        Assert.Contains("UTC", html);
        Assert.Contains("Evolución de potencia del parque", html);
        Assert.Contains("Otro parque", html);
        Assert.DoesNotContain("Aún no hay puntos disponibles", html);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/lib/signalr/8.0.17/signalr.min.js")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/js/monitoreo.js")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/_framework/blazor.web.js")).StatusCode);
    }

    [MariaDbHistoricoTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Worker_DistingueFalloDePersistenciaYFalloDePublicacion(bool fallaPersistencia)
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        var registro = new RegistroNotificaciones { Fallar = !fallaPersistencia };
        await using var app = await CrearHostAsync(notificador: registro);
        if (fallaPersistencia)
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER req14_rechazo BEFORE INSERT ON LecturasPanel
                FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Fallo controlado req14'
                """);
        using var worker = ActivatorUtilities.CreateInstance<RecoleccionLecturasWorker>(app.Services);
        try
        {
            await worker.StartAsync(CancellationToken.None);
            if (fallaPersistencia)
            {
                await Task.Delay(1100);
                Assert.Equal(0, registro.Intentos);
                Assert.Equal(0, await context.LecturasPanel.CountAsync());
                await context.Database.ExecuteSqlRawAsync("DROP TRIGGER req14_rechazo");
            }
            await registro.Confirmadas.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(100);
            Assert.Equal(4, await context.LecturasPanel.CountAsync());
            Assert.False(worker.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS req14_rechazo");
        }
    }

    [MonitoreoNavegadorFact]
    public async Task Pantalla_ServidorTemporalParaVerificacionEnNavegador()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        await using var app = await CrearHostAsync(pagina: true);
        var segundos = int.TryParse(Environment.GetEnvironmentVariable("SOLARUY_MONITOREO_UI_SEGUNDOS"), out var valor) ? valor : 120;
        await Task.Delay(TimeSpan.FromSeconds(segundos));
        Assert.True(await context.LecturasPanel.CountAsync() >= 4);
    }

    [MariaDbHistoricoTheory]
    [InlineData("Administrador")]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task Hub_AutorizaSuscripcionEntregaSoloSuGrupoYPermiteReconectar(string rol)
    {
        await database.ReiniciarAsync();
        await using var app = await CrearHostAsync();
        using var cliente = Cliente(app, out var cookies);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsync("/hubs/monitoreo/negotiate?negotiateVersion=1", null)).StatusCode);
        await cliente.PostAsync($"/pruebas/ingresar/{rol}", null);
        using var socket = await ConectarAsync(app, cookies);
        await SuscribirAsync(socket, database.Parque.Id);
        var notificador = app.Services.GetRequiredService<INotificadorLecturasConfirmadas>();
        await notificador.NotificarAsync([new(database.OtroParque.Id, database.PanelAjeno.Id, DateTime.UtcNow)]);
        await notificador.NotificarAsync([new(database.Parque.Id, database.Panel.Id, DateTime.UtcNow)]);
        var aviso = await RecibirAsync(socket);
        Assert.Equal("LecturasConfirmadas", aviso.GetProperty("target").GetString());
        Assert.Equal(database.Parque.Id, aviso.GetProperty("arguments")[0].GetGuid());
        await SuscribirAsync(socket, database.OtroParque.Id);
        await notificador.NotificarAsync([new(database.Parque.Id, database.Panel.Id, DateTime.UtcNow)]);
        await notificador.NotificarAsync([new(database.OtroParque.Id, database.PanelAjeno.Id, DateTime.UtcNow)]);
        Assert.Equal(database.OtroParque.Id, (await RecibirAsync(socket)).GetProperty("arguments")[0].GetGuid());
        socket.Abort();
        using var reconectado = await ConectarAsync(app, cookies);
        await SuscribirAsync(reconectado, database.Parque.Id);
        await notificador.NotificarAsync([new(database.Parque.Id, database.Panel.Id, DateTime.UtcNow)]);
        Assert.Equal(database.Parque.Id, (await RecibirAsync(reconectado)).GetProperty("arguments")[0].GetGuid());
    }

    [MariaDbHistoricoFact]
    public async Task Hub_RechazaRolAjenoYParqueInexistenteSinExponerEscrituras()
    {
        await database.ReiniciarAsync();
        await using var app = await CrearHostAsync();
        using var cliente = Cliente(app, out var cookies);
        await cliente.PostAsync("/pruebas/ingresar/Invitado", null);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsync("/hubs/monitoreo/negotiate?negotiateVersion=1", null)).StatusCode);
        await cliente.PostAsync("/pruebas/ingresar/Consulta", null);
        using var socket = await ConectarAsync(app, cookies);
        await EnviarAsync(socket, JsonSerializer.Serialize(new { type = 1, invocationId = "1", target = "SuscribirParque", arguments = new[] { Guid.NewGuid() } }));
        Assert.True((await RecibirAsync(socket)).TryGetProperty("error", out _));
        var metodos = typeof(MonitoreoHub).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "DesuscribirParque", "SuscribirParque" }, metodos.Select(m => m.Name).Order().ToArray());
    }

    [MariaDbHistoricoFact]
    public async Task Worker_NotificaSoloDespuesDeQueLasLecturasSeHanConfirmado()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        await using var app = await CrearHostAsync();
        using var cliente = Cliente(app, out var cookies);
        await cliente.PostAsync("/pruebas/ingresar/Consulta", null);
        using var socket = await ConectarAsync(app, cookies);
        await SuscribirAsync(socket, database.Parque.Id);
        using var worker = ActivatorUtilities.CreateInstance<RecoleccionLecturasWorker>(app.Services);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            var aviso = await RecibirAsync(socket);
            Assert.Equal(database.Parque.Id, aviso.GetProperty("arguments")[0].GetGuid());
            Assert.True(await context.LecturasPanel.AnyAsync(l => l.PanelId == database.Panel.Id));
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }

    [MariaDbHistoricoFact]
    public async Task Hub_CierraLaConexionCuandoExpiraLaCookie()
    {
        await using var app = await CrearHostAsync();
        using var cliente = Cliente(app, out var cookies);
        await cliente.PostAsync("/pruebas/ingresar/Consulta?segundos=2", null);
        using var socket = await ConectarAsync(app, cookies);
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open)
        {
            var recibido = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), limite.Token);
            if (recibido.MessageType == WebSocketMessageType.Close) break;
        }
        Assert.False(limite.IsCancellationRequested);
    }

    private async Task<WebApplication> CrearHostAsync(bool pagina = false, INotificadorLecturasConfirmadas? notificador = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = pagina ? typeof(SolarUY.Presentation.Components.App).Assembly.GetName().Name : null,
            ContentRootPath = pagina ? HistoricoDatabase.RutaProyecto("SolarUY.Presentation") : null
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, pagina ? 5067 : 0));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion) });
        builder.Services.AddSolarInfrastructure(builder.Configuration);
        builder.Services.AddSignalR();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<AccesoMonitoreoService>();
        builder.Services.AddSingleton<INotificadorLecturasConfirmadas, NotificadorLecturasConfirmadas>();
        if (notificador is not null) builder.Services.AddSingleton(notificador);
        builder.Services.AddScoped<IRecoleccionLecturasService, RecoleccionLecturasService>();
        builder.Services.AddSingleton(new ConfiguracionRecoleccion(pagina ? 2 : 60));
        builder.Services.Configure<RecoleccionLecturasOptions>(o => o.IntervaloSegundos = pagina ? 2 : 1);
        if (pagina)
        {
            builder.WebHost.UseStaticWebAssets();
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IUsuarioActual, UsuarioActualService>();
            builder.Services.AddScoped<IMonitoreoService, MonitoreoService>();
            builder.Services.AddSingleton<GeneracionActualService>();
            builder.Services.AddSingleton(new PoliticaMonitoreo(2, 2));
            builder.Services.AddScoped(_ => new SerieMonitoreoService(120, TimeSpan.FromSeconds(2)));
            builder.Services.Configure<MonitoreoOptions>(o => o.ReevaluacionSegundos = 1);
            builder.Services.AddRadzenComponents();
            builder.Services.AddHostedService<RecoleccionLecturasWorker>();
        }
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.Cookie.Name = ".SolarUY.Monitoreo.Pruebas";
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        var app = builder.Build();
        if (pagina) app.UseStaticFiles();
        app.UseAuthentication(); app.UseAuthorization();
        if (pagina) app.UseAntiforgery();
        // Emisor de cookies exclusivo del servidor de pruebas; no existe en Presentation.
        app.MapMethods("/pruebas/ingresar/{rol}", ["POST", "GET"], async (HttpContext context, string rol) =>
        {
            var identidad = new ClaimsIdentity([new Claim("sub", "usuario-prueba"), new Claim(ClaimTypes.Role, rol)], CookieAuthenticationDefaults.AuthenticationScheme);
            var segundos = int.TryParse(context.Request.Query["segundos"], out var valor) ? valor : 300;
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad),
                new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(segundos), AllowRefresh = false });
            if (context.Request.Method == "GET") context.Response.Redirect("/monitoreo");
        }).AllowAnonymous();
        app.MapHub<MonitoreoHub>("/hubs/monitoreo", o => o.CloseOnAuthenticationExpiration = true);
        if (pagina) app.MapRazorComponents<SolarUY.Presentation.Components.App>().AddInteractiveServerRenderMode();
        await app.StartAsync();
        return app;
    }

    private static HttpClient Cliente(WebApplication app, out CookieContainer cookies)
    {
        cookies = new CookieContainer();
        return new HttpClient(new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false })
        { BaseAddress = new Uri(app.Urls.Single()) };
    }

    private static async Task<ClientWebSocket> ConectarAsync(WebApplication app, CookieContainer cookies)
    {
        var socket = new ClientWebSocket(); socket.Options.Cookies = cookies;
        var direccion = new UriBuilder(app.Urls.Single()) { Scheme = "ws", Path = "/hubs/monitoreo" }.Uri;
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await socket.ConnectAsync(direccion, limite.Token);
            await EnviarAsync(socket, "{\"protocol\":\"json\",\"version\":1}");
            Assert.Equal("{}", (await RecibirAsync(socket)).GetRawText());
            return socket;
        }
        catch { socket.Dispose(); throw; }
    }

    private static async Task SuscribirAsync(ClientWebSocket socket, Guid parqueId)
    {
        await EnviarAsync(socket, JsonSerializer.Serialize(new { type = 1, invocationId = "1", target = "SuscribirParque", arguments = new[] { parqueId } }));
        var respuesta = await RecibirAsync(socket);
        Assert.Equal(3, respuesta.GetProperty("type").GetInt32());
        Assert.False(respuesta.TryGetProperty("error", out _), respuesta.GetRawText());
    }

    private static Task EnviarAsync(ClientWebSocket socket, string mensaje)
        => socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(mensaje + '\u001e')), WebSocketMessageType.Text, true, CancellationToken.None);

    private static async Task<JsonElement> RecibirAsync(ClientWebSocket socket)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var texto = new StringBuilder(); var buffer = new byte[8192];
        while (true)
        {
            var recibido = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), limite.Token);
            Assert.NotEqual(WebSocketMessageType.Close, recibido.MessageType);
            texto.Append(Encoding.UTF8.GetString(buffer, 0, recibido.Count));
            if (!recibido.EndOfMessage) continue;
            foreach (var mensaje in texto.ToString().Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var documento = JsonDocument.Parse(mensaje);
                if (documento.RootElement.TryGetProperty("type", out var tipo) && tipo.GetInt32() == 6) continue;
                return documento.RootElement.Clone();
            }
            texto.Clear();
        }
    }

    private sealed class RegistroNotificaciones : INotificadorLecturasConfirmadas
    {
        public bool Fallar { get; init; }
        private int _intentos;
        public int Intentos => Volatile.Read(ref _intentos);
        public TaskCompletionSource Confirmadas { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task NotificarAsync(IReadOnlyList<LecturaConfirmadaDto> lecturas, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _intentos); Confirmadas.TrySetResult();
            return Fallar ? Task.FromException(new InvalidOperationException("Fallo controlado del aviso local.")) : Task.CompletedTask;
        }
    }
}

public sealed class MonitoreoNavegadorFactAttribute : FactAttribute
{
    public MonitoreoNavegadorFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOLARUY_MONITOREO_UI_INSPECCION") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Verificación manual: habilitar SOLARUY_MONITOREO_UI_INSPECCION=1 con una MariaDB aislada.";
    }
}
