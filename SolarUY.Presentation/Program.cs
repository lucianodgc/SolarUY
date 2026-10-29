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
using SolarUY.Application.Nodos;
using SolarUY.Infrastructure;
using SolarUY.Presentation.Authentication;
using SolarUY.Presentation.Components;
using SolarUY.Presentation.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSolarInfrastructure(builder.Configuration);
builder.Services.AddScoped<IParqueSolarService, ParqueSolarService>();
builder.Services.AddScoped<INodoService, NodoService>();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualService>();
builder.Services.AddScoped<ITareaMantenimientoService, TareaMantenimientoService>();
builder.Services.AddScoped<IInversorService, InversorService>();
builder.Services.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
builder.Services.AddScoped<IReporteHistoricoService, ReporteHistoricoService>();
builder.Services.AddScoped<IRecoleccionLecturasService, RecoleccionLecturasService>();
builder.Services.AddOptions<RecoleccionLecturasOptions>()
    .Bind(builder.Configuration.GetSection("RecoleccionLecturas"))
    .Validate(options => options.Intervalo >= TimeSpan.FromSeconds(1) && options.Intervalo <= TimeSpan.FromMinutes(5),
        "RecoleccionLecturas:Intervalo debe estar entre un segundo y cinco minutos.")
    .ValidateOnStart();
builder.Services.AddHostedService<RecoleccionLecturasWorker>();
builder.Services.AddScoped<IValidator<CrearParqueSolarDto>, CrearParqueSolarDtoValidator>();
builder.Services.AddScoped<IValidator<ActualizarParqueSolarDto>, ActualizarParqueSolarDtoValidator>();
builder.Services.AddScoped<IValidator<CrearNodoDto>, CrearNodoDtoValidator>();
builder.Services.AddScoped<IValidator<ActualizarNodoDto>, ActualizarNodoDtoValidator>();
builder.Services.AddRadzenComponents();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
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
