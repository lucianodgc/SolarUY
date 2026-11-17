using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SolarUY.Domain.Entities;
using static SolarUY.Integration.Tests.MonitoreoNginxIntegrationTests;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class OrdenNginxIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [OrdenNavegadorFact]
    public async Task ServidorTemporalOrdenesParaPlaywright()
    {
        await database.ReiniciarAsync();
        await using var simulador = new SaludNginxIntegrationTests.SimuladorTemporal(database);
        await simulador.EstablecerFallaAsync(false);
        var runtime = Path.Combine(HistoricoDatabase.RutaProyecto("SolarUY.Browser.Tests"), ".runtime", "ordenes");
        Directory.CreateDirectory(runtime);
        foreach (var nombre in new[] { "control.json", "resultado.json", "finalizado.json" }) File.Delete(Path.Combine(runtime, nombre));
        var claves = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "solaruy-ordenes-" + Guid.NewGuid().ToString("N"))).FullName;
        await File.WriteAllTextAsync(Path.Combine(runtime, "sesion.json"), JsonSerializer.Serialize(new
        {
            cookie = CrearCookie(claves, "Administrador"), consulta = CrearCookie(claves), tecnico = CrearCookie(claves, "Tecnico"),
            parque = database.Parque.Id, otro = database.OtroParque.Id, panel = database.Panel.Id,
            proxy = Environment.GetEnvironmentVariable("SOLARUY_MONITOREO_PROXY_URL")
        }));
        var salida = new ConcurrentQueue<string>(); Process? a = null; Process? b = null;
        Process IniciarOrden(string instancia) => Iniciar("req17-" + instancia, instancia == "a" ? 5051 : 5052, true, claves, salida,
            new Dictionary<string, string>
            {
                ["InversorTcp__Port"] = simulador.Puerto.ToString(), ["RecoleccionLecturas__IntervaloSegundos"] = "1",
                ["TelemetriaEnVivo__IntervaloSegundos"] = "1", ["TelemetriaEnVivo__ActualizarInventarioSegundos"] = "1",
                ["SaludPanel__EvaluacionSegundos"] = "1", ["SaludDistribucion__IntervaloSegundos"] = "1"
            });
        try
        {
            a = IniciarOrden("a"); b = IniciarOrden("b");
            await EsperarAsync(5051, "req17-a", salida); await EsperarAsync(5052, "req17-b", salida);
            string? ultimo = null;
            var fin = DateTime.UtcNow.AddMinutes(5);
            while (DateTime.UtcNow < fin)
            {
                JsonDocument doc;
                try { doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime, "control.json"))); }
                catch (IOException) { await Task.Delay(50); continue; }
                catch (JsonException) { await Task.Delay(50); continue; }
                using (doc)
                {
                    var id = doc.RootElement.GetProperty("id").GetString();
                    if (id == ultimo) { await Task.Delay(50); continue; }
                    ultimo = id; var accion = doc.RootElement.GetProperty("accion").GetString();
                    if (accion == "terminar") break;
                    if (accion == "relevar") { await DetenerAsync(a); a = null; }
                    if (accion == "restaurar") { a = IniciarOrden("a"); await EsperarAsync(5051, "req17-a", salida); }
                    await using var db = database.CrearContexto();
                    var intentos = await db.Set<IntentoOrdenInversor>().AsNoTracking().OrderBy(i => i.SolicitadaUtc).ToArrayAsync();
                    await File.WriteAllTextAsync(Path.Combine(runtime, "resultado.json"), JsonSerializer.Serialize(new
                    { id, cantidad = intentos.Length, confirmadas = intentos.Count(i => i.Estado == EstadoOrdenInversor.Confirmada),
                        actores = intentos.Select(i => i.ActorSub).Distinct().ToArray() }));
                }
            }
            await using var final = database.CrearContexto();
            Assert.False(await final.Set<IntentoOrdenInversor>().GroupBy(i => new { i.ActorSub, i.Correlacion }).AnyAsync(g => g.Count() > 1));
        }
        finally
        {
            await DetenerAsync(a); await DetenerAsync(b);
            await File.WriteAllTextAsync(Path.Combine(runtime, "finalizado.json"), "{}");
        }
    }

    private static async Task DetenerAsync(Process? proceso)
    {
        if (proceso is null) return;
        if (!proceso.HasExited) { proceso.Kill(entireProcessTree: true); await proceso.WaitForExitAsync(); }
        proceso.Dispose();
    }
}

public sealed class OrdenNavegadorFactAttribute : FactAttribute
{
    public OrdenNavegadorFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOLARUY_ORDENES_BROWSER") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = "Requiere la suite de navegador aislada de req017 y MariaDB de pruebas.";
    }
}
