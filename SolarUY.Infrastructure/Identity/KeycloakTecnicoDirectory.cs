using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using SolarUY.Application.Exceptions;
using SolarUY.Application.Mantenimiento;

namespace SolarUY.Infrastructure.Identity;

public sealed class KeycloakTecnicoDirectory : ITecnicoDirectory
{
    private const int PageSize = 100;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public KeycloakTecnicoDirectory(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<TecnicoDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var conexion = ObtenerConexion();
            var token = await ObtenerTokenAsync(conexion, cancellationToken);
            var tecnicos = new List<TecnicoDto>();
            for (var offset = 0; ; offset += PageSize)
            {
                var usuarios = await ObtenerJsonAsync<List<UsuarioRepresentation>>(conexion,
                    $"users?enabled=true&first={offset}&max={PageSize}", token, cancellationToken)
                    ?? throw new TecnicoDirectoryException("Keycloak devolvió un directorio de usuarios inválido.");
                foreach (var usuario in usuarios)
                {
                    if (usuario.Enabled && !string.IsNullOrWhiteSpace(usuario.Id)
                        && await TieneRolTecnicoAsync(conexion, usuario.Id, token, cancellationToken))
                        tecnicos.Add(new TecnicoDto(usuario.Id, usuario.Username ?? usuario.Id));
                }
                if (usuarios.Count < PageSize) break;
            }
            return tecnicos.DistinctBy(tecnico => tecnico.Id).OrderBy(tecnico => tecnico.Nombre).ToArray();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        { throw new TecnicoDirectoryException("No se pudo consultar el directorio de técnicos de Keycloak."); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TecnicoDirectoryException("Keycloak no respondió dentro del tiempo permitido."); }
    }

    public async Task<bool> EsTecnicoHabilitadoAsync(string responsableId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(responsableId)) return false;
        try
        {
            var conexion = ObtenerConexion();
            var token = await ObtenerTokenAsync(conexion, cancellationToken);
            var usuario = await ObtenerJsonAsync<UsuarioRepresentation>(conexion,
                $"users/{Uri.EscapeDataString(responsableId)}", token, cancellationToken);
            return usuario is { Enabled: true } && usuario.Id == responsableId
                && await TieneRolTecnicoAsync(conexion, responsableId, token, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        { throw new TecnicoDirectoryException("No se pudo validar el responsable en Keycloak."); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TecnicoDirectoryException("Keycloak no respondió dentro del tiempo permitido."); }
    }

    private async Task<bool> TieneRolTecnicoAsync(Conexion conexion, string id, string token, CancellationToken cancellationToken)
    {
        var roles = await ObtenerJsonAsync<List<RolRepresentation>>(conexion,
            $"users/{Uri.EscapeDataString(id)}/role-mappings/realm/composite", token, cancellationToken);
        return roles?.Any(rol => rol.Name == "Tecnico") == true;
    }

    private async Task<T?> ObtenerJsonAsync<T>(Conexion conexion, string ruta, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{conexion.BaseUrl}/admin/realms/{conexion.Realm}/{ruta}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        AsegurarRespuesta(response);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private async Task<string> ObtenerTokenAsync(Conexion conexion, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{conexion.BaseUrl}/realms/{conexion.Realm}/protocol/openid-connect/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = conexion.ClientId,
            ["client_secret"] = conexion.ClientSecret
        });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        AsegurarRespuesta(response);
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
        if (json is null || !json.RootElement.TryGetProperty("access_token", out var token)
            || token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString()))
            throw new TecnicoDirectoryException("Keycloak no devolvió un token válido para el directorio.");
        return token.GetString()!;
    }

    private Conexion ObtenerConexion()
    {
        var section = _configuration.GetSection("Keycloak:Directory");
        var baseUrl = section["BaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || string.IsNullOrWhiteSpace(section["Realm"]) || string.IsNullOrWhiteSpace(section["ClientId"])
            || string.IsNullOrWhiteSpace(section["ClientSecret"]))
            throw new TecnicoDirectoryException("Configura Keycloak:Directory y su ClientSecret externo; usa HTTPS salvo localhost.");
        return new Conexion(baseUrl!.TrimEnd('/'), Uri.EscapeDataString(section["Realm"]!),
            section["ClientId"]!, section["ClientSecret"]!);
    }

    private static void AsegurarRespuesta(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new TecnicoDirectoryException(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? "El cliente de directorio no está autorizado. Comprueba su secreto y permisos de lectura de usuarios/roles."
                : "Keycloak no pudo atender la consulta del directorio.");
    }

    private sealed record Conexion(string BaseUrl, string Realm, string ClientId, string ClientSecret);
    private sealed class UsuarioRepresentation
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("username")] public string? Username { get; set; }
        [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    }
    private sealed class RolRepresentation
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
