using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolarUY.Application.Chatbot;
using SolarUY.Infrastructure.Chatbot;
using SolarUY.Presentation.Components.Pages;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class ChatbotPanelIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoTheory]
    [InlineData("Administrador")]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task Chatbot_PaginaHttpProtegidaYRecursosRadzen(string rol)
    {
        await using var app = ChatbotPruebasHost.Crear();
        await app.StartAsync();
        using var cliente = Cliente(app.Urls.Single());
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsync("/pruebas/ingresar/" + rol, null)).StatusCode);
        var respuesta = await cliente.GetAsync("/chatbot");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains("Asistente de SolarUY", html);
        Assert.Contains("Enviar pregunta", html);
        Assert.Contains("solo consulta información", html);
        Assert.DoesNotContain("sk-ficticia", html);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/_framework/blazor.web.js")).StatusCode);
    }

    [MariaDbHistoricoFact]
    public async Task Chatbot_PaginaHttpRechazaAnonimoYRolAjeno()
    {
        await using var app = ChatbotPruebasHost.Crear();
        await app.StartAsync();
        using var cliente = Cliente(app.Urls.Single());
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/chatbot")).StatusCode);
        await cliente.PostAsync("/pruebas/ingresar/Invitado", null);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/chatbot")).StatusCode);
    }

    [MariaDbHistoricoFact]
    public async Task Chatbot_PanelRealEnviaPorMcpEscapaHtmlYMuestraErroresSeguros()
    {
        await database.ReiniciarAsync();
        var captura = new CapturaChatbot();
        await using var app = ChatbotPruebasHost.Crear(configurar: s => s.AddSingleton<IComponentActivator>(captura));
        await using var scope = app.Services.CreateAsyncScope();
        var autenticacion = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        ((IHostEnvironmentAuthenticationStateProvider)autenticacion).SetAuthenticationState(Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new("sub", "tecnico-uno"),
                new(ClaimTypes.Role, "Administrador")], "prueba")))));
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var raiz = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Chatbot>());
        var pagina = captura.Pagina!;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            Establecer(pagina, "_pregunta", "¿Qué parques hay? <script>alert('prueba')</script>");
            await InvocarAsync(pagina, "EnviarAsync");
        });
        var html = await renderer.Dispatcher.InvokeAsync(raiz.ToHtmlString);
        Assert.Contains("Parque histórico", WebUtility.HtmlDecode(html));
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert", html);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            Establecer(pagina, "_pregunta", "Quiero probar un error controlado");
            await InvocarAsync(pagina, "EnviarAsync");
        });
        var conError = await renderer.Dispatcher.InvokeAsync(raiz.ToHtmlString);
        Assert.Contains("no está disponible", WebUtility.HtmlDecode(conError));
        Assert.DoesNotContain("sk-ficticia", conError);
    }

    [ChatbotNavegadorFact]
    public async Task Chatbot_ServidorTemporalParaVerificacionEnNavegador()
    {
        await database.ReiniciarAsync();
        var cierre = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = ChatbotPruebasHost.Crear(5217, cierre);
        await app.StartAsync();
        using var cliente = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5217") };
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/chatbot")).StatusCode);
        await cierre.Task.WaitAsync(TimeSpan.FromMinutes(10));
    }

    [MariaDbHistoricoFact]
    public async Task Chatbot_ConversacionSobreviveAlSalirYVolverYNuevaConversacionLaLimpia()
    {
        await database.ReiniciarAsync();
        var captura = new CapturaChatbot();
        await using var app = ChatbotPruebasHost.Crear(configurar: s => s.AddSingleton<IComponentActivator>(captura));
        await using var scope = app.Services.CreateAsyncScope();
        Autenticar(scope.ServiceProvider);
        var servicio = scope.ServiceProvider.GetRequiredService<IChatbotService>();
        await using (var primero = CrearRenderer(scope.ServiceProvider))
        {
            await primero.Dispatcher.InvokeAsync(() => primero.RenderComponentAsync<Chatbot>());
            var pagina = captura.Pagina!;
            foreach (var texto in new[] { "hola", "¿Qué parques hay? <script>alert('prueba')</script>",
                         "Desactivá el panel 3", "Quiero probar un error controlado" })
                await primero.Dispatcher.InvokeAsync(async () =>
                { Establecer(pagina, "_pregunta", texto); await InvocarAsync(pagina, "EnviarAsync"); });
        }
        var conversacion = await servicio.ObtenerConversacionAsync();
        Assert.Equal(8, conversacion.Count);
        await using (var otroApartado = CrearRenderer(scope.ServiceProvider))
        {
            var perfil = await otroApartado.Dispatcher.InvokeAsync(() => otroApartado.RenderComponentAsync<MiCuenta>());
            Assert.Contains("Perfil y permisos", await otroApartado.Dispatcher.InvokeAsync(perfil.ToHtmlString));
        }
        await using var segundo = CrearRenderer(scope.ServiceProvider);
        var raiz = await segundo.Dispatcher.InvokeAsync(() => segundo.RenderComponentAsync<Chatbot>());
        var html = await segundo.Dispatcher.InvokeAsync(raiz.ToHtmlString);
        Assert.All(conversacion, m => Assert.Contains(m.Texto, WebUtility.HtmlDecode(html)));
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("sk-ficticia", html);
        await segundo.Dispatcher.InvokeAsync(() => InvocarAsync(captura.Pagina!, "ReiniciarAsync"));
        Assert.Empty(await servicio.ObtenerConversacionAsync());
        Assert.Contains("Podés empezar", WebUtility.HtmlDecode(await segundo.Dispatcher.InvokeAsync(raiz.ToHtmlString)));
    }

    [MariaDbHistoricoTheory]
    [InlineData("otro-usuario", "Administrador", true)]
    [InlineData("tecnico-uno", "Consulta", true)]
    [InlineData(null, null, false)]
    public async Task Chatbot_ReabrirConIdentidadOPermisosDistintosDescartaConversacion(string? id, string? rol, bool autenticado)
    {
        await using var app = ChatbotPruebasHost.Crear();
        await using var scope = app.Services.CreateAsyncScope();
        Autenticar(scope.ServiceProvider);
        var servicio = scope.ServiceProvider.GetRequiredService<IChatbotService>();
        await servicio.EnviarPreguntaAsync(new("hola"));
        Assert.Equal(2, (await servicio.ObtenerConversacionAsync()).Count);
        Autenticar(scope.ServiceProvider, id, rol, autenticado);
        await using var renderer = CrearRenderer(scope.ServiceProvider);
        var raiz = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Chatbot>());
        Assert.DoesNotContain("¡Hola!", WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(raiz.ToHtmlString)));
        Assert.Empty(await servicio.ObtenerConversacionAsync());
    }

    [MariaDbHistoricoFact]
    public async Task Chatbot_CircuitosSeparadosNoCompartenConversacionNiReferenciasMutables()
    {
        await using var app = ChatbotPruebasHost.Crear();
        await using var primero = app.Services.CreateAsyncScope();
        await using var segundo = app.Services.CreateAsyncScope();
        Autenticar(primero.ServiceProvider);
        Autenticar(segundo.ServiceProvider);
        var conversacionUno = primero.ServiceProvider.GetRequiredService<IChatbotService>();
        var conversacionDos = segundo.ServiceProvider.GetRequiredService<IChatbotService>();
        await conversacionUno.EnviarPreguntaAsync(new("hola"));
        Assert.Empty(await conversacionDos.ObtenerConversacionAsync());
        var copia = Assert.IsType<MensajeChatDto[]>(await conversacionUno.ObtenerConversacionAsync());
        copia[0] = new(true, "Mensaje alterado en la copia");
        Assert.Equal("hola", (await conversacionUno.ObtenerConversacionAsync())[0].Texto);
    }

    [MariaDbHistoricoFact]
    public async Task Chatbot_SalirCancelaElTurnoSinBorrarLaConversacionAnterior()
    {
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var modelo = new ModeloChatbotControlado
        {
            AntesDeResponder = ct => { inicio.TrySetResult(); return Task.Delay(Timeout.Infinite, ct); }
        };
        var captura = new CapturaChatbot();
        await using var app = ChatbotPruebasHost.Crear(configurar: s =>
        { s.AddSingleton<IComponentActivator>(captura); s.AddSingleton<IModeloChatbotFactory>(modelo); });
        await using var scope = app.Services.CreateAsyncScope();
        Autenticar(scope.ServiceProvider);
        await using var renderer = CrearRenderer(scope.ServiceProvider);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Chatbot>());
        var pagina = captura.Pagina!;
        await renderer.Dispatcher.InvokeAsync(async () =>
        { Establecer(pagina, "_pregunta", "hola"); await InvocarAsync(pagina, "EnviarAsync"); });
        var envio = renderer.Dispatcher.InvokeAsync(async () =>
        {
            Establecer(pagina, "_pregunta", "Consulta en curso del Parque norte");
            await InvocarAsync(pagina, "EnviarAsync", renderizar: false);
        });
        await inicio.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await renderer.DisposeAsync();
        await envio.WaitAsync(TimeSpan.FromSeconds(10));
        var servicio = scope.ServiceProvider.GetRequiredService<IChatbotService>();
        var mensajes = await servicio.ObtenerConversacionAsync();
        Assert.Equal(2, mensajes.Count);
        Assert.Equal("hola", mensajes[0].Texto);
        await using var siguiente = CrearRenderer(scope.ServiceProvider);
        var raiz = await siguiente.Dispatcher.InvokeAsync(() => siguiente.RenderComponentAsync<Chatbot>());
        var html = WebUtility.HtmlDecode(await siguiente.Dispatcher.InvokeAsync(raiz.ToHtmlString));
        Assert.Contains("¡Hola!", html);
        Assert.DoesNotContain("Consulta en curso", html);
    }

    private static HtmlRenderer CrearRenderer(IServiceProvider servicios)
        => new(servicios, servicios.GetRequiredService<ILoggerFactory>());
    private static void Autenticar(IServiceProvider servicios, string? id = "tecnico-uno", string? rol = "Administrador", bool autenticado = true)
    {
        var identidad = autenticado
            ? new ClaimsIdentity([new("sub", id!), new(ClaimTypes.Role, rol!)], "prueba") : new ClaimsIdentity();
        ((IHostEnvironmentAuthenticationStateProvider)servicios.GetRequiredService<AuthenticationStateProvider>())
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identidad))));
    }

    private static HttpClient Cliente(string direccion) => new(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
        { BaseAddress = new(direccion), Timeout = TimeSpan.FromSeconds(15) };
    private static void Establecer(Chatbot pagina, string campo, object valor)
        => typeof(Chatbot).GetField(campo, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pagina, valor);
    private static async Task InvocarAsync(Chatbot pagina, string metodo, bool renderizar = true)
    {
        await (Task)typeof(Chatbot).GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(pagina, null)!;
        // Completar el ciclo de render que normalmente ejecuta EventCallback.
        if (renderizar) typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(pagina, null);
    }
    private sealed class CapturaChatbot : IComponentActivator
    {
        public Chatbot? Pagina { get; private set; }
        public IComponent CreateInstance(Type componentType)
        {
            var componente = (IComponent)Activator.CreateInstance(componentType)!;
            if (componente is Chatbot chatbot) Pagina = chatbot;
            return componente;
        }
    }
}

public sealed class ChatbotNavegadorFactAttribute : FactAttribute
{
    public ChatbotNavegadorFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOLARUY_CHATBOT_UI") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Activar SOLARUY_CHATBOT_UI=1 y la conexión aislada para el recorrido en navegador.";
    }
}
