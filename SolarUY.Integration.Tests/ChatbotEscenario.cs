using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Chatbot;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.Inversores;
using SolarUY.Application.Mantenimiento;
using SolarUY.Application.Monitoreo;
using SolarUY.Application.Reportes;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Domain.Monitoreo;
using SolarUY.Infrastructure.Chatbot;

namespace SolarUY.Integration.Tests;

// Dobles únicamente en los límites externos; los servicios de aplicación,
// autorización, adaptadores, Agent Framework y protocolo MCP son los reales.
internal sealed class ChatbotEscenario : IAsyncDisposable
{
    public ParqueSolar Parque { get; } = new("Parque norte", "Salto");
    public ParqueSolar OtroParque { get; } = new("Parque sur", "Rocha");
    public Nodo Nodo { get; }
    public Panel Panel { get; }
    public Panel SegundoPanel { get; }
    public List<InventarioNodoInversorDto> Inventario { get; } = [];
    public List<TareaMantenimiento> Tareas { get; } = [];
    public IdentidadChatbot Usuario { get; } = new();
    public ModeloChatbotControlado Modelo { get; } = new();
    public InversorChatbotControlado Inversor { get; } = new();
    public HistoricoChatbotControlado Historico { get; }
    public ServiceProvider Servicios { get; }
    public DateTimeOffset Ahora { get; } = new(2026, 11, 17, 1, 30, 0, TimeSpan.Zero);
    public bool FallaMantenimiento { get; set; }
    public int Escrituras { get; set; }

    public ChatbotEscenario(string rol = "Administrador", int tiempoMaximo = 10,
        Action<IServiceCollection>? configurar = null)
    {
        Usuario.Actual = new(true, "tecnico-uno", [rol]);
        Nodo = new(Parque, "Nodo norte", "Norte");
        Panel = HistoricoDatabase.CrearPanel(Nodo, "SERIE-UNO");
        SegundoPanel = HistoricoDatabase.CrearPanel(Nodo, "SERIE-DOS");
        Inventario.Add(new(Nodo.Id.ToString("D"), Nodo.Nombre, Parque.Nombre, Parque.Id,
            [new(Panel.Id.ToString("D"), Panel.NumeroSerie, 450, EstadoPanel.Activo),
             new(SegundoPanel.Id.ToString("D"), SegundoPanel.NumeroSerie, 450, EstadoPanel.Falla)]));
        Inversor.Paneles =
        [new(Nodo.Id.ToString("D"), Panel.Id.ToString("D"), true, EstadoSaludPanel.Saludable, 0, Panel.NumeroSerie),
         new(Nodo.Id.ToString("D"), SegundoPanel.Id.ToString("D"), true, EstadoSaludPanel.Fallado, 120, SegundoPanel.NumeroSerie)];
        Tareas.Add(new(Panel, "Limpieza", "Tarea propia", Ahora.UtcDateTime, "tecnico-uno"));
        Tareas.Add(new(SegundoPanel, "Revisión", "Tarea ajena", Ahora.UtcDateTime, "tecnico-dos"));
        var completada = new TareaMantenimiento(Panel, "Revisión", "Completada", Ahora.UtcDateTime, "tecnico-uno");
        completada.Iniciar(); completada.Completar(Ahora.UtcDateTime); Tareas.Add(completada);
        var cancelada = new TareaMantenimiento(Panel, "Revisión", "Cancelada", Ahora.UtcDateTime, "tecnico-uno");
        cancelada.Cancelar(); Tareas.Add(cancelada);
        Historico = new(this);
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        servicios.AddSingleton<IUsuarioActual>(Usuario);
        servicios.AddSingleton<TimeProvider>(new RelojChatbot(Ahora));
        servicios.AddSingleton<IInversorInventarioRepository>(new InventarioChatbotControlado(this));
        servicios.AddSingleton<IInversorClient>(Inversor);
        servicios.AddSingleton<IMonitoreoRepository>(new MonitoreoChatbotControlado(this));
        servicios.AddScoped<IMonitoreoService, MonitoreoService>();
        servicios.AddSingleton<GeneracionActualService>();
        servicios.AddSingleton(new PoliticaMonitoreo(60, 3));
        servicios.AddSingleton<IHistoricoGeneracionRepository>(Historico);
        servicios.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
        servicios.AddSingleton<ITareaMantenimientoRepository>(new TareasChatbotControladas(this));
        servicios.AddSingleton<IPanelRepository>(new PanelesChatbotControlados(this));
        servicios.AddSingleton<ITecnicoDirectory>(new TecnicosChatbotControlados());
        servicios.AddScoped<ITareaMantenimientoService, TareaMantenimientoService>();
        servicios.AddScoped<IEstadoEquiposConsultaClient, EstadoEquiposConsultaClient>();
        servicios.AddScoped<IConsultaChatbotService, ConsultaChatbotService>();
        servicios.AddScoped<ServidorMcpChatbot>();
        servicios.AddScoped<ConexionMcpChatbot>();
        servicios.AddScoped<IConexionMcpChatbot>(sp => sp.GetRequiredService<ConexionMcpChatbot>());
        servicios.AddSingleton<IModeloChatbotFactory>(Modelo);
        servicios.AddOptions<ChatbotOptions>().Configure(o => o.TiempoMaximoSegundos = tiempoMaximo);
        servicios.AddScoped<IChatbotClient, ChatbotClient>();
        servicios.AddScoped<IChatbotService, ChatbotService>();
        configurar?.Invoke(servicios);
        Servicios = servicios.BuildServiceProvider(validateScopes: true);
    }

