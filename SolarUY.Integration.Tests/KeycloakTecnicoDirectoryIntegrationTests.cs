using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using SolarUY.Application.Exceptions;
using SolarUY.Infrastructure.Identity;

namespace SolarUY.Integration.Tests;

public sealed class KeycloakTecnicoDirectoryIntegrationTests
{
    [Fact]
    public async Task DirectorioPaginaYDevuelveSoloTecnicosHabilitadosSinDuplicados()
    {
        var paginaCompleta = "[" + string.Join(",", Enumerable.Repeat(
            """{"id":"origen","username":"Zoe","enabled":true}""", 100)) + "]";
        var consultas = new List<string>();
        using var cliente = CrearCliente(solicitud =>
        {
            var ruta = solicitud.RequestUri!.PathAndQuery;
            if (solicitud.Method == HttpMethod.Post)
                return ResponderToken(solicitud, """{"access_token":"token-prueba"}""");
            Assert.Equal("token-prueba", solicitud.Headers.Authorization?.Parameter);
            if (ruta.Contains("role-mappings", StringComparison.Ordinal))
                return Json(ruta.Contains("invitado", StringComparison.Ordinal) ? "[]" : """[{"name":"Tecnico"}]""");
            consultas.Add(ruta);
            return Json(ruta.Contains("first=0", StringComparison.Ordinal) ? paginaCompleta : """
                [{"id":"origen","username":"Zoe","enabled":true},
                 {"id":"a","enabled":true},{"id":"invitado","enabled":true},
                 {"id":"bloqueado","enabled":false},{"id":"","enabled":true}]
                """);
        });
        var directorio = new KeycloakTecnicoDirectory(cliente, Configuracion());

        var tecnicos = await directorio.ListarAsync();

        Assert.Equal(["a", "origen"], tecnicos.Select(t => t.Id));
        Assert.Equal(["a", "Zoe"], tecnicos.Select(t => t.Nombre));
        Assert.Collection(consultas,
            ruta => Assert.EndsWith("first=0&max=100", ruta),
            ruta => Assert.EndsWith("first=100&max=100", ruta));
    }

    [Theory]
    [InlineData("tecnico", """{"id":"tecnico","enabled":true}""", """[{"name":"Tecnico"}]""", true)]
    [InlineData("tecnico", """{"id":"tecnico","enabled":true}""", """[{"name":"Consulta"}]""", false)]
    [InlineData("bloqueado", """{"id":"bloqueado","enabled":false}""", "[]", false)]
    [InlineData("ajeno", """{"id":"otro","enabled":true}""", "[]", false)]
    [InlineData("ausente", null, "[]", false)]
    public async Task ValidacionIndividualExigeIdentidadHabilitadaConRol(string id, string? usuario, string roles, bool habilitado)
    {
        using var cliente = CrearCliente(solicitud =>
        {
            if (solicitud.Method == HttpMethod.Post) return Json("""{"access_token":"token-prueba"}""");
            if (solicitud.RequestUri!.AbsolutePath.EndsWith("/composite", StringComparison.Ordinal)) return Json(roles);
            return usuario is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(usuario);
        });
        var directorio = new KeycloakTecnicoDirectory(cliente, Configuracion());

        Assert.Equal(habilitado, await directorio.EsTecnicoHabilitadoAsync(id));
        Assert.False(await directorio.EsTecnicoHabilitadoAsync(" "));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "no está autorizado")]
    [InlineData(HttpStatusCode.Forbidden, "no está autorizado")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "no pudo atender")]
    public async Task DirectorioExplicaRechazosDelProveedor(HttpStatusCode estado, string mensaje)
    {
        using var cliente = CrearCliente(_ => new HttpResponseMessage(estado));
        var directorio = new KeycloakTecnicoDirectory(cliente, Configuracion());

        var error = await Assert.ThrowsAsync<TecnicoDirectoryException>(() => directorio.ListarAsync());
        Assert.Contains(mensaje, error.Message);
    }

    [Theory]
    [InlineData("{}", "token válido")]
    [InlineData("no es json", "No se pudo consultar")]
    [InlineData("""{"access_token":42}""", "token válido")]
    public async Task DirectorioRechazaUnaRespuestaDeTokenInvalida(string cuerpo, string mensaje)
    {
        using var cliente = CrearCliente(_ => Json(cuerpo));
        var directorio = new KeycloakTecnicoDirectory(cliente, Configuracion());

        Assert.Contains(mensaje, (await Assert.ThrowsAsync<TecnicoDirectoryException>(() => directorio.ListarAsync())).Message);
    }

    [Theory]
    [InlineData("http://directorio.invalid")]
    [InlineData("https://directorio.invalid/?extra=1")]
    [InlineData("https://directorio.invalid/#detalle")]
    [InlineData("no es una url")]
    public async Task DirectorioRechazaConfiguracionInvalidaAntesDeEnviarSolicitudes(string direccion)
    {
        using var cliente = CrearCliente(_ => throw new InvalidOperationException("No se esperaba una solicitud HTTP."));
        var directorio = new KeycloakTecnicoDirectory(cliente, Configuracion(direccion));

        await Assert.ThrowsAsync<TecnicoDirectoryException>(() => directorio.ListarAsync());
    }

    private static HttpResponseMessage ResponderToken(HttpRequestMessage solicitud, string cuerpo)
    {
        Assert.EndsWith("/realms/solaruy/protocol/openid-connect/token", solicitud.RequestUri?.AbsolutePath);
        Assert.Equal("application/x-www-form-urlencoded", solicitud.Content?.Headers.ContentType?.MediaType);
        return Json(cuerpo);
    }

    private static IConfiguration Configuracion(string direccion = "https://directorio.invalid")
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:Directory:BaseUrl"] = direccion,
            ["Keycloak:Directory:Realm"] = "solaruy",
            ["Keycloak:Directory:ClientId"] = "directorio-pruebas",
            ["Keycloak:Directory:ClientSecret"] = "valor-aislado"
        }).Build();

    private static HttpClient CrearCliente(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new(new TransportePruebas(responder));

    private static HttpResponseMessage Json(string contenido) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(contenido, Encoding.UTF8, "application/json")
    };

    private sealed class TransportePruebas(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responder(request));
        }
    }
}
