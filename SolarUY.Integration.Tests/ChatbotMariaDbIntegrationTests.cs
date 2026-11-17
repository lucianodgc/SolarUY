using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Chatbot;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.Inversores;
using SolarUY.Application.Mantenimiento;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Monitoreo;
using SolarUY.Infrastructure;
using SolarUY.Infrastructure.Chatbot;
using SolarUY.Infrastructure.Inversores;
using SolarUY.Infrastructure.Inversores.Tcp;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class ChatbotMariaDbIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoTheory]
    [InlineData(PeriodoHistorico.Diario)]
    [InlineData(PeriodoHistorico.Mensual)]
    [InlineData(PeriodoHistorico.Anual)]
    public async Task Chatbot_McpHistoricoCoincideConMariaDbSinCambiarLecturas(PeriodoHistorico periodo)
    {
        await database.ReiniciarAsync();
        await using var servicios = CrearServicios(Configuracion());
        await using var scope = servicios.CreateAsyncScope();
        var esperado = await scope.ServiceProvider.GetRequiredService<IHistoricoGeneracionService>()
            .ConsultarAsync(new(TipoObjetivoHistorico.Parque, database.Parque.Id, periodo, new(2026, 2, 1)));
        var antes = await InstantaneaAsync();
        await using var sesion = await scope.ServiceProvider.GetRequiredService<ConexionMcpChatbot>().AbrirAsync();
        var resultado = await sesion.Cliente.CallToolAsync("ConsultarGeneracionHistoricaAsync", new Dictionary<string, object?>
        { ["objetivo"] = database.Parque.Id.ToString(), ["tipo"] = "Parque", ["periodo"] = periodo.ToString(), ["fechaLocal"] = "2026-02-01" });
        var datos = resultado.StructuredContent!.Value.GetProperty("datos");
        Assert.Equal(esperado.EnergiaKWh, datos.GetProperty("energiaKWh").GetDecimal());
        Assert.Equal(esperado.Serie.Count, datos.GetProperty("serie").GetArrayLength());
        Assert.Equal(antes, await InstantaneaAsync());
    }

    [ChatbotOpenAiFact]
    public async Task Chatbot_OpenAiRealConsultaHistoricoPorAgentFrameworkYMcp()
        => await ProbarHistoricoRealAsync("OpenAI");

    [ChatbotOllamaFact]
    public async Task Chatbot_OllamaRealConsultaHistoricoPorAgentFrameworkYMcp()
        => await ProbarHistoricoRealAsync("Ollama");

    private async Task ProbarHistoricoRealAsync(string proveedor)
    {
        await database.ReiniciarAsync();
        var configuracion = Configuracion(proveedor: proveedor);
        var registros = new RegistrosChatbot();
        await using var servicios = CrearServicios(configuracion, registros: registros);
        await using var scope = servicios.CreateAsyncScope();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(320));
        var antes = await InstantaneaAsync();
        var resultado = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(
            new($"¿Cuánta energía generó el parque '{database.Parque.Nombre}', identificador {database.Parque.Id}, el día 1 de febrero de 2026? Consultá sus datos y respondé en kWh con cobertura."), limite.Token);
        Assert.True(resultado.Estado == EstadoConsultaChat.Correcto,
            "El proveedor no completó la consulta. " + string.Join("; ", registros.Mensajes.Where(m => m.StartsWith("Chatbot:"))));
        Assert.Contains(resultado.Consultas, r => r.Herramienta is "ConsultarGeneracionDiariaAsync" or "ConsultarGeneracionHistoricaAsync");
        var energia = resultado.Consultas.First(r => r.Herramienta is "ConsultarGeneracionDiariaAsync" or "ConsultarGeneracionHistoricaAsync");
        Assert.Equal(.78m, ((JsonElement)energia.Datos!).GetProperty("energiaKWh").GetDecimal());
        Assert.Contains("kWh", resultado.Texto);
        Assert.True(resultado.Texto.Contains("0,78") || resultado.Texto.Contains("0.78"), "La respuesta no refleja la energía consultada.");
        VerificarSecretos(configuracion, resultado, registros);
        Assert.Equal(antes, await InstantaneaAsync());
    }

    [ChatbotOpenAiFact]
    public async Task Chatbot_OpenAiRealResumenDeHoyConMariaDbYSimuladorTcpAislado()
        => await ProbarResumenRealAsync("OpenAI");

    [ChatbotOllamaFact]
    public async Task Chatbot_OllamaRealResumenDeHoyConMariaDbYSimuladorTcpAislado()
        => await ProbarResumenRealAsync("Ollama");

    [ChatbotOllamaFact]
    public async Task Chatbot_OllamaRealContinuaConversacionYDetallaCadaParque()
        => await ProbarResumenRealAsync("Ollama", conversacion: true);

    private async Task ProbarResumenRealAsync(string proveedor, bool conversacion = false)
    {
        await database.ReiniciarAsync();
        await PrepararHoyAsync();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var configuracion = Configuracion(puerto, proveedor);
        var registros = new RegistrosChatbot();
        await using var servicios = CrearServicios(configuracion, registros: registros);
        await using var scope = servicios.CreateAsyncScope();
        var inventario = await scope.ServiceProvider.GetRequiredService<IInversorInventarioRepository>().ListarAsync();
        var motor = new InversorSimulador(configuracion);
        // Preparación de la fuente aislada fuera del recorrido del chatbot.
        await motor.SincronizarInventarioAsync(inventario);
        var servidor = new InversorTcpServer(motor, Options.Create(new InversorTcpOptions
            { Host = "127.0.0.1", Port = puerto, TimeoutSeconds = 5 }), NullLogger<InversorTcpServer>.Instance);
        using var apagado = new CancellationTokenSource();
        var ejecucion = servidor.EjecutarAsync(apagado.Token);
        var antes = await InstantaneaAsync();
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(320));
            var chatbot = scope.ServiceProvider.GetRequiredService<IChatbotService>();
            if (conversacion)
            {
                var listado = await chatbot.EnviarPreguntaAsync(new("¿Qué parques hay?"), limite.Token);
                Assert.Equal(EstadoConsultaChat.Correcto, listado.Estado);
                Assert.Contains(listado.Consultas, r => r.Herramienta == "ListarParquesAsync");
                var ampliacion = await chatbot.EnviarPreguntaAsync(new("agregale mas informacion"), limite.Token);
                Assert.True(ampliacion.Estado == EstadoConsultaChat.Correcto,
                    "La ampliación no consultó datos nuevos del turno: " + ampliacion.Estado);
                Assert.Contains(database.Parque.Nombre, ampliacion.Texto, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(database.OtroParque.Nombre, ampliacion.Texto, StringComparison.OrdinalIgnoreCase);
                VerificarDetalleCompleto(ampliacion);
                VerificarSecretos(configuracion, ampliacion, registros);
            }
            var resultado = await chatbot.EnviarPreguntaAsync(new(conversacion
                ? "dime cuantos parques hay y detalla la informacion de cada uno?"
                : "¿Cómo está funcionando todo hoy?"), limite.Token);
            Assert.True(resultado.Estado == EstadoConsultaChat.Correcto,
                "El proveedor no completó la consulta. " + string.Join("; ", registros.Mensajes.Where(m => m.StartsWith("Chatbot:"))));
            VerificarDetalleCompleto(resultado);
            Assert.DoesNotContain(resultado.Consultas, r => r.Estado is EstadoConsultaChat.NoDisponible or EstadoConsultaChat.NoAutorizado);
            Assert.Contains(database.Parque.Nombre, resultado.Texto, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(database.OtroParque.Nombre, resultado.Texto, StringComparison.OrdinalIgnoreCase);
            if (conversacion)
                Assert.Contains("Cantidad de parques autorizados: 2.", resultado.Texto);
            Assert.Equal(antes, await InstantaneaAsync());
            VerificarSecretos(configuracion, resultado, registros);
            var rechazo = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("Desactivá el panel 3"));
            Assert.Contains("únicamente puede consultar", rechazo.Texto);
            Assert.All(await motor.ObtenerEstadoPanelesAsync(inventario[0].Id), p => Assert.True(p.EstaActivo));
        }
        finally
        {
            await apagado.CancelAsync();
            try { await ejecucion; } catch (OperationCanceledException) { }
        }
    }

    private void VerificarDetalleCompleto(RespuestaChatDto respuesta)
    {
        foreach (var nombre in new[] { "ConsultarEstadoActualAsync", "ConsultarGeneracionDiariaAsync",
                     "ConsultarPanelesConFallaAsync", "ConsultarMantenimientosPendientesAsync" })
            Assert.True(respuesta.Consultas.Count(r => r.Herramienta == nombre) >= 2,
                "El detalle no consultó cada apartado de ambos parques: " + nombre);
        var parques = respuesta.Consultas.Where(r => r.Herramienta == "ConsultarEstadoActualAsync")
            .Select(r => ((JsonElement)r.Datos!).GetProperty("parqueId").GetGuid()).Distinct().Order().ToArray();
        Assert.Equal(new[] { database.Parque.Id, database.OtroParque.Id }.Order(), parques);
    }

    private async Task PrepararHoyAsync()
    {
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        var paneles = await context.Paneles.ToListAsync();
        var dia = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Montevideo").Date;
        foreach (var panel in paneles)
        {
            context.LecturasPanel.Add(HistoricoDatabase.Lectura(panel, dia.AddHours(12), 1200));
            context.LecturasPanel.Add(HistoricoDatabase.Lectura(panel, dia.AddHours(12).AddMinutes(1), 1200));
            context.TareasMantenimiento.Add(new TareaMantenimiento(panel, "Limpieza", "Revisión de prueba REQ017",
                DateTime.UtcNow, "tecnico-uno"));
        }
        await context.SaveChangesAsync();
    }

    private async Task<string> InstantaneaAsync()
    {
        await using var context = database.CrearContexto();
        var lecturas = await context.LecturasPanel.AsNoTracking().OrderBy(l => l.Id)
            .Select(l => new { l.Id, l.PanelId, l.FechaHora, l.PotenciaGeneradaW, l.EstadoSalud }).ToArrayAsync();
        var tareas = await context.TareasMantenimiento.AsNoTracking().OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.PanelId, t.Estado, t.ResponsableId, t.Descripcion }).ToArrayAsync();
        var paneles = await context.Paneles.AsNoTracking().OrderBy(p => p.Id).Select(p => new { p.Id, p.NodoId, p.Estado }).ToArrayAsync();
        return JsonSerializer.Serialize(new { lecturas, tareas, paneles });
    }

    internal static IConfigurationRoot Configuracion(int puerto = 5055, string proveedor = "Ollama")
        => new ConfigurationBuilder().AddUserSecrets("solaruy-presentation-local").AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion),
                ["InversorTcp:Host"] = "127.0.0.1", ["InversorTcp:Port"] = puerto.ToString(),
                ["InversorTcp:TimeoutSeconds"] = "5", ["Chatbot:Proveedor"] = proveedor,
                ["Chatbot:Modelo"] = proveedor == "OpenAI" ? "gpt-6-luna" : "qwen3:8b",
                ["Chatbot:TiempoMaximoSegundos"] = "300", ["Chatbot:Mcp:Transporte"] = "Interno"
            }).Build();

    internal static ServiceProvider CrearServicios(IConfiguration configuracion, string rol = "Administrador", RegistrosChatbot? registros = null)
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton(configuracion);
        servicios.AddLogging(b => { b.ClearProviders(); if (registros is not null) b.AddProvider(registros); });
        servicios.AddSolarInfrastructure(configuracion);
        servicios.AddChatbotInfrastructure(configuracion);
        servicios.AddSingleton<IUsuarioActual>(new IdentidadChatbot { Actual = new(true, "tecnico-uno", [rol]) });
        servicios.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
        servicios.AddScoped<ITareaMantenimientoService, TareaMantenimientoService>();
        servicios.AddScoped<IMonitoreoService, MonitoreoService>();
        servicios.AddSingleton<GeneracionActualService>();
        servicios.AddSingleton(new PoliticaMonitoreo(60, 3));
        servicios.AddScoped<IConsultaChatbotService, ConsultaChatbotService>();
        servicios.AddScoped<IChatbotService, ChatbotService>();
        return servicios.BuildServiceProvider(validateScopes: true);
    }

    private static void VerificarSecretos(IConfiguration configuracion, RespuestaChatDto resultado, RegistrosChatbot registros)
    {
        var clave = configuracion["Chatbot:OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(clave))
        {
            Assert.NotEqual("OpenAI", configuracion["Chatbot:Proveedor"]);
            return;
        }
        Assert.False(JsonSerializer.Serialize(resultado).Contains(clave!, StringComparison.Ordinal), "Se detectó una credencial en la respuesta.");
        Assert.False(string.Join("\n", registros.Mensajes).Contains(clave!, StringComparison.Ordinal), "Se detectó una credencial en los registros.");
        foreach (var carpeta in new[] { "SolarUY.Application/Chatbot", "SolarUY.Infrastructure/Chatbot", "prompting/req017_Chatbot_en_lenguaje_natural" })
        foreach (var archivo in Directory.EnumerateFiles(HistoricoDatabase.RutaProyecto(carpeta)))
            Assert.False(File.ReadAllText(archivo).Contains(clave!, StringComparison.Ordinal), "Se detectó una credencial en un archivo del requerimiento.");
    }
}

public sealed class ChatbotOpenAiFactAttribute : FactAttribute
{
    public ChatbotOpenAiFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOLARUY_CHATBOT_LIVE") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Configurar SOLARUY_CHATBOT_LIVE=1, clave OpenAI externa y base MariaDB aislada para la verificación real.";
    }
}

public sealed class ChatbotOllamaFactAttribute : FactAttribute
{
    public ChatbotOllamaFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOLARUY_CHATBOT_OLLAMA") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Configurar SOLARUY_CHATBOT_OLLAMA=1, Ollama con qwen3:8b y la base MariaDB aislada.";
    }
}

internal sealed class RegistrosChatbot : ILoggerProvider
{
    public List<string> Mensajes { get; } = [];
    public ILogger CreateLogger(string categoryName) => new Registro(this);
    public void Dispose() { }
    private sealed class Registro(RegistrosChatbot registros) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { lock (registros.Mensajes) registros.Mensajes.Add(formatter(state, exception)); }
    }
}