    public ValueTask DisposeAsync() => Servicios.DisposeAsync();
    private sealed class RelojChatbot(DateTimeOffset ahora) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => ahora; }

    private sealed class InventarioChatbotControlado(ChatbotEscenario escenario) : IInversorInventarioRepository
    { public Task<IReadOnlyList<InventarioNodoInversorDto>> ListarAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventarioNodoInversorDto>>(escenario.Inventario.ToArray()); }

    private sealed class MonitoreoChatbotControlado(ChatbotEscenario escenario) : IMonitoreoRepository
    {
        public Task<IReadOnlyList<ParqueMonitoreoDto>> ListarParquesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParqueMonitoreoDto>>([new(escenario.Parque.Id, escenario.Parque.Nombre), new(escenario.OtroParque.Id, escenario.OtroParque.Nombre)]);
        public Task<DatosMonitoreoDto?> ObtenerDatosAsync(Guid parqueId, DateTime referenciaUtc, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("El chat no usa lecturas guardadas como estado actual.");
    }

    private sealed class TareasChatbotControladas(ChatbotEscenario escenario) : ITareaMantenimientoRepository
    {
        public Task<IReadOnlyList<TareaMantenimiento>> ListarAsync(FiltroTareasDto filtro, string? responsableAutorizado,
            DateTime referenciaUtc, CancellationToken cancellationToken = default)
        {
            if (escenario.FallaMantenimiento) throw new IOException("Falla de fuente controlada.");
            return Task.FromResult<IReadOnlyList<TareaMantenimiento>>(escenario.Tareas
                .Where(t => (responsableAutorizado is null || t.ResponsableId == responsableAutorizado)
                    && (filtro.Estado is null || t.Estado == filtro.Estado)).ToArray());
        }
        public Task AgregarAsync(TareaMantenimiento tarea, CancellationToken cancellationToken = default)
        { escenario.Escrituras++; throw new InvalidOperationException("Escritura prohibida."); }
        public Task<bool> GuardarAsync(TareaMantenimiento tarea, TareaMantenimientoOriginal original, string? responsableAutorizado, CancellationToken cancellationToken = default)
        { escenario.Escrituras++; throw new InvalidOperationException("Escritura prohibida."); }
        public Task<TareaMantenimiento?> ObtenerAsync(Guid id, string? responsableAutorizado, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MantenimientoRealizadoReporteDto>> ListarRealizadosParaReporteAsync(ConsultaMantenimientosReporteDto consulta, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PanelesChatbotControlados(ChatbotEscenario escenario) : IPanelRepository
    {
        public Task<Panel?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(new[] { escenario.Panel, escenario.SegundoPanel }.SingleOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<PanelDto>> ListarAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class TecnicosChatbotControlados : ITecnicoDirectory
    {
        public Task<IReadOnlyList<TecnicoDto>> ListarAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("No consultar directorio para leer tareas.");
        public Task<bool> EsTecnicoHabilitadoAsync(string responsableId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No validar asignaciones para leer tareas.");
    }
}

internal sealed class IdentidadChatbot : IUsuarioActual
{
    public UsuarioActualDto Actual { get; set; } = new(true, "tecnico-uno", ["Administrador"]);
    public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Actual); }
}

internal sealed class HistoricoChatbotControlado(ChatbotEscenario escenario) : IHistoricoGeneracionRepository
{
    public ConsultaHistoricoDto? UltimaConsulta { get; private set; }
    public bool SinDatos { get; set; }
    public bool Falla { get; set; }
    public decimal Energia { get; set; } = .7m;
    public Task<bool> ExisteObjetivoAsync(TipoObjetivoHistorico tipo, Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(id == escenario.Parque.Id || id == escenario.OtroParque.Id || id == escenario.Nodo.Id || id == escenario.Panel.Id);
    public Task<IReadOnlyList<OpcionHistoricoDto>> ListarOpcionesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OpcionHistoricoDto>>([
            new(TipoObjetivoHistorico.Parque, escenario.Parque.Id, escenario.Parque.Nombre),
            new(TipoObjetivoHistorico.Parque, escenario.OtroParque.Id, escenario.OtroParque.Nombre),
            new(TipoObjetivoHistorico.Nodo, escenario.Nodo.Id, escenario.Nodo.Nombre, escenario.Parque.Id),
            new(TipoObjetivoHistorico.Panel, escenario.Panel.Id, escenario.Panel.NumeroSerie, escenario.Parque.Id, escenario.Nodo.Id)]);
    public Task<ResultadoHistoricoDto> ConsultarAsync(ConsultaHistoricoDto consulta, CancellationToken cancellationToken = default)
    {
        if (Falla) throw new IOException("Error histórico controlado.");
        UltimaConsulta = consulta;
        return Task.FromResult(new ResultadoHistoricoDto(consulta, SinDatos ? null : Energia,
            SinDatos ? CoberturaHistorico.SinDatos : CoberturaHistorico.Parcial,
            [new(consulta.Intervalos[0], SinDatos ? null : Energia, SinDatos ? CoberturaHistorico.SinDatos : CoberturaHistorico.Parcial, false, false)])
            { LecturasSinNodoHistorico = 2 });
    }
}

internal sealed class ModeloChatbotControlado : IModeloChatbotFactory, IChatClient
{
    public Queue<(string Nombre, Dictionary<string, object?> Argumentos)> Llamadas { get; } = new();
    public Func<IReadOnlyList<ChatMessage>, string>? Responder { get; set; }
    public Func<IReadOnlyList<ChatMessage>, string>? Planificar { get; set; }
    public Func<CancellationToken, Task>? AntesDeResponder { get; set; }
    public List<ChatMessage> MensajesRecibidos { get; } = [];
    public bool Caido { get; set; }
    public int CantidadSolicitudes { get; private set; }
    public IChatClient Crear() => this;
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        CantidadSolicitudes++;
        if (Caido) throw new IOException("sk-ficticia-que-no-debe-salir");
        if (AntesDeResponder is not null) await AntesDeResponder(cancellationToken);
        var recibidos = messages.ToArray();
        MensajesRecibidos.Clear(); MensajesRecibidos.AddRange(recibidos);
        if (options?.ResponseFormat is ChatResponseFormatJson)
            return new(new ChatMessage(ChatRole.Assistant, Planificar?.Invoke(recibidos) ?? "{\"accion\":\"herramientas\",\"parques\":[]}"));
        if (Llamadas.TryDequeue(out var llamada))
            return new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(Guid.NewGuid().ToString(), llamada.Nombre, llamada.Argumentos)]));
        return new(new ChatMessage(ChatRole.Assistant, Responder?.Invoke(recibidos) ?? "Respuesta controlada basada en consultas de SolarUY."));
    }
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    { foreach (var update in (await GetResponseAsync(messages, options, cancellationToken)).ToChatResponseUpdates()) yield return update; }
}
