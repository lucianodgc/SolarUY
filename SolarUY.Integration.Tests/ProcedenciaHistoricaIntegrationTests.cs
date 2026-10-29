using Microsoft.EntityFrameworkCore;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class ProcedenciaHistoricaIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task Legado_SeConsultaPorPanelSinAtribuirloANodosNiParques()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        var fecha = new DateTime(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);
        var id = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO LecturasPanel (Id, PanelId, NodoId, FechaHora, PotenciaGeneradaW, EstadoSalud)
            VALUES ({id}, {database.Panel.Id}, NULL, {fecha}, 1200.123456, 1)
            """);
        var repo = new HistoricoGeneracionRepository(new FabricaContexto(database));
        foreach (var tipo in Enum.GetValues<TipoObjetivoHistorico>())
        {
            var objetivo = tipo switch
            {
                TipoObjetivoHistorico.Panel => database.Panel.Id,
                TipoObjetivoHistorico.Nodo => database.Panel.NodoId,
                _ => database.Parque.Id
            };
            var resultado = await repo.ConsultarAsync(new(tipo, objetivo, fecha, fecha.AddMinutes(5),
                [new(fecha, fecha.AddMinutes(5), "Legado")]));
            Assert.Equal(1, resultado.LecturasSinNodoHistorico);
            if (tipo == TipoObjetivoHistorico.Panel) Assert.True(resultado.EnergiaKWh > 0);
            else Assert.Null(resultado.EnergiaKWh);
        }
        var lectura = await context.LecturasPanel.SingleAsync(l => l.Id == id);
        Assert.Null(lectura.NodoId);
        Assert.Equal(1200.123456m, lectura.PotenciaGeneradaW);
        Assert.Equal(fecha, lectura.FechaHora);
        Assert.Equal(EstadoSaludLectura.Fallado, lectura.EstadoSalud);
    }

    [MariaDbHistoricoFact]
    public async Task NodoVerificado_NoSigueAlPanelCuandoCambiaDeNodo()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        var fecha = new DateTime(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);
        var panel = await context.Paneles.Include(p => p.Nodo).SingleAsync(p => p.Id == database.Panel.Id);
        context.LecturasPanel.Add(new(panel, fecha, 1200, EstadoSaludLectura.Sano));
        await context.SaveChangesAsync();
        await context.Paneles.Where(p => p.Id == panel.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.NodoId, database.PanelAjeno.NodoId));
        var repo = new HistoricoGeneracionRepository(new FabricaContexto(database));
        foreach (var (nodo, esperado) in new[] { (database.Panel.NodoId, true), (database.PanelAjeno.NodoId, false) })
        {
            var resultado = await repo.ConsultarAsync(new(TipoObjetivoHistorico.Nodo, nodo, fecha, fecha.AddMinutes(5),
                [new(fecha, fecha.AddMinutes(5), "Movimiento")]));
            if (esperado) Assert.Equal(0.1m, resultado.EnergiaKWh);
            else Assert.Null(resultado.EnergiaKWh);
        }
    }

    [MariaDbHistoricoFact]
    public async Task LecturaSinNodo_InterrumpeLaInterpolacionDelNodoConocido()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        var fecha = new DateTime(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);
        var panel = await context.Paneles.Include(p => p.Nodo).SingleAsync(p => p.Id == database.Panel.Id);
        context.LecturasPanel.Add(new(panel, fecha, 1200, EstadoSaludLectura.Sano));
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO LecturasPanel (Id, PanelId, NodoId, FechaHora, PotenciaGeneradaW, EstadoSalud)
            VALUES ({Guid.NewGuid()}, {panel.Id}, NULL, {fecha.AddMinutes(1)}, 1200, 0)
            """);
        var repo = new HistoricoGeneracionRepository(new FabricaContexto(database));
        var resultado = await repo.ConsultarAsync(new(TipoObjetivoHistorico.Nodo, panel.NodoId, fecha, fecha.AddMinutes(5),
            [new(fecha, fecha.AddMinutes(5), "Cambio sin evidencia")]));
        Assert.Equal(0.02m, resultado.EnergiaKWh);
        Assert.Equal(1, resultado.LecturasSinNodoHistorico);
        Assert.Equal(CoberturaHistorico.Parcial, resultado.Cobertura);
    }

    private sealed class FabricaContexto(HistoricoDatabase database) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => database.CrearContexto();
    }
}
