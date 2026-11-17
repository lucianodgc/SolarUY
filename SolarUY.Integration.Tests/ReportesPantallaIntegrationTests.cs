using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;
using SolarUY.Application.Abstractions;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.Reportes;
using SolarUY.Domain.Entities;
using SolarUY.Infrastructure;
using SolarUY.Presentation.Authentication;
using SolarUY.Presentation.Components;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class ReportesPantallaIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoTheory]
    [InlineData("Administrador")]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task Pagina_AutorizaReportesYEntregaLosRecursosDeGraficoYDescarga(string rol)
    {
        await PrepararDatosAsync();
        await using var app = CrearHost(0, new TaskCompletionSource());
        await app.StartAsync();
        using var cliente = CrearCliente(app);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsync($"/pruebas/ingresar/{rol}", null)).StatusCode);
        var respuesta = await cliente.GetAsync("/reportes");
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Filtros del reporte", html);
        Assert.Contains("Exportar CSV", html);
        Assert.Contains("Gráfico de generación", html);
        Assert.Contains("Energía observada", html);
        Assert.Contains("Mantenimientos realizados", html);
        Assert.Contains("America/Montevideo", html);
        foreach (var recurso in new[] { "/js/reportes.js", "/_framework/blazor.web.js",
                     "/_content/Radzen.Blazor/Radzen.Blazor.js", "/_content/Radzen.Blazor/css/material-base.css" })
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(recurso)).StatusCode);
    }

    [MariaDbHistoricoFact]
    public async Task Pagina_DeniegaElAccesoAnonimoYLosRolesAjenos()
    {
        await using var app = CrearHost(0, new TaskCompletionSource());
        await app.StartAsync();
        using var cliente = CrearCliente(app);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/reportes")).StatusCode);
        await cliente.PostAsync("/pruebas/ingresar/Invitado", null);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/reportes")).StatusCode);
    }

    [ReportesNavegadorFact]
    public async Task Pagina_ServidorTemporalParaVerificacionEnNavegador()
    {
        await PrepararDatosAsync();
        var cierre = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = CrearHost(5215, cierre);
        await app.StartAsync();
        using var cliente = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5215") };
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/reportes")).StatusCode);
        var segundos = int.TryParse(Environment.GetEnvironmentVariable("SOLARUY_REPORTES_UI_SEGUNDOS"), out var valor)
            ? Math.Clamp(valor, 30, 1800) : 600;
        await cierre.Task.WaitAsync(TimeSpan.FromSeconds(segundos));
    }

    private async Task PrepararDatosAsync()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        context.AttachRange(database.Panel, database.SegundoPanel, database.TercerPanel, database.PanelAjeno);
        var dia = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Montevideo").Date,
            DateTimeKind.Unspecified);
        context.LecturasPanel.AddRange(
            HistoricoDatabase.Lectura(database.Panel, dia, 1200),
            HistoricoDatabase.Lectura(database.Panel, dia.AddMinutes(5), 0),
            HistoricoDatabase.Lectura(database.Panel, dia.AddHours(2), 1200),
            HistoricoDatabase.Lectura(database.Panel, dia.AddHours(4), 0),
            HistoricoDatabase.Lectura(database.SegundoPanel, dia, 2400),
            HistoricoDatabase.Lectura(database.TercerPanel, dia, 3600),
            HistoricoDatabase.Lectura(database.PanelAjeno, dia, 12000));
        var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(dia, TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo"));
        foreach (var panel in new[] { database.Panel, database.TercerPanel })
        {
            var tarea = new TareaMantenimiento(panel, "Limpieza", "Revisión; \"panel\" — ñ", inicioUtc.AddDays(-1), "tecnico-prueba");
            tarea.Iniciar();
            tarea.Completar(inicioUtc.AddHours(1));
            context.TareasMantenimiento.Add(tarea);
        }
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO LecturasPanel (Id, PanelId, NodoId, FechaHora, PotenciaGeneradaW, EstadoSalud)
            VALUES ({Guid.NewGuid()}, {database.Panel.Id}, NULL, {inicioUtc.AddHours(12)}, 9000, 0)
            """);
    }

    private static WebApplication CrearHost(int puerto, TaskCompletionSource cierre)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(App).Assembly.GetName().Name,
            ContentRootPath = HistoricoDatabase.RutaProyecto("SolarUY.Presentation")
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, puerto));
        builder.WebHost.UseStaticWebAssets();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)
        });
        builder.Services.AddSolarInfrastructure(builder.Configuration);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthorization();
        builder.Services.AddRadzenComponents();
        builder.Services.AddScoped<IUsuarioActual, UsuarioActualService>();
        builder.Services.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
        builder.Services.AddScoped<IReporteHistoricoService, ReporteHistoricoService>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = ".SolarUY.Reportes.Pruebas";
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        var app = builder.Build();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        // Emisor limitado al host de pruebas en loopback; no se añade a Presentation.
        app.MapMethods("/pruebas/ingresar/{rol}", ["GET", "POST"], async (HttpContext context, string rol) =>
        {
            if (rol is not ("Administrador" or "Tecnico" or "Consulta" or "Invitado"))
            {
                context.Response.StatusCode = 400;
                return;
            }
            var identidad = new ClaimsIdentity([new Claim("sub", "usuario-prueba"), new Claim(ClaimTypes.Name, rol),
                new Claim(ClaimTypes.Role, rol)], CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad),
                new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30), AllowRefresh = false });
            if (context.Request.Method == "GET") context.Response.Redirect("/reportes");
            else context.Response.StatusCode = 204;
        }).AllowAnonymous();
        app.MapGet("/pruebas/finalizar", (HttpContext context) =>
        {
            context.Response.OnCompleted(() => { cierre.TrySetResult(); return Task.CompletedTask; });
            return Results.Text("Servidor de verificación finalizado.");
        }).AllowAnonymous();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }

    private static HttpClient CrearCliente(WebApplication app)
        => new(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
        { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(15) };
}

public sealed class ReportesNavegadorFactAttribute : FactAttribute
{
    public ReportesNavegadorFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOLARUY_REPORTES_UI_INSPECCION") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Inspección en navegador: habilitar SOLARUY_REPORTES_UI_INSPECCION=1 y una conexión MariaDB aislada.";
    }
}
