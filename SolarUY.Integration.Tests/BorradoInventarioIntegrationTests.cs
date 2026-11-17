using Microsoft.EntityFrameworkCore;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Domain.Salud;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class BorradoInventarioIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HistorialDeSaludImpideBorradoSinEliminarDependencias(bool borrarParque)
    {
        await database.ReiniciarAsync();
        await using (var db = database.CrearContexto())
        {
            var panel = await db.Paneles.SingleAsync(p => p.Id == database.Panel.Id);
            db.Add(new SaludPanel(panel, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        if (borrarParque)
        {
            await using var db = database.CrearContexto();
            var parque = await db.ParquesSolares.SingleAsync(p => p.Id == database.Parque.Id);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.EliminarParqueSolarEnCascadaAsync(parque));
            Assert.Contains("historial de salud", error.Message);
        }
        else
        {
            var repository = new NodoRepository(new Factory(database));
            var nodo = await repository.ObtenerParaEliminarAsync(database.Panel.NodoId);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.EliminarEnCascadaAsync(nodo!));
            Assert.Contains("historial de salud", error.Message);
        }
        await using var comprobar = database.CrearContexto();
        Assert.Equal(4, await comprobar.Paneles.CountAsync());
        Assert.Equal(10, await comprobar.LecturasPanel.CountAsync());
        Assert.Single(await comprobar.Set<SaludPanel>().ToArrayAsync());
    }

    [MariaDbHistoricoFact]
    public async Task BorrarParqueSinSaludEliminaInversorYConservaElOtroParque()
    {
        await database.ReiniciarAsync();
        await using var db = database.CrearContexto();
        var nodo = await db.Nodos.SingleAsync(n => n.Id == database.Panel.NodoId);
        db.Inversores.Add(new Inversor(nodo, "Marca", "Modelo", 10, EstadoInversor.Encendido, false));
        await db.SaveChangesAsync();
        var parque = await db.ParquesSolares.SingleAsync(p => p.Id == database.Parque.Id);
        await db.EliminarParqueSolarEnCascadaAsync(parque);
        db.ChangeTracker.Clear();
        Assert.False(await db.Inversores.AnyAsync());
        Assert.Single(await db.ParquesSolares.ToArrayAsync());
        Assert.Equal(database.OtroParque.Id, (await db.ParquesSolares.SingleAsync()).Id);
        Assert.Single(await db.Paneles.ToArrayAsync());
    }

    private sealed class Factory(HistoricoDatabase database) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => database.CrearContexto();
        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
