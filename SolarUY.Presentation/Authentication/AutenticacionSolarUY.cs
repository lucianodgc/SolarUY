using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace SolarUY.Presentation.Authentication;

internal static class AutenticacionSolarUY
{
    internal static void Configurar(IServiceCollection services, IConfiguration configuracion, bool esDesarrollo)
    {
        services
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
                var openIdConnect = configuracion.GetSection("Authentication:OpenIdConnect");
                options.Authority = openIdConnect["Authority"]
                    ?? throw new InvalidOperationException("Falta Authentication:OpenIdConnect:Authority.");
                options.ClientId = openIdConnect["ClientId"]
                    ?? throw new InvalidOperationException("Falta Authentication:OpenIdConnect:ClientId.");
                options.ClientSecret = openIdConnect["ClientSecret"];
                options.ResponseType = "code";
                options.MapInboundClaims = false;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.RequireHttpsMetadata = !esDesarrollo;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events.OnRemoteFailure = context =>
                {
                    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("SolarUY.Authentication")
                        .LogWarning(context.Failure, "No se pudo completar el inicio de sesión con Keycloak.");
                    context.HandleResponse();
                    context.Response.Redirect("/inicio-sesion-incompleto");
                    return Task.CompletedTask;
                };
                options.Events.OnTokenValidated = AgregarRoles;
            });
        services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());
    }

    internal static async Task CerrarSesionAsync(HttpContext contexto, IAntiforgery antiforgery)
    {
        if (!await antiforgery.IsRequestValidAsync(contexto))
        {
            contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await contexto.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
        {
            RedirectUri = "/"
        });
    }

    private static Task AgregarRoles(TokenValidatedContext contexto)
    {
        var realmAccess = contexto.Principal?.FindFirst("realm_access")?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess) || contexto.Principal?.Identity is not ClaimsIdentity identidad)
            return Task.CompletedTask;

        using var documento = JsonDocument.Parse(realmAccess);
        if (!documento.RootElement.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
            return Task.CompletedTask;

        foreach (var rol in roles.EnumerateArray())
        {
            if (rol.ValueKind == JsonValueKind.String && rol.GetString() is { } nombre
                && !identidad.HasClaim(ClaimTypes.Role, nombre))
                identidad.AddClaim(new Claim(ClaimTypes.Role, nombre));
        }

        return Task.CompletedTask;
    }
}
