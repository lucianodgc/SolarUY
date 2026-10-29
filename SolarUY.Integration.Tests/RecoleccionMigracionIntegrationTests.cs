using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;
using SolarUY.Domain.Entities;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class RecoleccionMigracionIntegrationTests
{
    [MariaDbHistoricoFact]
    public async Task Migracion_CompatibleConInstalacionQueYaGuardabaElNodoVerificado()
    {
        var conexion = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)
            ?? throw new InvalidOperationException("Falta la conexión aislada de integración."));
        Assert.StartsWith("solaruy_req007_test", conexion.Database);
        conexion.Database = "solaruy_req007_test_compat_" + Guid.NewGuid().ToString("N");
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(conexion.ConnectionString, new MariaDbServerVersion(new Version(11, 8, 0))).Options);
        try
        {
            var migrador = context.GetService<IMigrator>();
            await migrador.MigrateAsync("20261029023745_AddRecoleccionPeriodica");
            // Reproducir la nulabilidad de la versión antigua, solo en esta base temporal.
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE LecturasPanel MODIFY NodoId CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL");
            var panel = HistoricoDatabase.CrearPanel(new Nodo(new ParqueSolar("Verificado", "Salto"), "Original", "Norte"), "Verificado");
            var lectura = new LecturaPanel(panel, new DateTime(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc), 123.456789m,
                SolarUY.Domain.Enums.EstadoSaludLectura.Fallado);
            context.LecturasPanel.Add(lectura);
            await context.SaveChangesAsync();
            await migrador.MigrateAsync();
            context.ChangeTracker.Clear();
            var despues = await context.LecturasPanel.SingleAsync();
            Assert.Equal(lectura.Id, despues.Id);
            Assert.Equal(panel.NodoId, despues.NodoId);
            Assert.Equal(lectura.FechaHora, despues.FechaHora);
            Assert.Equal(lectura.PotenciaGeneradaW, despues.PotenciaGeneradaW);
            Assert.Equal(lectura.EstadoSalud, despues.EstadoSalud);
            Assert.False(context.Database.HasPendingModelChanges());
        }
        finally { await context.Database.EnsureDeletedAsync(); }
    }

    [MariaDbHistoricoTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migracion_ConservaDatosPreviosSinInventarNodoYRechazaDuplicados(bool duplicados)
    {
        var conexionBase = new MySqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)
                ?? throw new InvalidOperationException("Falta la conexión aislada de integración."));
        Assert.StartsWith("solaruy_req007_test", conexionBase.Database);
        conexionBase.Database = "solaruy_req007_test_migracion_" + Guid.NewGuid().ToString("N");
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(conexionBase.ConnectionString, new MariaDbServerVersion(new Version(11, 8, 0))).Options;
        await using var context = new ApplicationDbContext(opciones);
        try
        {
            var migrador = context.GetService<IMigrator>();
            await migrador.MigrateAsync("20261022130747_AddLecturaPanel");
            var panel = HistoricoDatabase.CrearPanel(new Nodo(new ParqueSolar("Migración", "Salto"), "Nodo", "Norte"), "Migración");
            context.Paneles.Add(panel);
            await context.SaveChangesAsync();
            var id = Guid.NewGuid();
            var fecha = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO LecturasPanel (Id, PanelId, FechaHora, PotenciaGeneradaW, EstadoSalud)
                VALUES ({id}, {panel.Id}, {fecha}, 100, 0)
                """);
            if (duplicados)
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO LecturasPanel (Id, PanelId, FechaHora, PotenciaGeneradaW, EstadoSalud)
                    VALUES ({Guid.NewGuid()}, {panel.Id}, {fecha}, 200, 1)
                    """);

            await context.Database.OpenConnectionAsync();
            if (duplicados)
            {
                var error = await Assert.ThrowsAsync<MySqlException>(() => migrador.MigrateAsync());
                Assert.Contains("duplicadas", error.Message);
                using var columna = context.Database.GetDbConnection().CreateCommand();
                columna.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'LecturasPanel' AND column_name = 'NodoId'";
                Assert.Equal(0, Convert.ToInt32(await columna.ExecuteScalarAsync()));
            }
            using var cantidad = context.Database.GetDbConnection().CreateCommand();
            cantidad.CommandText = "SELECT COUNT(*) FROM LecturasPanel";
            Assert.Equal(duplicados ? 2 : 1, Convert.ToInt32(await cantidad.ExecuteScalarAsync()));
            if (duplicados) return;

            // Una variable aislada no constituye evidencia y no debe atribuir datos.
            await context.Database.ExecuteSqlRawAsync("SET @SolarUY_TopologiaHistoricaVerificada = 1");
            await migrador.MigrateAsync();
            var lectura = await context.LecturasPanel.SingleAsync();
            Assert.Equal(id, lectura.Id);
            Assert.Null(lectura.NodoId);
            Assert.Null(lectura.Nodo);
            Assert.Equal(panel.Id, lectura.PanelId);
            Assert.Equal(SolarUY.Domain.Enums.EstadoSaludLectura.Sano, lectura.EstadoSalud);
            Assert.Equal(100m, lectura.PotenciaGeneradaW);
            Assert.Equal(fecha, lectura.FechaHora);
            Assert.Equal(DateTimeKind.Utc, lectura.FechaHora.Kind);
            // Mover el panel no permite reconstruir retrospectivamente la procedencia.
            var otroNodo = new Nodo(new ParqueSolar("Otro parque", "Rocha"), "Otro nodo", "Sur");
            context.Nodos.Add(otroNodo);
            await context.SaveChangesAsync();
            await context.Paneles.Where(p => p.Id == panel.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.NodoId, otroNodo.Id));
            context.ChangeTracker.Clear();
            Assert.Null((await context.LecturasPanel.SingleAsync()).NodoId);
            var panelActual = await context.Paneles.Include(p => p.Nodo).SingleAsync();
            context.LecturasPanel.Add(new LecturaPanel(panelActual, fecha.AddSeconds(1), 50, SolarUY.Domain.Enums.EstadoSaludLectura.Sano));
            await context.SaveChangesAsync();
            Assert.Equal(otroNodo.Id, (await context.LecturasPanel.SingleAsync(l => l.Id != id)).NodoId);
            Assert.False(context.Database.HasPendingModelChanges());

            using var indices = context.Database.GetDbConnection().CreateCommand();
            indices.CommandText = "SELECT COUNT(DISTINCT index_name) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'LecturasPanel' AND index_name IN ('IX_LecturasPanel_PanelId_FechaHora', 'IX_LecturasPanel_NodoId_FechaHora')";
            Assert.Equal(2, Convert.ToInt32(await indices.ExecuteScalarAsync()));
            foreach (var clave in new[] { "PanelId", "NodoId" })
            {
                using var consulta = context.Database.GetDbConnection().CreateCommand();
                consulta.CommandText = $"EXPLAIN SELECT FechaHora FROM LecturasPanel FORCE INDEX (IX_LecturasPanel_{clave}_FechaHora) WHERE {clave} = @id AND FechaHora >= '2026-10-28' AND FechaHora < '2026-10-29'";
                var parametro = consulta.CreateParameter();
                parametro.ParameterName = "@id";
                parametro.Value = (clave == "PanelId" ? panel.Id : panel.NodoId).ToString();
                consulta.Parameters.Add(parametro);
                await using var plan = await consulta.ExecuteReaderAsync();
                Assert.True(await plan.ReadAsync());
                Assert.Equal($"IX_LecturasPanel_{clave}_FechaHora", plan.GetString(plan.GetOrdinal("key")));
            }
            var retroceso = await Assert.ThrowsAsync<MySqlException>(() => migrador.MigrateAsync("20261029120000_AddDistribucionMonitoreo"));
            Assert.Contains("sin nodo historico verificado", retroceso.Message);
            Assert.Equal(2, await context.LecturasPanel.CountAsync());
            Assert.Null((await context.LecturasPanel.SingleAsync(l => l.Id == id)).NodoId);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
            // Solo la base temporal creada por esta prueba; nunca la base configurada por el usuario.
            await context.Database.EnsureDeletedAsync();
        }
    }
}
