using FluentValidation;
using Radzen;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Chatbot;
using SolarUY.Application.Mantenimiento;
using SolarUY.Application.Inversores;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Application.Reportes;
using SolarUY.Application.ParquesSolares;
using SolarUY.Application.Nodos;
using SolarUY.Infrastructure;
using SolarUY.Presentation.Authentication;
using SolarUY.Presentation.Components;
using SolarUY.Presentation.BackgroundServices;
using SolarUY.Presentation.Monitoreo;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Monitoreo;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

await Program.CrearAplicacion(WebApplication.CreateBuilder(args)).RunAsync();

internal static partial class Program
{
    internal static WebApplication CrearAplicacion(WebApplicationBuilder builder)
    {
        ConfigurarServicios(builder);
        var app = builder.Build();
        ConfigurarPipeline(app);
        return app;
    }

    internal static void ConfigurarServicios(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<SaludPanelOptions>().Bind(builder.Configuration.GetSection("SaludPanel"))
            .Validate(o => o.IntervalosDesconexion is > 0 and <= 1000 && o.EvaluacionSegundos is > 0 and <= 3600
                && o.UmbralFalla > 0 && o.UmbralFalla < o.UmbralDegradado && o.UmbralDegradado <= 1
                && o.FactorEsperado is > 0 and <= 1, "Configuración de salud inválida.")
            .Validate(o => !o.EvaluarPotenciaBaja,
                "La evaluación por potencia baja requiere evidencia de operación que el contrato histórico aún no ofrece; mantenerla deshabilitada.")
            .ValidateOnStart();
        builder.Services.AddSingleton<SolarUY.Domain.Salud.EvaluacionSaludService>();
        builder.Services.AddSingleton<SolarUY.Domain.Salud.SaludNodoService>();
        builder.Services.AddSingleton(provider =>
        {
            var o = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SaludPanelOptions>>().Value;
            var intervalo = provider.GetRequiredService<ConfiguracionRecoleccion>().Intervalo;
            return new SolarUY.Domain.Salud.PoliticaSalud(TimeSpan.FromTicks(checked(intervalo.Ticks * o.IntervalosDesconexion)),
                o.UmbralFalla, o.UmbralDegradado, o.FactorEsperado, o.EvaluarPotenciaBaja);
        });
        builder.Services.AddSingleton(provider => new SolarUY.Application.Salud.ConfiguracionConsultaSalud(
            TimeSpan.FromSeconds(3L * provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SaludPanelOptions>>().Value.EvaluacionSegundos)));
        builder.Services.AddScoped<SolarUY.Application.Salud.ISaludPanelService, SolarUY.Application.Salud.SaludPanelService>();
        builder.Services.AddScoped<SolarUY.Application.Salud.IConsultaSaludService, SolarUY.Application.Salud.ConsultaSaludService>();
        builder.Services.AddSingleton<SolarUY.Application.Salud.INotificadorSaludConfirmada, NotificadorSaludConfirmada>();
        builder.Services.AddHostedService<SaludPanelWorker>();
        builder.Services.AddOptions<SaludDistribucionOptions>().Bind(builder.Configuration.GetSection("SaludDistribucion"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.InstanciaId) && o.InstanciaId.Length <= 100
                && o.IntervaloSegundos is > 0 and <= 3600, "Configuración de distribución de salud inválida.")
            .Validate<Microsoft.Extensions.Options.IOptions<SaludPanelOptions>>((o, salud) =>
                o.IntervaloSegundos <= salud.Value.EvaluacionSegundos,
                "El observador de salud debe consultar al menos con la frecuencia de evaluación configurada.")
            .ValidateOnStart();
        builder.Services.AddHostedService<ObservacionSaludWorker>();
        builder.Services.AddSolarInfrastructure(builder.Configuration);
        builder.Services.AddChatbotInfrastructure(builder.Configuration);
        builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
        builder.Services.AddScoped<INodoService, NodoService>();
        builder.Services.AddScoped<IUsuarioActual, UsuarioActualService>();

        // Servicios del chatbot
        builder.Services.AddScoped<IConsultaChatbotService, ConsultaChatbotService>();
        builder.Services.AddScoped<IChatbotService, ChatbotService>();

        // Servicios de paneles
        builder.Services.AddScoped<SolarUY.Application.Paneles.IPanelService, SolarUY.Application.Paneles.PanelService>();
        builder.Services.AddScoped<IValidator<SolarUY.Application.Paneles.CrearPanelDto>, SolarUY.Application.Paneles.PanelDtoValidator>();
        builder.Services.AddScoped<IValidator<SolarUY.Application.Paneles.ActualizarPanelDto>, SolarUY.Application.Paneles.ActualizarPanelDtoValidator>();

        builder.Services.AddScoped<ITareaMantenimientoService, TareaMantenimientoService>();
        builder.Services.AddScoped<IInversorService, InversorService>();
        builder.Services.AddScoped<IOrdenInversorService, OrdenInversorService>();
        builder.Services.AddOptions<OrdenInversorOptions>()
            .Bind(builder.Configuration.GetSection("OrdenInversor"))
            .Validate(o => o.CierreAuditoriaSegundos is >= 1 and <= 30 && o.IntentosPersistencia is >= 1 and <= 2,
                "OrdenInversor requiere cierre de 1–30 segundos y 1–2 intentos de persistencia.")
            .ValidateOnStart();
        builder.Services.AddSingleton(p => p.GetRequiredService<Microsoft.Extensions.Options.IOptions<OrdenInversorOptions>>().Value);
        builder.Services.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
        builder.Services.AddScoped<IReporteHistoricoService, ReporteHistoricoService>();
        builder.Services.AddScoped<IRecoleccionLecturasService, RecoleccionLecturasService>();
        builder.Services.AddOptions<TelemetriaEnVivoOptions>().Bind(builder.Configuration.GetSection("TelemetriaEnVivo"))
            .Validate(o => o.IntervaloSegundos is > 0 and <= 60 && o.ActualizarInventarioSegundos >= o.IntervaloSegundos,
                "TelemetriaEnVivo requiere intervalos positivos y una actualización de inventario no más frecuente que la telemetría.")
            .ValidateOnStart();
        builder.Services.AddSingleton<ITelemetriaEnVivoService>(provider => new TelemetriaEnVivoService(
            provider.GetRequiredService<IInversorInventarioRepository>(),
            provider.GetRequiredService<IInversorClient>(),
            provider.GetRequiredService<TimeProvider>(),
            TimeSpan.FromSeconds(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelemetriaEnVivoOptions>>()
                .Value.ActualizarInventarioSegundos)));
        builder.Services.AddOptions<MonitoreoOptions>().Bind(builder.Configuration.GetSection("Monitoreo"))
            .Validate<ConfiguracionRecoleccion>((o, recoleccion) => o.EsValida(recoleccion.Intervalo),
                "Configuración Monitoreo fuera del rango representable.")
            .ValidateOnStart();
        builder.Services.AddSingleton<GeneracionActualService>();
        builder.Services.AddSingleton(provider => new PoliticaMonitoreo(
            (int)provider.GetRequiredService<ConfiguracionRecoleccion>().Intervalo.TotalSeconds,
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MonitoreoOptions>>().Value.IntervalosVigencia));
        builder.Services.AddScoped<IMonitoreoService, MonitoreoService>();
        builder.Services.AddScoped<AccesoMonitoreoService>();
        builder.Services.AddScoped(provider => new SerieMonitoreoService(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MonitoreoOptions>>().Value.MaximoPuntosGrafico,
            TimeSpan.FromSeconds(3 * provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelemetriaEnVivoOptions>>().Value.IntervaloSegundos)));
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<INotificadorLecturasConfirmadas, NotificadorLecturasConfirmadas>();
        builder.Services.AddSingleton<NotificadorTelemetriaEnVivo>();
        builder.Services.AddHostedService<TelemetriaEnVivoWorker>();
        var rutaClaves = builder.Configuration["DataProtection:RutaClaves"];
        if (!string.IsNullOrWhiteSpace(rutaClaves))
            builder.Services.AddDataProtection().SetApplicationName("SolarUY")
                .PersistKeysToFileSystem(new DirectoryInfo(rutaClaves));
        var direccionProxy = builder.Configuration["ReverseProxy:Direccion"];
        if (!string.IsNullOrWhiteSpace(direccionProxy))
            builder.Services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.KnownProxies.Add(System.Net.IPAddress.Parse(direccionProxy));
            });
        builder.Services.AddOptions<RecoleccionLecturasOptions>()
            .Bind(builder.Configuration.GetSection("RecoleccionLecturas"))
            .Validate(options => options.TieneIntervaloValido,
                "RecoleccionLecturas:IntervaloSegundos es obligatorio y debe estar entre 1 y 4294967 segundos (límite de PeriodicTimer).")
            .ValidateOnStart();
        builder.Services.AddHostedService<RecoleccionLecturasWorker>();
        builder.Services.AddSingleton(provider => new ConfiguracionRecoleccion(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RecoleccionLecturasOptions>>()
                .Value.IntervaloSegundos ?? throw new InvalidOperationException("Falta IntervaloSegundos.")));
        builder.Services.AddScoped<IValidator<CrearParqueSolarDto>, CrearParqueSolarDtoValidator>();
        builder.Services.AddScoped<IValidator<ActualizarParqueSolarDto>, ActualizarParqueSolarDtoValidator>();
        builder.Services.AddScoped<IValidator<CrearNodoDto>, CrearNodoDtoValidator>();
        builder.Services.AddScoped<IValidator<ActualizarNodoDto>, ActualizarNodoDtoValidator>();
        builder.Services.AddRadzenComponents();
        AutenticacionSolarUY.Configurar(builder.Services, builder.Configuration, builder.Environment.IsDevelopment());
        builder.Services.AddCascadingAuthenticationState();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

    }

    private static void ConfigurarPipeline(WebApplication app)
    {
        var direccionProxy = app.Configuration["ReverseProxy:Direccion"];
        if (!string.IsNullOrWhiteSpace(direccionProxy)) app.UseForwardedHeaders();
        app.Use(async (context, siguiente) =>
        {
            context.Response.Headers["X-SolarUY-Instancia"] = app.Configuration["Instancia:Id"]
                ?? app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SaludDistribucionOptions>>().Value.InstanciaId;
            await siguiente();
        });

        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .AllowAnonymous();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapHub<MonitoreoHub>("/hubs/monitoreo", options => options.CloseOnAuthenticationExpiration = true);

        app.MapPost("/Account/Logout", AutenticacionSolarUY.CerrarSesionAsync).AllowAnonymous();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
    }
}
