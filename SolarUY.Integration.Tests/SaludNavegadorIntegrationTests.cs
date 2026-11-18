using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Monitoreo;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Application.Salud;
using SolarUY.Domain.Monitoreo;
using SolarUY.Domain.Salud;
using SolarUY.Infrastructure;
using SolarUY.Infrastructure.Inversores;
using SolarUY.Presentation.Authentication;
using SolarUY.Presentation.Monitoreo;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class SaludNavegadorIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [SaludNavegadorFact]
    public async Task ServidorTemporalSaludParaPlaywright()
    {
        await database.ReiniciarAsync();
        await using (var db = database.CrearContexto()) await db.LecturasPanel.ExecuteDeleteAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development", ApplicationName = typeof(SolarUY.Presentation.Components.App).Assembly.GetName().Name,
            ContentRootPath = HistoricoDatabase.RutaProyecto("SolarUY.Presentation")
        });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 5069));
        builder.WebHost.UseStaticWebAssets();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
        { ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion) });
        builder.Services.AddSolarInfrastructure(builder.Configuration);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState(); builder.Services.AddAuthorization(); builder.Services.AddRadzenComponents();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddSignalR(); builder.Services.AddScoped<IUsuarioActual, UsuarioActualService>();
        builder.Services.AddScoped<SolarUY.Application.Inversores.IInversorService, SolarUY.Application.Inversores.InversorService>();
        builder.Services.AddScoped<SolarUY.Application.Inversores.IOrdenInversorService, SolarUY.Application.Inversores.OrdenInversorService>();
        builder.Services.AddSingleton(new SolarUY.Application.Inversores.OrdenInversorOptions());
        builder.Services.AddScoped<AccesoMonitoreoService>(); builder.Services.AddScoped<IMonitoreoService, MonitoreoService>();
        builder.Services.AddSingleton<GeneracionActualService>(); builder.Services.AddSingleton(new PoliticaMonitoreo(1, 2));
        builder.Services.AddSingleton(new ConfiguracionRecoleccion(1));
        builder.Services.AddScoped(_ => new SerieMonitoreoService(3, TimeSpan.FromSeconds(1)));
        builder.Services.Configure<MonitoreoOptions>(o => o.ReevaluacionSegundos = 1);
        builder.Services.AddSingleton<EvaluacionSaludService>(); builder.Services.AddSingleton<SaludNodoService>();
        builder.Services.AddSingleton(new PoliticaSalud(TimeSpan.FromSeconds(5)));
        builder.Services.AddSingleton(new ConfiguracionConsultaSalud(TimeSpan.FromSeconds(3)));
        builder.Services.AddScoped<ISaludPanelService, SaludPanelService>(); builder.Services.AddScoped<IConsultaSaludService, ConsultaSaludService>();
        builder.Services.AddSingleton<INotificadorSaludConfirmada, NotificadorSaludConfirmada>();
        builder.Services.Configure<SaludPanelOptions>(o => o.EvaluacionSegundos = 1);
        builder.Services.AddHostedService<SaludPanelWorker>();
        await using var app = builder.Build();
        app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
        var fin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Rutas de control exclusivas de este servidor de pruebas, nunca de Presentation.
        app.MapPost("/pruebas/ingresar/{rol}", async (HttpContext ctx, string rol) =>
        {
            var identidad = new ClaimsIdentity([new Claim("sub", "prueba-salud"), new Claim(ClaimTypes.Role, rol)], CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad),
                new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) });
        });
        app.MapGet("/pruebas/datos", () => new { parque = database.Parque.Id, otro = database.OtroParque.Id });
        app.MapPost("/pruebas/medir/{falla:bool}", async (bool falla, IRecoleccionLecturasRepository repository) =>
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["InversorSimulador:PanelesConFalla"] = falla ? database.Panel.NumeroSerie : "" }).Build();
            var recolector = new RecoleccionLecturasService(repository, new InversorSimulador(config), TimeProvider.System, new ConfiguracionRecoleccion(1));
            return await recolector.RecolectarAsync();
        });
        app.MapPost("/pruebas/terminar", () => { fin.TrySetResult(); return Results.Ok(); });
        app.MapHub<MonitoreoHub>("/hubs/monitoreo", o => o.CloseOnAuthenticationExpiration = true);
        app.MapRazorComponents<SolarUY.Presentation.Components.App>().AddInteractiveServerRenderMode();
        await app.StartAsync();
        await fin.Task.WaitAsync(TimeSpan.FromMinutes(3));
        await app.StopAsync();
        await using var final = database.CrearContexto();
        Assert.False(await final.Set<CambioEstadoSaludPanel>().GroupBy(c => new { c.PanelId, c.Version }).AnyAsync(g => g.Count() > 1));
    }
}
public sealed class SaludNavegadorFactAttribute : FactAttribute
{
    public SaludNavegadorFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOLARUY_SALUD_BROWSER") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Servidor temporal para la suite aislada de navegador de req016.";
    }
}
