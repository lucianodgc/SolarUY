using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Radzen;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Mantenimiento;
using SolarUY.Application.Inversores;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Application.Reportes;
using SolarUY.Application.ParquesSolares;
using SolarUY.Infrastructure;
using SolarUY.Presentation.Authentication;
using SolarUY.Presentation.Components;
using SolarUY.Presentation.BackgroundServices;
using SolarUY.Presentation.Monitoreo;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Monitoreo;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSolarInfrastructure(builder.Configuration);
builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualService>();
builder.Services.AddScoped<ITareaMantenimientoService, TareaMantenimientoService>();
builder.Services.AddScoped<IInversorService, InversorService>();
builder.Services.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
builder.Services.AddScoped<IReporteHistoricoService, ReporteHistoricoService>();
builder.Services.AddScoped<IRecoleccionLecturasService, RecoleccionLecturasService>();
builder.Services.AddOptions<MonitoreoOptions>().Bind(builder.Configuration.GetSection("Monitoreo"))
    .Validate<ConfiguracionRecoleccion>((o, recoleccion) => o.IntervalosVigencia > 0 && o.MaximoPuntosGrafico > 0
        && o.IntervalosVigencia <= long.MaxValue / recoleccion.Intervalo.Ticks
        && o.ReevaluacionSegundos is > 0 and <= 4_294_967, "Configuración Monitoreo fuera del rango representable.")
    .ValidateOnStart();
builder.Services.AddSingleton<GeneracionActualService>();
builder.Services.AddSingleton(provider => new PoliticaMonitoreo(
    (int)provider.GetRequiredService<ConfiguracionRecoleccion>().Intervalo.TotalSeconds,
    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MonitoreoOptions>>().Value.IntervalosVigencia));
builder.Services.AddScoped<IMonitoreoService, MonitoreoService>();
builder.Services.AddScoped<AccesoMonitoreoService>();
builder.Services.AddScoped(provider => new SerieMonitoreoService(
    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MonitoreoOptions>>().Value.MaximoPuntosGrafico,
    provider.GetRequiredService<ConfiguracionRecoleccion>().Intervalo));
builder.Services.AddSignalR();
builder.Services.AddSingleton<INotificadorLecturasConfirmadas, NotificadorLecturasConfirmadas>();
builder.Services.AddOptions<DistribucionMonitoreoOptions>().Bind(builder.Configuration.GetSection("DistribucionMonitoreo"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.InstanciaId) && o.InstanciaId.Length <= 100
        && o.IntervaloSegundos is > 0 and <= 4_294_967 && o.TamanoLote > 0, "Configuración de distribución de monitoreo inválida.")
    .ValidateOnStart();
builder.Services.AddHostedService<DistribucionMonitoreoWorker>();
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
builder.Services.AddRadzenComponents();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/hubs/monitoreo")) context.Response.StatusCode = 401;
            else context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/hubs/monitoreo")) context.Response.StatusCode = 403;
            else context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddOpenIdConnect(options =>
    {
        var openIdConnect = builder.Configuration.GetSection("Authentication:OpenIdConnect");
        options.Authority = openIdConnect["Authority"]
            ?? throw new InvalidOperationException("Falta Authentication:OpenIdConnect:Authority.");
        options.ClientId = openIdConnect["ClientId"]
            ?? throw new InvalidOperationException("Falta Authentication:OpenIdConnect:ClientId.");
        options.ClientSecret = openIdConnect["ClientSecret"];
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };
        options.Events.OnTokenValidated = context =>
        {
            var realmAccess = context.Principal?.FindFirst("realm_access")?.Value;
            if (!string.IsNullOrWhiteSpace(realmAccess))
            {
                using var document = JsonDocument.Parse(realmAccess);
                if (document.RootElement.TryGetProperty("roles", out var roles)
                    && roles.ValueKind == JsonValueKind.Array
                    && context.Principal?.Identity is ClaimsIdentity identity)
                {
                    foreach (var role in roles.EnumerateArray())
                    {
                        if (role.ValueKind == JsonValueKind.String && role.GetString() is { } roleName
                            && !identity.HasClaim(ClaimTypes.Role, roleName))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                        }
                    }
                }
            }

            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();
if (!string.IsNullOrWhiteSpace(direccionProxy)) app.UseForwardedHeaders();
app.Use(async (context, siguiente) =>
{
    context.Response.Headers["X-SolarUY-Instancia"] = app.Services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<DistribucionMonitoreoOptions>>().Value.InstanciaId;
    await siguiente();
});

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

app.MapPost("/Account/Logout", async (HttpContext context, IAntiforgery antiforgery) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
    {
        RedirectUri = "/"
    });
}).AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
