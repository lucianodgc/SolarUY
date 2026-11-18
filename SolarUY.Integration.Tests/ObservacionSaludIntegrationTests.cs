using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SolarUY.Application.Salud;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Domain.Salud;
using SolarUY.Infrastructure.Persistence;
using SolarUY.Presentation.Monitoreo;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class ObservacionSaludIntegrationTests(HistoricoDatabase database, ITestOutputHelper salida) : IClassFixture<HistoricoDatabase>
{
    private static readonly DateTime Ahora = new(2027, 2, 1, 12, 0, 0, DateTimeKind.Utc);
    private ObservacionSaludRepository Repository() => new(new Factory(database));
    private static ObservacionSaludWorker CrearWorker(ServiceProvider provider, INotificadorSaludConfirmada entrega)
        => new(provider.GetRequiredService<IServiceScopeFactory>(), entrega,
            Options.Create(new SaludDistribucionOptions()), NullLogger<ObservacionSaludWorker>.Instance);
    private ServiceProvider Provider()
    {
        var servicios = new ServiceCollection();
        servicios.AddScoped<IObservacionSaludRepository>(_ => Repository());
        return servicios.BuildServiceProvider(validateScopes: true);
    }
    private async Task MedirAsync(DateTime instante, EstadoSaludLectura senal)
    {
        await using var db = database.CrearContexto();
        var panel = await db.Paneles.Include(p => p.Nodo).SingleAsync(p => p.Id == database.Panel.Id);
        db.LecturasPanel.Add(new LecturaPanel(panel, instante, 10, senal)); await db.SaveChangesAsync();
        await new SaludPanelRepository(new Factory(database), new EvaluacionSaludService()).EvaluarLoteAsync(
            [panel.Id], instante, new PoliticaSalud(TimeSpan.FromSeconds(90)), default);
    }
    private async Task PrepararAsync()
    {
        await database.ReiniciarAsync();
        await using var db = database.CrearContexto(); await db.LecturasPanel.ExecuteDeleteAsync();
    }

    [MariaDbHistoricoTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DosObservadoresRecuperanCommitSinAvisoYReintentanSinConsumirseElProgreso(bool cancelacionDeTransporte)
    {
        await PrepararAsync(); await MedirAsync(Ahora, EstadoSaludLectura.Fallado);
        await using var provider = Provider();
        var entregaA = new Entrega { FallarPrimera = true, CancelacionDeTransporte = cancelacionDeTransporte }; var entregaB = new Entrega();
        using var a = CrearWorker(provider, entregaA); using var b = CrearWorker(provider, entregaB);
        await a.StartAsync(default); await b.StartAsync(default);
        try
        {
            await EsperarAsync(() => entregaA.Avisos.Any(x => x.PanelId == database.Panel.Id && x.Version == 1)
                && entregaB.Avisos.Any(x => x.PanelId == database.Panel.Id && x.Version == 1));
            Assert.True(entregaA.Intentos >= 2);
            await a.StopAsync(default);
            await MedirAsync(Ahora.AddSeconds(1), EstadoSaludLectura.Sano);
            await EsperarAsync(() => entregaB.Avisos.Any(x => x.PanelId == database.Panel.Id && x.Version == 2));
            var recuperada = new Entrega();
            using var reiniciado = CrearWorker(provider, recuperada); await reiniciado.StartAsync(default);
            try { await EsperarAsync(() => recuperada.Avisos.Any(x => x.PanelId == database.Panel.Id && x.Version == 2)); }
            finally { await reiniciado.StopAsync(default); }
            await using var db = database.CrearContexto();
            Assert.Equal(2, await db.LecturasPanel.CountAsync()); Assert.Equal(2, await db.Set<CambioEstadoSaludPanel>().CountAsync());
            Assert.Empty(await db.Set<AlertaPanel>().Where(x => x.CierreUtc == null).ToArrayAsync());
        }
        finally { await a.StopAsync(default); await b.StopAsync(default); }
    }

    [MariaDbHistoricoFact]
    public async Task TrasladoYRetiradaInvalidanParquesAnteriorYActualSinEscribirSalud()
    {
        await PrepararAsync(); await MedirAsync(Ahora, EstadoSaludLectura.Fallado);
        await using var provider = Provider(); var entrega = new Entrega();
        using var worker = CrearWorker(provider, entrega); await worker.StartAsync(default);
        try
        {
            await EsperarAsync(() => entrega.Avisos.Any(x => x.PanelId == database.Panel.Id));
            entrega.Avisos.Clear();
            await using var db = database.CrearContexto();
            await db.Paneles.Where(p => p.Id == database.Panel.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.NodoId, database.PanelAjeno.NodoId));
            await EsperarAsync(() => entrega.Avisos.Any(x => x.PanelId == database.Panel.Id && x.ParqueId == database.Parque.Id)
                && entrega.Avisos.Any(x => x.PanelId == database.Panel.Id && x.ParqueId == database.OtroParque.Id));
            Assert.Equal(1, (await db.Set<SaludPanel>().SingleAsync()).Version);
            // Panel sin datos ni dependencias: su retirada también debe invalidar el parque previo.
            entrega.Avisos.Clear();
            await db.Paneles.Where(p => p.Id == database.TercerPanel.Id).ExecuteDeleteAsync();
            await EsperarAsync(() => entrega.Avisos.Any(x => x.PanelId == database.TercerPanel.Id && x.ParqueId == database.Parque.Id));
            var intentos = entrega.Intentos;
            await Task.Delay(1300);
            Assert.Equal(intentos, entrega.Intentos);
        }
        finally { await worker.StopAsync(default); }
    }

    [MariaDbHistoricoFact]
    public async Task CambioDuranteLaLecturaSeEntregaEnLaSiguienteInstantanea()
    {
        await PrepararAsync(); await MedirAsync(Ahora, EstadoSaludLectura.Fallado);
        var gate = new LecturaIntervenida(Repository());
        var servicios = new ServiceCollection(); servicios.AddSingleton<IObservacionSaludRepository>(gate);
        await using var provider = servicios.BuildServiceProvider(); var entrega = new Entrega();
        using var worker = CrearWorker(provider, entrega); await worker.StartAsync(default);
        try
        {
            await gate.Leida.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await MedirAsync(Ahora.AddSeconds(1), EstadoSaludLectura.Sano);
            gate.Liberar.TrySetResult();
            await EsperarAsync(() => entrega.Avisos.Any(x => x.PanelId == database.Panel.Id && x.Version == 2));
            Assert.Contains(entrega.Avisos, aviso => aviso.PanelId == database.Panel.Id && aviso.Version == 2);
        }
        finally { gate.Liberar.TrySetResult(); await worker.StopAsync(default); }
    }
    private static async Task EsperarAsync(Func<bool> condicion)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condicion()) await Task.Delay(50, limite.Token);
    }
    private sealed class Factory(HistoricoDatabase database) : IDbContextFactory<ApplicationDbContext>
    { public ApplicationDbContext CreateDbContext() => database.CrearContexto(); }
    private sealed class Entrega : INotificadorSaludConfirmada
    {
        public bool FallarPrimera { get; init; }
        public bool CancelacionDeTransporte { get; init; }
        public ConcurrentQueue<AvisoSaludConfirmada> Avisos { get; } = new();
        private int _intentos;
        public int Intentos => Volatile.Read(ref _intentos);
        public Task NotificarAsync(IReadOnlyList<AvisoSaludConfirmada> avisos, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _intentos) == 1 && FallarPrimera)
            {
                if (CancelacionDeTransporte) throw new OperationCanceledException("Cancelación del transporte, no del host.");
                throw new InvalidOperationException("Fallo de prueba.");
            }
            foreach (var aviso in avisos) Avisos.Enqueue(aviso);
            return Task.CompletedTask;
        }
    }
    private sealed class LecturaIntervenida(IObservacionSaludRepository real) : IObservacionSaludRepository
    {
        private int _lecturas;
        public TaskCompletionSource Leida { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Liberar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<SnapshotSaludDto> LeerSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var datos = await real.LeerSnapshotAsync(cancellationToken);
            if (Interlocked.Increment(ref _lecturas) == 1)
            { Leida.TrySetResult(); await Liberar.Task.WaitAsync(cancellationToken); }
            return datos;
        }
    }

    [MariaDbHistoricoFact]
    public async Task ReinicioInvalidaTambienElParqueQueQuedoVacioDuranteLaCaida()
    {
        await PrepararAsync();
        await using var db = database.CrearContexto();
        await db.Paneles.Where(p => p.Nodo.ParqueId == database.Parque.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.NodoId, database.PanelAjeno.NodoId));
        await using var provider = Provider(); var entrega = new Entrega();
        using var worker = CrearWorker(provider, entrega); await worker.StartAsync(default);
        try
        {
            await EsperarAsync(() => entrega.Avisos.Any(a => a.ParqueId == database.Parque.Id && a.PanelId == null));
            Assert.Contains(entrega.Avisos, aviso => aviso.ParqueId == database.Parque.Id && aviso.PanelId == null);
        }
        finally { await worker.StopAsync(default); }
    }

    [MariaDbHistoricoFact]
    public async Task SnapshotDeInventarioAmpliadoConservaPanelesSinEvaluarYRegistraCosto()
    {
        await PrepararAsync();
        await using var db = database.CrearContexto();
        var nodo = await db.Nodos.SingleAsync(n => n.Id == database.Panel.NodoId);
        for (var i = 0; i < 1000; i++) db.Paneles.Add(new Panel(nodo, "COSTO-" + i, "M", "X", 100, Ahora.AddDays(-1), EstadoPanel.Activo));
        await db.SaveChangesAsync();
        var repository = Repository(); await repository.LeerSnapshotAsync();
        var reloj = Stopwatch.StartNew();
        for (var i = 0; i < 5; i++)
        {
            var snapshot = await repository.LeerSnapshotAsync();
            Assert.Equal(1004, snapshot.Paneles.Count); Assert.Equal(2, snapshot.Parques.Count);
            Assert.All(snapshot.Paneles, p => Assert.Null(p.EvaluadoUtc));
        }
        salida.WriteLine($"MariaDB 11.8 local: snapshot de 1004 paneles, promedio de 5 lecturas: {reloj.Elapsed.TotalMilliseconds / 5:F2} ms. Dos consultas por snapshot, sin límite de rendimiento contractual.");
    }
}
