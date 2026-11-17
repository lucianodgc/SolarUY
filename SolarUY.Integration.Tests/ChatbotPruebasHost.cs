using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Chatbot;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.Mantenimiento;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Monitoreo;
using SolarUY.Infrastructure;
using SolarUY.Infrastructure.Chatbot;
using SolarUY.Presentation.Authentication;
using SolarUY.Presentation.Components;

namespace SolarUY.Integration.Tests;

internal static class ChatbotPruebasHost
{
    public static WebApplication Crear(int puerto = 0, TaskCompletionSource? cierre = null,
        Action<IServiceCollection>? configurar = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development", ApplicationName = typeof(App).Assembly.GetName().Name,
            ContentRootPath = HistoricoDatabase.RutaProyecto("SolarUY.Presentation")
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, puerto));
        builder.WebHost.UseStaticWebAssets();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion) });
        builder.Services.AddSolarInfrastructure(builder.Configuration);
        builder.Services.AddChatbotInfrastructure(builder.Configuration);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthorization();
        builder.Services.AddRadzenComponents();
        builder.Services.AddScoped<IUsuarioActual, UsuarioActualService>();
        builder.Services.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
        builder.Services.AddScoped<ITareaMantenimientoService, TareaMantenimientoService>();
        builder.Services.AddScoped<IMonitoreoService, MonitoreoService>();
        builder.Services.AddSingleton<GeneracionActualService>();
        builder.Services.AddSingleton(new PoliticaMonitoreo(60, 3));
        builder.Services.AddScoped<IConsultaChatbotService, ConsultaChatbotService>();
        builder.Services.AddScoped<IChatbotService, ChatbotService>();
        // Proveedor externo controlado. Application y MCP permanecen reales.
        builder.Services.AddScoped<IModeloChatbotFactory, ModeloPanelChatbotFactory>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.Cookie.Name = ".SolarUY.Chatbot.Pruebas";
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        configurar?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
        // Solo existe en el host de pruebas, ligado a loopback. No se modifica
        // la autenticación ni se agregan rutas de prueba a Presentation.
        app.MapMethods("/pruebas/ingresar/{rol}", ["GET", "POST"], async (HttpContext context, string rol) =>
        {
            if (rol is not ("Administrador" or "Tecnico" or "Consulta" or "Invitado"))
            { context.Response.StatusCode = 400; return; }
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity([new("sub", "tecnico-uno"),
                    new(ClaimTypes.Name, rol), new(ClaimTypes.Role, rol)], CookieAuthenticationDefaults.AuthenticationScheme)));
            if (context.Request.Method == "GET") context.Response.Redirect("/chatbot"); else context.Response.StatusCode = 204;
        }).AllowAnonymous();
        app.MapGet("/pruebas/finalizar", (HttpContext context) =>
        {
            context.Response.OnCompleted(() => { cierre?.TrySetResult(); return Task.CompletedTask; });
            return Results.Text("Verificación finalizada.");
        }).AllowAnonymous();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }

    private sealed class ModeloPanelChatbotFactory : IModeloChatbotFactory
    {
        public IChatClient Crear()
        {
            var modelo = new ModeloChatbotControlado();
            modelo.AntesDeResponder = ct => Task.Delay(250, ct);
            modelo.Responder = mensajes =>
            {
                var ultima = mensajes.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
                if (ultima.Contains("error controlado", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("sk-ficticia-no-exponer");
                var resultado = mensajes.SelectMany(m => m.Contents).OfType<FunctionResultContent>().LastOrDefault();
                if (resultado is null) return "No se obtuvieron datos.";
                var datos = JsonSerializer.SerializeToElement(resultado.Result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var nombres = datos.GetProperty("datos").EnumerateArray().Select(p => p.GetProperty("nombre").GetString());
                return "Parques disponibles: " + string.Join(", ", nombres) + ".";
            };
            modelo.Llamadas.Enqueue(("ListarParquesAsync", []));
            return modelo;
        }
    }
}
