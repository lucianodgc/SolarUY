using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SolarUY.Presentation.Monitoreo;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class MonitoreoNginxIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MonitoreoPlaywrightFact]
    public async Task ServidorTemporalParaPlaywright_ConDosProcesosYNginx()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto(); await context.LecturasPanel.ExecuteDeleteAsync();
        var runtime = Path.Combine(HistoricoDatabase.RutaProyecto("SolarUY.Browser.Tests"), ".runtime");
        Directory.CreateDirectory(runtime);
        foreach (var archivo in new[] { "control.json", "resultado-control.json", "finalizado.json" }) File.Delete(Path.Combine(runtime, archivo));
        var claves = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "solaruy-req14-" + Guid.NewGuid().ToString("N"))).FullName;
        var cookie = CrearCookie(claves);
        await File.WriteAllTextAsync(Path.Combine(runtime, "sesion.json"), JsonSerializer.Serialize(new
        {
            cookie, parqueId = database.Parque.Id, otroParqueId = database.OtroParque.Id,
            proxy = Environment.GetEnvironmentVariable("SOLARUY_MONITOREO_PROXY_URL") ?? "http://127.0.0.1:18088"
        }));
        var salida = new ConcurrentQueue<string>(); Process? a = null; Process? b = null;
        try
        {
            a = Iniciar("req14-a", 5051, true, claves, salida); b = Iniciar("req14-b", 5052, true, claves, salida);
            await EsperarAsync(5051, "req14-a", salida); await EsperarAsync(5052, "req14-b", salida);
            string? ultimo = null;
            var fin = DateTime.UtcNow.AddMinutes(4);
            while (DateTime.UtcNow < fin)
            {
                var archivo = Path.Combine(runtime, "control.json");
                if (!File.Exists(archivo)) { await Task.Delay(50); continue; }
                JsonDocument orden;
                try { orden = JsonDocument.Parse(await File.ReadAllTextAsync(archivo)); }
                catch (JsonException) { await Task.Delay(50); continue; }
                catch (IOException) { await Task.Delay(50); continue; }
                using (orden)
                {
                    var id = orden.RootElement.GetProperty("id").GetString();
                    if (id == ultimo) { await Task.Delay(50); continue; }
                    ultimo = id;
                    var accion = orden.RootElement.GetProperty("accion").GetString();
                    if (accion == "terminar") break;
                    switch (accion)
                    {
                        case "pausar":
                            await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER req14_browser_pausa BEFORE INSERT ON AvisosMonitoreo FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pausa de prueba'");
                            break;
                        case "reanudar": await context.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS req14_browser_pausa"); break;
                        case "repetir":
                            await context.Database.ExecuteSqlRawAsync("UPDATE CursoresMonitoreo SET Secuencia = GREATEST(Secuencia - 1, 0) WHERE InstanciaId IN ('req14-a', 'req14-b')");
                            break;
                        case "relevar":
                            if (a is not null && !a.HasExited) { a.Kill(entireProcessTree: true); await a.WaitForExitAsync(); }
                            Assert.NotNull(b); Assert.False(b.HasExited);
                            await EsperarAsync(5052, "req14-b", salida);
                            break;
                        default: throw new InvalidOperationException("Orden de prueba desconocida.");
                    }
                    await File.WriteAllTextAsync(Path.Combine(runtime, "resultado-control.json"), JsonSerializer.Serialize(new { id, accion }));
                }
            }
            Assert.False(await context.LecturasPanel.GroupBy(l => new { l.PanelId, l.FechaHora }).AnyAsync(g => g.Count() > 1));
        }
        finally
        {
            foreach (var proceso in new[] { a, b })
                if (proceso is not null) { if (!proceso.HasExited) { proceso.Kill(entireProcessTree: true); await proceso.WaitForExitAsync(); } proceso.Dispose(); }
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS req14_browser_pausa");
            await File.WriteAllTextAsync(Path.Combine(runtime, "finalizado.json"), "{}");
        }
    }

    [MonitoreoNginxFact]
    public async Task DosProcesosTrasNginx_RecibenAvisosYRecuperanAutenticacionYEstadoTrasRelevo()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        var proxy = new Uri(Environment.GetEnvironmentVariable("SOLARUY_MONITOREO_PROXY_URL")!);
        var claves = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "solaruy-req14-" + Guid.NewGuid().ToString("N"))).FullName;
        var cookie = CrearCookie(claves);
        var salida = new ConcurrentQueue<string>();
        Process? a = null; Process? b = null;
        try
        {
            b = Iniciar("req14-b", 5052, false, claves, salida);
            a = Iniciar("req14-a", 5051, true, claves, salida);
            await EsperarAsync(5051, "req14-a", salida); await EsperarAsync(5052, "req14-b", salida);
            using var clienteA = Cliente(proxy, cookie, "a", out var cookiesA);
            using var clienteB = Cliente(proxy, cookie, "b", out var cookiesB);
            var ruta = "/monitoreo?parque=" + database.Parque.Id;
            Assert.Equal("req14-a", await InstanciaAsync(clienteA, ruta));
            Assert.Equal("req14-b", await InstanciaAsync(clienteB, ruta));
            using (var socketA = await ConectarAsync(proxy, cookiesA, database.Parque.Id))
            using (var socketB = await ConectarAsync(proxy, cookiesB, database.Parque.Id))
            {
                Assert.Equal(database.Parque.Id, (await RecibirAsync(socketA)).GetProperty("arguments")[0].GetGuid());
                Assert.Equal(database.Parque.Id, (await RecibirAsync(socketB)).GetProperty("arguments")[0].GetGuid());
            }
            a.Kill(entireProcessTree: true); await a.WaitForExitAsync();
            // La misma cookie autenticada y la afinidad A deben funcionar en el respaldo B.
            Assert.Equal("req14-b", await InstanciaAsync(clienteA, ruta));
            Assert.Equal("b", cookiesA.GetCookies(proxy)["SolarUYInstancia"]?.Value);
            b.Kill(entireProcessTree: true); await b.WaitForExitAsync(); b.Dispose();
            b = Iniciar("req14-b", 5052, true, claves, salida);
            await EsperarAsync(5052, "req14-b", salida);
            using var relevoA = await ConectarAsync(proxy, cookiesA, database.Parque.Id);
            using var relevoB = await ConectarAsync(proxy, cookiesB, database.Parque.Id);
            Assert.Equal(database.Parque.Id, (await RecibirAsync(relevoA)).GetProperty("arguments")[0].GetGuid());
            Assert.Equal(database.Parque.Id, (await RecibirAsync(relevoB)).GetProperty("arguments")[0].GetGuid());
            Assert.Equal("req14-b", await InstanciaAsync(clienteA, ruta));
            Assert.False(await context.LecturasPanel.GroupBy(l => new { l.PanelId, l.FechaHora }).AnyAsync(g => g.Count() > 1));
        }
        finally
        {
            foreach (var proceso in new[] { a, b })
                if (proceso is not null) { if (!proceso.HasExited) { proceso.Kill(entireProcessTree: true); await proceso.WaitForExitAsync(); } proceso.Dispose(); }
        }
    }

    private static Process Iniciar(string id, int puerto, bool recolectar, string claves, ConcurrentQueue<string> salida)
    {
        var inicio = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = HistoricoDatabase.RutaProyecto("SolarUY.Presentation") };
        inicio.ArgumentList.Add(typeof(MonitoreoHub).Assembly.Location);
        inicio.Environment["ConnectionStrings__SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)!;
        inicio.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{puerto}";
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["RecoleccionLecturas__Habilitada"] = recolectar.ToString();
        inicio.Environment["RecoleccionLecturas__IntervaloSegundos"] = "2";
        inicio.Environment["DistribucionMonitoreo__InstanciaId"] = id;
        inicio.Environment["DistribucionMonitoreo__IntervaloSegundos"] = "1";
        inicio.Environment["DataProtection__RutaClaves"] = claves;
        inicio.Environment["Logging__EventLog__LogLevel__Default"] = "None";
        inicio.Environment["Logging__LogLevel__Default"] = "Warning";
        inicio.Environment["Monitoreo__MaximoPuntosGrafico"] = "3";
        inicio.Environment["Monitoreo__ReevaluacionSegundos"] = "1";
        var proceso = new Process { StartInfo = inicio };
        proceso.OutputDataReceived += (_, e) => { if (e.Data is not null) salida.Enqueue(e.Data); };
        proceso.ErrorDataReceived += (_, e) => { if (e.Data is not null) salida.Enqueue(e.Data); };
        proceso.Start(); proceso.BeginOutputReadLine(); proceso.BeginErrorReadLine(); return proceso;
    }

    private static string CrearCookie(string claves)
    {
        var servicios = new ServiceCollection(); servicios.AddLogging();
        servicios.AddDataProtection().SetApplicationName("SolarUY").PersistKeysToFileSystem(new DirectoryInfo(claves));
        servicios.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        using var emisor = servicios.BuildServiceProvider();
        var opciones = emisor.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var identidad = new ClaimsIdentity([new Claim("sub", "prueba-nginx"), new Claim(ClaimTypes.Role, "Consulta")], CookieAuthenticationDefaults.AuthenticationScheme);
        return opciones.TicketDataFormat.Protect(new AuthenticationTicket(new ClaimsPrincipal(identidad),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) }, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static async Task EsperarAsync(int puerto, string id, ConcurrentQueue<string> salida)
    {
        using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var fin = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < fin)
        {
            try
            {
                var respuesta = await cliente.GetAsync($"http://127.0.0.1:{puerto}/lib/signalr/8.0.17/signalr.min.js");
                if (respuesta.Headers.TryGetValues("X-SolarUY-Instancia", out var valores) && valores.Single() == id) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(100);
        }
        Assert.Fail(string.Join(Environment.NewLine, salida.TakeLast(20)));
    }

    private static HttpClient Cliente(Uri proxy, string ticket, string instancia, out CookieContainer cookies)
    {
        cookies = new(); cookies.Add(proxy, new Cookie(".AspNetCore.Cookies", ticket, "/"));
        cookies.Add(proxy, new Cookie("SolarUYInstancia", instancia, "/"));
        return new HttpClient(new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false }) { BaseAddress = proxy };
    }

    private static async Task<string> InstanciaAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta);
        Assert.True(respuesta.IsSuccessStatusCode, $"HTTP {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        return respuesta.Headers.GetValues("X-SolarUY-Instancia").Single();
    }

    private static async Task<ClientWebSocket> ConectarAsync(Uri proxy, CookieContainer cookies, Guid parque)
    {
        var socket = new ClientWebSocket(); socket.Options.Cookies = cookies;
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await socket.ConnectAsync(new UriBuilder(proxy) { Scheme = "ws", Path = "/hubs/monitoreo" }.Uri, limite.Token);
        await EnviarAsync(socket, "{\"protocol\":\"json\",\"version\":1}");
        await RecibirAsync(socket);
        await EnviarAsync(socket, JsonSerializer.Serialize(new { type = 1, invocationId = "1", target = "SuscribirParque", arguments = new[] { parque } }));
        JsonElement respuesta;
        do { respuesta = await RecibirAsync(socket); }
        while (!respuesta.TryGetProperty("type", out var tipo) || tipo.GetInt32() != 3);
        Assert.False(respuesta.TryGetProperty("error", out _), respuesta.GetRawText());
        return socket;
    }

    private static Task EnviarAsync(ClientWebSocket socket, string mensaje) => socket.SendAsync(
        new ArraySegment<byte>(Encoding.UTF8.GetBytes(mensaje + '\u001e')), WebSocketMessageType.Text, true, default);

    private static readonly ConditionalWeakTable<ClientWebSocket, Queue<JsonElement>> MensajesPendientes = new();

    private static async Task<JsonElement> RecibirAsync(ClientWebSocket socket)
    {
        var pendientes = MensajesPendientes.GetOrCreateValue(socket);
        if (pendientes.Count > 0) return pendientes.Dequeue();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var texto = new StringBuilder(); var buffer = new byte[8192];
        while (true)
        {
            var recibido = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), limite.Token);
            Assert.NotEqual(WebSocketMessageType.Close, recibido.MessageType);
            texto.Append(Encoding.UTF8.GetString(buffer, 0, recibido.Count));
            if (!recibido.EndOfMessage) continue;
            foreach (var mensaje in texto.ToString().Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var doc = JsonDocument.Parse(mensaje);
                if (doc.RootElement.TryGetProperty("type", out var tipo) && tipo.GetInt32() == 6) continue;
                pendientes.Enqueue(doc.RootElement.Clone());
            }
            if (pendientes.Count > 0) return pendientes.Dequeue();
            texto.Clear();
        }
    }
}

public sealed class MonitoreoNginxFactAttribute : FactAttribute
{
    public MonitoreoNginxFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SOLARUY_MONITOREO_PROXY_URL"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Requiere Nginx configurado para host.docker.internal:5051/5052 y SOLARUY_MONITOREO_PROXY_URL.";
    }
}

public sealed class MonitoreoPlaywrightFactAttribute : FactAttribute
{
    public MonitoreoPlaywrightFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOLARUY_MONITOREO_BROWSER_PROXY") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Servidor temporal utilizado por la suite aislada de Playwright.";
    }
}
