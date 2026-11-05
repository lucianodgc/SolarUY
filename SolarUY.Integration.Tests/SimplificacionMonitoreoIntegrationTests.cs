using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Infrastructure.Inversores;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class SimplificacionMonitoreoIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task RetirarAvisos_ConservaInventarioYTareasYLecturas()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        var lecturasAntes = await context.LecturasPanel.AsNoTracking().OrderBy(l => l.Id).ToArrayAsync();
        var panelesAntes = await context.Paneles.CountAsync();
        var nodosAntes = await context.Nodos.CountAsync();
        var parquesAntes = await context.ParquesSolares.CountAsync();
        var tareasAntes = await context.TareasMantenimiento.CountAsync();
        var migrador = context.GetService<IMigrator>();
        await migrador.MigrateAsync("20261029140000_AddHistoricalNodeCoverage");
        try
        {
            await context.Database.MigrateAsync();
            await context.Database.OpenConnectionAsync();
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('AvisosMonitoreo', 'CursoresMonitoreo', 'SecuenciaMonitoreo')";
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
            var lecturasDespues = await context.LecturasPanel.AsNoTracking().OrderBy(l => l.Id).ToArrayAsync();
            Assert.Equal(lecturasAntes.Select(l => (l.Id, l.PanelId, l.NodoId, l.FechaHora, l.PotenciaGeneradaW, l.EstadoSalud)),
                lecturasDespues.Select(l => (l.Id, l.PanelId, l.NodoId, l.FechaHora, l.PotenciaGeneradaW, l.EstadoSalud)));
            Assert.Equal(panelesAntes, await context.Paneles.CountAsync());
            Assert.Equal(nodosAntes, await context.Nodos.CountAsync());
            Assert.Equal(parquesAntes, await context.ParquesSolares.CountAsync());
            Assert.Equal(tareasAntes, await context.TareasMantenimiento.CountAsync());
        }
        finally { await context.Database.MigrateAsync(); }
    }

    [MariaDbHistoricoFact]
    public async Task Recolectar_SinTablasDeAvisos_GuardaLecturasSinDuplicarlas()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        var repository = new RecoleccionLecturasRepository(new HistoricoContextFactory(database));
        var servicio = new RecoleccionLecturasService(repository, new InversorSimulador(new ConfigurationBuilder().Build()),
            new RelojRecoleccion(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero)), new ConfiguracionRecoleccion(30));
        var resultado = await servicio.RecolectarAsync();
        Assert.Equal(4, resultado.LecturasGuardadas);
        Assert.Equal(4, await context.LecturasPanel.CountAsync());
        Assert.Equal(0, (await servicio.RecolectarAsync()).LecturasGuardadas);
    }

    private sealed class HistoricoContextFactory(HistoricoDatabase database) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => database.CrearContexto();
    }

    private sealed class RelojRecoleccion(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
