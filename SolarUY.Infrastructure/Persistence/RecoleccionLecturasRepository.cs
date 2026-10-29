using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Application.Exceptions;
using SolarUY.Domain.Entities;
using SolarUY.Application.Monitoreo;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;

namespace SolarUY.Infrastructure.Persistence;

public sealed class RecoleccionLecturasRepository : IRecoleccionLecturasRepository
{
    private const string NombreBloqueoSql = "CONCAT('solaruy:lecturas:', LEFT(SHA2(DATABASE(),256),40))";
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public RecoleccionLecturasRepository(IDbContextFactory<ApplicationDbContext> contextFactory) => _contextFactory = contextFactory;

    public async Task<ISesionRecoleccionLecturas?> IntentarAbrirSesionAsync(CancellationToken cancellationToken = default)
    {
        var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            // Una conexión física por sesión: cerrarla libera el bloqueo aun si falla RELEASE_LOCK.
            var connection = context.Database.GetDbConnection();
            connection.ConnectionString = new MySqlConnectionStringBuilder(connection.ConnectionString) { Pooling = false }.ConnectionString;
            await context.Database.OpenConnectionAsync(cancellationToken);
            await using var command = CrearComando(context, $"SELECT GET_LOCK({NombreBloqueoSql}, 0)");
            var resultado = await command.ExecuteScalarAsync(cancellationToken);
            if (resultado is null or DBNull) throw new InvalidOperationException("MariaDB no pudo adquirir la exclusión del recolector.");
            if (Convert.ToInt32(resultado) == 1) return new SesionRecoleccionRepository(context);
            await context.DisposeAsync();
            return null;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    private static DbCommand CrearComando(ApplicationDbContext context, string sql)
    {
        var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 5;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        return command;
    }

    private sealed class SesionRecoleccionRepository(ApplicationDbContext context) : ISesionRecoleccionLecturas
    {
        private bool _disposed;

        public async Task<IReadOnlyList<Panel>> ListarPendientesAsync(DateTime instanteUtc, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (instanteUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Se requiere fecha UTC.", nameof(instanteUtc));
            await VerificarPropiedadAsync(cancellationToken);
            return await context.Paneles.AsNoTrackingWithIdentityResolution().Include(panel => panel.Nodo)
                .Where(panel => panel.FechaInstalacion <= instanteUtc && !context.LecturasPanel.Any(lectura =>
                    lectura.PanelId == panel.Id && lectura.FechaHora == instanteUtc))
                .OrderBy(panel => panel.Id).ToListAsync(cancellationToken);
        }

        public async Task GuardarAsync(IReadOnlyList<LecturaPanel> lecturas, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await VerificarPropiedadAsync(cancellationToken);
            if (lecturas.Count == 0) return;
            await using var transaccion = await context.Database.BeginTransactionAsync(cancellationToken);
            var filas = await context.Database.ExecuteSqlRawAsync("UPDATE SecuenciaMonitoreo SET Valor = Valor + 1 WHERE Id = 1", cancellationToken);
            if (filas != 1) throw new InvalidOperationException("Falta la fila coordinadora de monitoreo; no se guardará el lote.");
            await using var secuencia = CrearComando(context, "SELECT Valor FROM SecuenciaMonitoreo WHERE Id = 1");
            var numero = Convert.ToInt64(await secuencia.ExecuteScalarAsync(cancellationToken));
            foreach (var lectura in lecturas)
            {
                context.Attach(lectura.Nodo);
                context.Attach(lectura.Panel);
            }
            context.LecturasPanel.AddRange(lecturas);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                var contenido = JsonSerializer.Serialize(lecturas.Select(l => new LecturaConfirmadaDto(l.Panel.Nodo.ParqueId, l.PanelId, l.FechaHora)).ToArray());
                await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO AvisosMonitoreo (Secuencia, Contenido) VALUES ({numero}, {contenido})", cancellationToken);
                await transaccion.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
            {
                throw new LecturaDuplicadaException(exception);
            }
        }

        private async Task VerificarPropiedadAsync(CancellationToken cancellationToken)
        {
            if (context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                throw new InvalidOperationException("La conexión de la sesión exclusiva está cerrada.");
            await using var command = CrearComando(context, $"SELECT IS_USED_LOCK({NombreBloqueoSql}) = CONNECTION_ID()");
            var propietario = await command.ExecuteScalarAsync(cancellationToken);
            if (propietario is null or DBNull || Convert.ToInt32(propietario) != 1)
                throw new InvalidOperationException("Se perdió la exclusión del recolector antes de guardar.");
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (context.Database.GetDbConnection().State != System.Data.ConnectionState.Open) return;
                await using var command = CrearComando(context, $"SELECT RELEASE_LOCK({NombreBloqueoSql})");
                await command.ExecuteScalarAsync(CancellationToken.None);
            }
            // Cerrar la conexión física libera el bloqueo; no ocultar el fallo original del ciclo.
            catch (DbException) { }
            finally { await context.DisposeAsync(); }
        }
    }
}
