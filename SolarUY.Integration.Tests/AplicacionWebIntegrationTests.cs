using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SolarUY.Presentation.Components;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class AplicacionWebIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task ArranqueRealSirveLasPantallasSegunElRol()
    {
        await database.ReiniciarAsync();
        await using var app = CrearAplicacion();
        await app.StartAsync();
        using var admin = CrearCliente(app);
        var salud = await admin.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, salud.StatusCode);
        Assert.Equal("prueba-integracion", Assert.Single(salud.Headers.GetValues("X-SolarUY-Instancia")));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/Account/Logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await admin.PostAsync("/hubs/monitoreo/negotiate?negotiateVersion=1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/pruebas/sesion/Administrador", null)).StatusCode);

        var rutas = new Dictionary<string, string>
        {
            ["/parques-solares"] = database.Parque.Nombre,
            ["/parques-solares/alta"] = "Registrar parque",
            [$"/parques-solares/{database.Parque.Id}/editar"] = "Guardar cambios",
            [$"/parques-solares/{database.Parque.Id}/nodos"] = database.Panel.Nodo.Nombre,
            [$"/parques-solares/{database.Parque.Id}/nodos/alta"] = "Registrar nodo",
            [$"/nodos/{database.Panel.NodoId}/editar"] = "Editar nodo",
            ["/paneles"] = database.Panel.NumeroSerie,
            ["/paneles/alta"] = "Registrar panel",
            [$"/paneles/{database.Panel.Id}/editar"] = "Editar panel",
            ["/mantenimiento"] = "Crear tarea",
            ["/mantenimiento/crear"] = "Técnico responsable",
            ["/simulador"] = "Simulador de inversor",
            ["/monitoreo"] = "Monitoreo de generación",
            ["/reportes"] = "Filtros del reporte"
        };
        foreach (var (ruta, texto) in rutas)
            Assert.True((await ObtenerTextoAsync(admin, ruta)).Contains(texto, StringComparison.Ordinal),
                $"GET {ruta} no contiene «{texto}».");

        using var consulta = CrearCliente(app);
        Assert.Equal(HttpStatusCode.NoContent, (await consulta.PostAsync("/pruebas/sesion/Consulta", null)).StatusCode);
        Assert.DoesNotContain("Registrar parque", await ObtenerTextoAsync(consulta, "/parques-solares"));
        Assert.DoesNotContain("Crear tarea", await ObtenerTextoAsync(consulta, "/"));
        var rechazada = await consulta.GetAsync("/mantenimiento/crear");
        Assert.Equal(HttpStatusCode.Redirect, rechazada.StatusCode);
        Assert.Contains("/Account/AccessDenied", rechazada.Headers.Location?.AbsolutePath);
    }

    [MariaDbHistoricoFact]
    public async Task Autenticacion_UsaOidcParaIngresarYExigeAntifalsificacionParaSalir()
    {
        await using var app = CrearAplicacion();
        await app.StartAsync();
        using var cliente = CrearCliente(app);
        var desafio = await cliente.GetAsync("/mi-cuenta");
        Assert.Equal(HttpStatusCode.Redirect, desafio.StatusCode);
        Assert.Equal("/oidc/authorize", desafio.Headers.Location?.AbsolutePath);
        Assert.Contains("response_type=code", desafio.Headers.Location?.Query);

        var fallida = await cliente.GetAsync("/signin-oidc?error=access_denied");
        Assert.Equal("/inicio-sesion-incompleto", fallida.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsync("/pruebas/sesion/Tecnico", null)).StatusCode);
        var html = await ObtenerTextoAsync(cliente, "/");
        var token = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)" """.Trim(),
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.True(token.Success);
        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token.Groups[1].Value
        });
        var cierre = await cliente.PostAsync("/Account/Logout", formulario);
        Assert.Equal(HttpStatusCode.Redirect, cierre.StatusCode);
        Assert.Equal("/oidc/logout", cierre.Headers.Location?.AbsolutePath);
        Assert.Equal("/oidc/authorize", (await cliente.GetAsync("/mi-cuenta")).Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("""{"roles":false}""", false)]
    [InlineData("""{"roles":["Tecnico","Tecnico",42,"Consulta"]}""", true)]
    public async Task Autenticacion_ImportaSoloRolesDeTextoSinDuplicados(string? realmAccess, bool agregaRoles)
    {
        await using var app = CrearAplicacion();
        var nombre = OpenIdConnectDefaults.AuthenticationScheme;
        var opciones = app.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(nombre);
        var identidad = new ClaimsIdentity([new Claim(ClaimTypes.Role, "Tecnico")], nombre);
        if (realmAccess is not null) identidad.AddClaim(new Claim("realm_access", realmAccess));
        var estado = new TokenValidatedContext(new DefaultHttpContext(),
            new AuthenticationScheme(nombre, null, typeof(OpenIdConnectHandler)), opciones,
            new ClaimsPrincipal(identidad), new AuthenticationProperties());

        await opciones.Events.TokenValidated(estado);

        Assert.Single(identidad.FindAll(ClaimTypes.Role), rol => rol.Value == "Tecnico");
        Assert.Equal(agregaRoles, identidad.HasClaim(ClaimTypes.Role, "Consulta"));
        Assert.DoesNotContain(identidad.FindAll(ClaimTypes.Role), rol => rol.Value == "42");
    }

    private WebApplication CrearAplicacion()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(App).Assembly.GetName().Name,
            ContentRootPath = HistoricoDatabase.RutaProyecto("SolarUY.Presentation")
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
            {
                AuthorizationEndpoint = "http://localhost/oidc/authorize",
                EndSessionEndpoint = "http://localhost/oidc/logout"
            }));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SolarUY"] = database.ConexionConfigurada,
            ["Instancia:Id"] = "prueba-integracion",
            ["RecoleccionLecturas:Habilitada"] = "false",
            ["SaludPanel:Habilitada"] = "false",
            ["SaludDistribucion:Habilitada"] = "false",
            ["Keycloak:Directory:ClientSecret"] = null
        });
        var app = Program.CrearAplicacion(builder);
        app.MapPost("/pruebas/sesion/{rol}", AbrirSesionAsync).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> AbrirSesionAsync(HttpContext context, string rol)
    {
        var identidad = new ClaimsIdentity([new Claim("sub", "usuario-prueba"),
            new Claim(ClaimTypes.Name, "usuario-prueba"), new Claim(ClaimTypes.Role, rol)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));
        return Results.NoContent();
    }

    private static async Task<string> ObtenerTextoAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.True(respuesta.IsSuccessStatusCode, $"GET {ruta} devolvió {(int)respuesta.StatusCode}.");
        return html;
    }

    private static HttpClient CrearCliente(WebApplication app)
        => new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
}
