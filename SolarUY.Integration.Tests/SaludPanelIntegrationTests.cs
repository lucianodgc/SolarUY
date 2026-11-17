using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SolarUY.Presentation.Monitoreo;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Salud;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Domain.Salud;
using SolarUY.Infrastructure.Persistence;
using EstadoSaludPanel = SolarUY.Domain.Salud.EstadoSaludPanel;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class SaludPanelIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    private static readonly DateTime Ahora = new(2027, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly PoliticaSalud _politica = new(TimeSpan.FromSeconds(90));
    private SaludPanelRepository Repository() => new(new Factory(database), new EvaluacionSaludService());
    private async Task PrepararAsync()
    {
        await database.ReiniciarAsync();
        await using var db = database.CrearContexto(); await db.LecturasPanel.ExecuteDeleteAsync();
    }
    private async Task MedirAsync(DateTime instante, EstadoSaludLectura senal)
    {
        await using var db = database.CrearContexto();
        var panel = await db.Paneles.Include(p => p.Nodo).SingleAsync(p => p.Id == database.Panel.Id);
        db.LecturasPanel.Add(new LecturaPanel(panel, instante, senal == EstadoSaludLectura.Fallado ? 0 : 100, senal));
        await db.SaveChangesAsync();
    }
    private Task<IReadOnlyList<AvisoSaludConfirmada>> EvaluarAsync(DateTime instante)
        => Repository().EvaluarLoteAsync([database.Panel.Id], instante, _politica, default);

    [MariaDbHistoricoFact]
    public async Task MigracionIncrementalConservaLecturasSinInventarHistoriaDeSalud()
    {
        await database.ReiniciarAsync();
        await using var db = database.CrearContexto();
        var anteriores = await db.LecturasPanel.AsNoTracking().OrderBy(l => l.Id)
            .Select(l => new { l.Id, l.FechaHora, l.EstadoSalud, l.PotenciaGeneradaW, l.NodoId }).ToArrayAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261105010000_RemoveDistribucionMonitoreo");
        try { await db.Database.MigrateAsync(); }
        finally { await db.Database.MigrateAsync(); }
        var posteriores = await db.LecturasPanel.AsNoTracking().OrderBy(l => l.Id)
            .Select(l => new { l.Id, l.FechaHora, l.EstadoSalud, l.PotenciaGeneradaW, l.NodoId }).ToArrayAsync();
        Assert.Equal(anteriores, posteriores);
        Assert.Empty(await db.Set<CambioEstadoSaludPanel>().ToArrayAsync());
        Assert.Empty(await db.Set<AlertaPanel>().ToArrayAsync());
    }

    [MariaDbHistoricoFact]
    public async Task ReinicioTrasLecturaSinEvaluarReconciliacionNoRepiteAdquisicionYExpiraLaVista()
    {
        await PrepararAsync(); await MedirAsync(Ahora, EstadoSaludLectura.Fallado);
        var servicio = new SaludPanelService(Repository(), _politica, new Reloj(Ahora));
        Assert.NotEmpty(await servicio.EvaluarAsync());
        await new SaludPanelService(Repository(), _politica, new Reloj(Ahora.AddSeconds(1))).EvaluarAsync();
        await using var db = database.CrearContexto();
        Assert.Equal(1, await db.LecturasPanel.CountAsync());
        Assert.Single(await db.Set<CambioEstadoSaludPanel>().ToArrayAsync());
        var consulta = new ConsultaSaludService(Repository(), new Usuario("Consulta", true), new SaludNodoService(),
            new Reloj(Ahora.AddSeconds(30)), new ConfiguracionConsultaSalud(TimeSpan.FromSeconds(15)));
        var estado = await consulta.ConsultarAsync(database.Parque.Id);
        Assert.False(estado.Paneles.Single(p => p.PanelId == database.Panel.Id).Vigente);
        Assert.False(Assert.Single(estado.Alertas).Vigente);
    }

    [MariaDbHistoricoFact]
    public async Task AvisoLocalFallidoSeReintentaSinRepetirTransicionNiAdquisicion()
    {
        await PrepararAsync(); await MedirAsync(Ahora, EstadoSaludLectura.Fallado);
        var servicios = new ServiceCollection();
        servicios.AddScoped<ISaludPanelService>(_ => new SaludPanelService(Repository(), _politica, new Reloj(Ahora)));
        await using var provider = servicios.BuildServiceProvider();
        var entrega = new EntregaFallida();
        using var worker = new SaludPanelWorker(provider.GetRequiredService<IServiceScopeFactory>(), entrega,
            Options.Create(new SaludPanelOptions { EvaluacionSegundos = 1 }), NullLogger<SaludPanelWorker>.Instance);
        await worker.StartAsync(default);
        try { await entrega.Reintentada.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { await worker.StopAsync(default); }
        await using var db = database.CrearContexto();
        Assert.Equal(1, await db.LecturasPanel.CountAsync()); Assert.Single(await db.Set<CambioEstadoSaludPanel>().ToArrayAsync());
        Assert.Single(await db.Set<AlertaPanel>().ToArrayAsync());
    }

    [MariaDbHistoricoFact]
    public async Task EpisodiosSonUnicosYConservanHistorialTrasRecuperacionYRecurrencia()
    {
        await PrepararAsync(); await MedirAsync(Ahora, EstadoSaludLectura.Fallado);
        await EvaluarAsync(Ahora); await EvaluarAsync(Ahora.AddSeconds(1));
        await using var db = database.CrearContexto();
        Assert.Single(await db.Set<AlertaPanel>().ToArrayAsync()); Assert.Single(await db.Set<CambioEstadoSaludPanel>().ToArrayAsync());
        Assert.Null((await db.Set<CambioEstadoSaludPanel>().SingleAsync()).EstadoAnterior);
        await MedirAsync(Ahora.AddSeconds(10), EstadoSaludLectura.Sano); await EvaluarAsync(Ahora.AddSeconds(10));
        Assert.Empty(await db.Set<AlertaPanel>().Where(a => a.CierreUtc == null).ToArrayAsync());
        await MedirAsync(Ahora.AddSeconds(20), EstadoSaludLectura.Fallado); await EvaluarAsync(Ahora.AddSeconds(20));
        Assert.Equal(2, await db.Set<AlertaPanel>().CountAsync()); Assert.Equal(3, await db.Set<CambioEstadoSaludPanel>().CountAsync());
        Assert.Single(await db.Set<AlertaPanel>().Where(a => a.CierreUtc == null).ToArrayAsync());
        Assert.Equal(3, await db.LecturasPanel.CountAsync());
    }
    [MariaDbHistoricoFact]
    public async Task EvaluadoresConcurrentesCreanUnaSolaTransicionYLaBaseImpideOtraAlertaActiva()
    {
        await PrepararAsync(); await MedirAsync(Ahora, EstadoSaludLectura.Fallado);
        await Task.WhenAll(EvaluarAsync(Ahora), EvaluarAsync(Ahora));
        await using var db = database.CrearContexto();
        Assert.Single(await db.Set<SaludPanel>().ToArrayAsync()); Assert.Single(await db.Set<CambioEstadoSaludPanel>().ToArrayAsync());
        var panel = await db.Paneles.Include(p => p.Nodo).SingleAsync(p => p.Id == database.Panel.Id);
        db.Add(new AlertaPanel(panel, EstadoSaludPanel.Desconectado, Ahora));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [MariaDbHistoricoFact]
    public async Task SinEventosDetectaDesconexionYUnaLecturaPosteriorRecuperaSinRevertirPorAtrasadas()
    {
        await PrepararAsync(); await EvaluarAsync(Ahora);
        await using var db = database.CrearContexto();
        Assert.Empty(await db.Set<CambioEstadoSaludPanel>().ToArrayAsync());
        await EvaluarAsync(Ahora.AddSeconds(90));
        Assert.Equal(EstadoSaludPanel.Desconectado, (await db.Set<SaludPanel>().AsNoTracking().SingleAsync()).Estado);
        await MedirAsync(Ahora.AddSeconds(89), EstadoSaludLectura.Sano); await EvaluarAsync(Ahora.AddSeconds(91));
        await MedirAsync(Ahora.AddSeconds(20), EstadoSaludLectura.Fallado); await EvaluarAsync(Ahora.AddSeconds(92));
        Assert.Equal(EstadoSaludPanel.Normal, (await db.Set<SaludPanel>().AsNoTracking().SingleAsync()).Estado);
        Assert.Equal(2, await db.Set<CambioEstadoSaludPanel>().CountAsync());
    }
    [MariaDbHistoricoFact]
    public async Task TrasladoExcluyeEvidenciaAnteriorYMuestraLaTopologiaActualSinReescribirLecturas()
    {
        await PrepararAsync(); await MedirAsync(Ahora, EstadoSaludLectura.Fallado); await EvaluarAsync(Ahora);
        await using var db = database.CrearContexto();
        var nodoOriginal = database.Panel.NodoId;
        await db.Paneles.Where(p => p.Id == database.Panel.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.NodoId, database.TercerPanel.NodoId));
        await EvaluarAsync(Ahora.AddSeconds(1));
        var consulta = await Repository().ConsultarAsync(database.Parque.Id, Ahora.AddSeconds(1), TimeSpan.FromSeconds(15), default);
        var panel = consulta!.Paneles.Single(p => p.PanelId == database.Panel.Id);
        Assert.False(panel.Vigente); Assert.Equal(database.TercerPanel.NodoId, panel.NodoId);
        Assert.Equal(database.TercerPanel.NodoId, Assert.Single(consulta.Alertas).NodoId);
        Assert.Equal(nodoOriginal, (await db.LecturasPanel.SingleAsync()).NodoId);
        await EvaluarAsync(Ahora.AddSeconds(91));
        Assert.Equal(EstadoSaludPanel.Desconectado, (await db.Set<SaludPanel>().SingleAsync()).Estado);
        await MedirAsync(Ahora.AddSeconds(92), EstadoSaludLectura.Sano); await EvaluarAsync(Ahora.AddSeconds(92));
        Assert.Empty(await db.Set<AlertaPanel>().Where(a => a.CierreUtc == null).ToArrayAsync());
    }
    [MariaDbHistoricoFact]
    public async Task FalloTransaccionalNoDejaEstadoSinSuAlertaYSeRecuperaAlReintentar()
    {
        await PrepararAsync(); await MedirAsync(Ahora, EstadoSaludLectura.Fallado);
        await using var db = database.CrearContexto();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER req016_fallo BEFORE INSERT ON AlertasPanel FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Fallo de prueba'");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => EvaluarAsync(Ahora));
            Assert.Empty(await db.Set<SaludPanel>().ToArrayAsync()); Assert.Empty(await db.Set<CambioEstadoSaludPanel>().ToArrayAsync());
            Assert.Equal(1, await db.LecturasPanel.CountAsync());
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS req016_fallo"); }
        await EvaluarAsync(Ahora);
        Assert.Single(await db.Set<AlertaPanel>().ToArrayAsync());
    }
    [MariaDbHistoricoTheory]
    [InlineData("Administrador")]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task LectoresConsultanSoloSuSeleccionYElHistorialSePagina(string rol)
    {
        await PrepararAsync(); await MedirAsync(Ahora, EstadoSaludLectura.Fallado); await EvaluarAsync(Ahora);
        var servicio = new ConsultaSaludService(Repository(), new Usuario(rol, true), new SaludNodoService(),
            new Reloj(Ahora), new ConfiguracionConsultaSalud(TimeSpan.FromSeconds(15)));
        var estado = await servicio.ConsultarAsync(database.Parque.Id);
        Assert.Single(estado.Alertas); Assert.Equal(3, estado.Paneles.Count);
        Assert.Single((await servicio.HistorialAsync(database.Parque.Id, database.Panel.Id, 0, 1)).Elementos);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servicio.HistorialAsync(database.Parque.Id, database.PanelAjeno.Id));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => servicio.HistorialAsync(database.Parque.Id, database.Panel.Id, 0, 101));
    }
    [MariaDbHistoricoTheory]
    [InlineData("Administrador", false)]
    [InlineData("Invitado", true)]
    public async Task RechazaConsultasNoAutorizadas(string rol, bool autenticado)
    {
        var servicio = new ConsultaSaludService(Repository(), new Usuario(rol, autenticado), new SaludNodoService(),
            new Reloj(Ahora), new ConfiguracionConsultaSalud(TimeSpan.FromSeconds(15)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ConsultarAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.HistorialAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
    private sealed class Factory(HistoricoDatabase database) : IDbContextFactory<ApplicationDbContext>
    { public ApplicationDbContext CreateDbContext() => database.CrearContexto(); }
    private sealed class Usuario(string rol, bool autenticado) : IUsuarioActual
    { public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default) => Task.FromResult(new UsuarioActualDto(autenticado, "test", [rol])); }
    private sealed class Reloj(DateTime ahora) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(ahora); }
    private sealed class EntregaFallida : INotificadorSaludConfirmada
    {
        private int _intentos;
        public TaskCompletionSource Reintentada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task NotificarAsync(IReadOnlyList<AvisoSaludConfirmada> avisos, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _intentos) > 1) Reintentada.TrySetResult();
            return Task.FromException(new InvalidOperationException("Fallo de envío controlado."));
        }
    }
}
