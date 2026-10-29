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
    [MariaDbHistoricoTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migracion_ProtegeDatosPreviosYExigeTopologiaVerificada(bool duplicados)
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

            var error = await Assert.ThrowsAsync<MySqlException>(() => migrador.MigrateAsync());
            Assert.Contains(duplicados ? "duplicadas" : "nodo original", error.Message);
            await context.Database.OpenConnectionAsync();
            using var columna = context.Database.GetDbConnection().CreateCommand();
            columna.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'LecturasPanel' AND column_name = 'NodoId'";
            Assert.Equal(0, Convert.ToInt32(await columna.ExecuteScalarAsync()));
            using var cantidad = context.Database.GetDbConnection().CreateCommand();
            cantidad.CommandText = "SELECT COUNT(*) FROM LecturasPanel";
            Assert.Equal(duplicados ? 2 : 1, Convert.ToInt32(await cantidad.ExecuteScalarAsync()));
            if (duplicados) return;

            // Esta prueba creó la topología y puede demostrar que nunca cambió.
            await context.Database.ExecuteSqlRawAsync("SET @SolarUY_TopologiaHistoricaVerificada = 1");
            await migrador.MigrateAsync();
            var lectura = await context.LecturasPanel.SingleAsync();
            Assert.Equal(id, lectura.Id);
            Assert.Equal(panel.NodoId, lectura.NodoId);
            Assert.Equal(100m, lectura.PotenciaGeneradaW);
            Assert.Equal(fecha, lectura.FechaHora);
            Assert.Equal(DateTimeKind.Utc, lectura.FechaHora.Kind);

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
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
            // Solo la base temporal creada por esta prueba; nunca la base configurada por el usuario.
            await context.Database.EnsureDeletedAsync();
        }
    }
}
