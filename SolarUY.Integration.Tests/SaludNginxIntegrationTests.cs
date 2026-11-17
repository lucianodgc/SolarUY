using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SolarUY.Domain.Salud;
using SolarUY.Infrastructure.Inversores;
using SolarUY.Infrastructure.Inversores.Tcp;
using SolarUY.Infrastructure.Persistence;
using static SolarUY.Integration.Tests.MonitoreoNginxIntegrationTests;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class SaludNginxIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [SaludNginxTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaludCompartida_LlegaDesdeCualquieraDeLasInstanciasYRecuperaRelevo(bool fuenteA)
    {
        await PrepararAsync();
        await using var simulador = new SimuladorTemporal(database);
        await simulador.EstablecerFallaAsync(true);
        var proxy = new Uri(Environment.GetEnvironmentVariable("SOLARUY_MONITOREO_PROXY_URL")!);
        var claves = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "solaruy-salud-" + Guid.NewGuid().ToString("N"))).FullName;
        var ticket = CrearCookie(claves); var salida = new ConcurrentQueue<string>();
        Process? a = null; Process? b = null;
        try
        {
            a = IniciarSalud("a", fuenteA, simulador.Puerto, claves, salida);
            b = IniciarSalud("b", !fuenteA, simulador.Puerto, claves, salida);
            await EsperarAsync(5051, "req16-a", salida); await EsperarAsync(5052, "req16-b", salida);
            using var clienteA = Cliente(proxy, ticket, "a", out var cookiesA);
            using var clienteB = Cliente(proxy, ticket, "b", out var cookiesB);
            await AfinidadAsync(clienteA, cookiesA, proxy, "a"); await AfinidadAsync(clienteB, cookiesB, proxy, "b");
            using var socketA = await ConectarAsync(proxy, cookiesA, database.Parque.Id);
            using var socketB = await ConectarAsync(proxy, cookiesB, database.Parque.Id);
            await EsperarCondicionAsync(EstadoSaludPanel.Falla);
            await AvisoSaludAsync(socketA); await AvisoSaludAsync(socketB);
            await simulador.EstablecerFallaAsync(false);
            await EsperarCondicionAsync(EstadoSaludPanel.Normal);
            await AvisoSaludAsync(socketA); await AvisoSaludAsync(socketB);
            await using (var db = database.CrearContexto())
                Assert.Empty(await db.Set<AlertaPanel>().Where(x => x.CierreUtc == null).ToArrayAsync());

            var fuente = fuenteA ? a : b;
            fuente.Kill(entireProcessTree: true); await fuente.WaitForExitAsync(); fuente.Dispose();
            if (fuenteA) a = null; else b = null;
            var clienteFuente = fuenteA ? clienteA : clienteB;
            Assert.Equal(fuenteA ? "req16-b" : "req16-a", await InstanciaAsync(clienteFuente, "/monitoreo?parque=" + database.Parque.Id));
            var cookiesFuente = fuenteA ? cookiesA : cookiesB;
            Assert.Equal(fuenteA ? "b" : "a", cookiesFuente.GetCookies(proxy)["SolarUYInstancia"]?.Value);
            using var reconectado = await ConectarAsync(proxy, cookiesFuente, database.Parque.Id);
            var nuevaFuente = IniciarSalud(fuenteA ? "a" : "b", true, simulador.Puerto, claves, salida);
            if (fuenteA) a = nuevaFuente; else b = nuevaFuente;
            await EsperarAsync(fuenteA ? 5051 : 5052, fuenteA ? "req16-a" : "req16-b", salida);
            await simulador.EstablecerFallaAsync(true);
            await EsperarCondicionAsync(EstadoSaludPanel.Falla);
            await AvisoSaludAsync(reconectado);
            using var respuesta = await clienteFuente.GetAsync("/monitoreo?parque=" + database.Parque.Id);
            Assert.True(respuesta.IsSuccessStatusCode);
            Assert.Contains("Alertas activas (1)", WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync()));
            await VerificarUnicidadAsync();
        }
        finally { await DetenerAsync(a); await DetenerAsync(b); }
    }

    [SaludDistribuidaNavegadorFact]
    public async Task ServidorTemporalSaludDistribuidaParaPlaywright()
    {
        await PrepararAsync();
        await using var simulador = new SimuladorTemporal(database);
        await simulador.EstablecerFallaAsync(false);
        var runtime = Path.Combine(HistoricoDatabase.RutaProyecto("SolarUY.Browser.Tests"), ".runtime", "salud-distribuida");
        Directory.CreateDirectory(runtime);
        foreach (var archivo in new[] { "control.json", "resultado.json", "finalizado.json" }) File.Delete(Path.Combine(runtime, archivo));
        var claves = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "solaruy-salud-" + Guid.NewGuid().ToString("N"))).FullName;
        await File.WriteAllTextAsync(Path.Combine(runtime, "sesion.json"), JsonSerializer.Serialize(new
        {
            cookie = CrearCookie(claves), parque = database.Parque.Id,
            proxy = Environment.GetEnvironmentVariable("SOLARUY_MONITOREO_PROXY_URL")
        }));
        var salida = new ConcurrentQueue<string>(); Process? a = null; Process? b = null;
        try
        {
            a = IniciarSalud("a", true, simulador.Puerto, claves, salida);
            b = IniciarSalud("b", false, simulador.Puerto, claves, salida);
            await EsperarAsync(5051, "req16-a", salida); await EsperarAsync(5052, "req16-b", salida);
            string? ultima = null;
            var fin = DateTime.UtcNow.AddMinutes(4);
            while (DateTime.UtcNow < fin)
            {
                var ruta = Path.Combine(runtime, "control.json");
                if (!File.Exists(ruta)) { await Task.Delay(50); continue; }
                JsonDocument doc;
                try { doc = JsonDocument.Parse(await File.ReadAllTextAsync(ruta)); }
                catch (IOException) { await Task.Delay(50); continue; }
                catch (JsonException) { await Task.Delay(50); continue; }
                using (doc)
                {
                    var id = doc.RootElement.GetProperty("id").GetString();
                    if (id == ultima) { await Task.Delay(50); continue; }
                    ultima = id; var accion = doc.RootElement.GetProperty("accion").GetString();
                    if (accion == "terminar") break;
                    switch (accion)
                    {
                        case "falla": await simulador.EstablecerFallaAsync(true); break;
                        case "recuperar": await simulador.EstablecerFallaAsync(false); break;
                        case "relevar": await DetenerAsync(a); a = null; break;
                        case "restaurar":
                            a = IniciarSalud("a", true, simulador.Puerto, claves, salida);
                            await EsperarAsync(5051, "req16-a", salida); break;
                        default: throw new InvalidOperationException("Acción de prueba desconocida.");
                    }
                    await File.WriteAllTextAsync(Path.Combine(runtime, "resultado.json"), JsonSerializer.Serialize(new { id }));
                }
            }
            await VerificarUnicidadAsync();
        }
        finally
        {
            await DetenerAsync(a); await DetenerAsync(b);
            await File.WriteAllTextAsync(Path.Combine(runtime, "finalizado.json"), "{}");
        }
    }

    private async Task PrepararAsync()
    {
        await database.ReiniciarAsync();
        await using var db = database.CrearContexto(); await db.LecturasPanel.ExecuteDeleteAsync();
    }
    private static Process IniciarSalud(string instancia, bool fuente, int puertoTcp, string claves, ConcurrentQueue<string> salida)
        => Iniciar("req16-" + instancia, instancia == "a" ? 5051 : 5052, fuente, claves, salida, new Dictionary<string, string>
        {
            ["InversorTcp__Port"] = puertoTcp.ToString(), ["RecoleccionLecturas__IntervaloSegundos"] = "1",
            ["SaludPanel__Habilitada"] = fuente.ToString(), ["SaludPanel__EvaluacionSegundos"] = "1",
            ["SaludPanel__IntervalosDesconexion"] = "10", ["SaludDistribucion__IntervaloSegundos"] = "1",
            ["TelemetriaEnVivo__IntervaloSegundos"] = "1", ["TelemetriaEnVivo__ActualizarInventarioSegundos"] = "1"
        });
    private async Task EsperarCondicionAsync(EstadoSaludPanel condicion)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            await using var db = database.CrearContexto();
            if (await db.Set<SaludPanel>().AnyAsync(s => s.PanelId == database.Panel.Id && s.Estado == condicion, limite.Token)) return;
            await Task.Delay(100, limite.Token);
        }
    }
    private async Task AvisoSaludAsync(ClientWebSocket socket)
    {
        var fin = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < fin)
        {
            var mensaje = await RecibirAsync(socket);
            if (mensaje.TryGetProperty("target", out var target) && target.GetString() == "SaludActualizada")
            { Assert.Equal(database.Parque.Id, mensaje.GetProperty("arguments")[0].GetGuid()); return; }
        }
        Assert.Fail("No se recibió invalidación de salud en esta instancia.");
    }
    private static async Task AfinidadAsync(HttpClient cliente, CookieContainer cookies, Uri proxy, string instancia)
    {
        var fin = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < fin)
        {
            cookies.Add(proxy, new Cookie("SolarUYInstancia", instancia, "/"));
            if (await InstanciaAsync(cliente, "/lib/signalr/8.0.17/signalr.min.js") == "req16-" + instancia) return;
            await Task.Delay(100);
        }
        Assert.Fail("No se pudo fijar el cliente en su instancia de prueba.");
    }
    private async Task VerificarUnicidadAsync()
    {
        await using var db = database.CrearContexto();
        Assert.False(await db.LecturasPanel.GroupBy(x => new { x.PanelId, x.FechaHora }).AnyAsync(g => g.Count() > 1));
        Assert.False(await db.Set<CambioEstadoSaludPanel>().GroupBy(x => new { x.PanelId, x.Version }).AnyAsync(g => g.Count() > 1));
        Assert.False(await db.Set<AlertaPanel>().Where(x => x.CierreUtc == null).GroupBy(x => x.PanelId).AnyAsync(g => g.Count() > 1));
    }
    private static async Task DetenerAsync(Process? proceso)
    {
        if (proceso is null) return;
        if (!proceso.HasExited) { proceso.Kill(entireProcessTree: true); await proceso.WaitForExitAsync(); }
        proceso.Dispose();
    }
    internal sealed class SimuladorTemporal(HistoricoDatabase database) : IAsyncDisposable
    {
        private CancellationTokenSource? _fin;
        private Task? _ejecucion;
        public int Puerto { get; } = PuertoLibre();
        public async Task EstablecerFallaAsync(bool falla)
        {
            await ApagarAsync();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["InversorSimulador:PanelesConFalla"] = falla ? database.Panel.NumeroSerie : "" }).Build();
            var motor = new InversorSimulador(config);
            await motor.SincronizarInventarioAsync(await new InversorInventarioRepository(new Factory(database)).ListarAsync());
            var servidor = new InversorTcpServer(motor, Options.Create(new InversorTcpOptions { Port = Puerto }), NullLogger<InversorTcpServer>.Instance);
            _fin = new CancellationTokenSource(); _ejecucion = servidor.EjecutarAsync(_fin.Token);
            if (_ejecucion.IsCompleted) await _ejecucion;
        }
        private async Task ApagarAsync()
        {
            if (_fin is null) return;
            await _fin.CancelAsync();
            try { if (_ejecucion is not null) await _ejecucion; }
            catch (OperationCanceledException) { }
            _fin.Dispose(); _fin = null;
        }
        public ValueTask DisposeAsync() => new(ApagarAsync());
        private static int PuertoLibre()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
    private sealed class Factory(HistoricoDatabase database) : IDbContextFactory<ApplicationDbContext>
    { public ApplicationDbContext CreateDbContext() => database.CrearContexto(); }
}
public sealed class SaludNginxTheoryAttribute : TheoryAttribute
{
    public SaludNginxTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SOLARUY_MONITOREO_PROXY_URL"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Requiere el Nginx de pruebas en 5051/5052 y una MariaDB aislada.";
    }
}
public sealed class SaludDistribuidaNavegadorFactAttribute : FactAttribute
{
    public SaludDistribuidaNavegadorFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOLARUY_SALUD_DISTRIBUIDA_BROWSER") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Fixture de la suite de navegador con dos instancias reales y Nginx.";
    }
}
