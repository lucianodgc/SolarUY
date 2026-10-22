using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Domain.Entities;

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
        return command;
    }

    private sealed class SesionRecoleccionRepository(ApplicationDbContext context) : ISesionRecoleccionLecturas
    {
        private bool _disposed;

        public async Task<IReadOnlyList<Panel>> ListarPendientesAsync(DateTime instanteUtc, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (instanteUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Se requiere fecha UTC.", nameof(instanteUtc));
            var inicioMinuto = new DateTime(instanteUtc.Ticks - instanteUtc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
            var finMinuto = inicioMinuto.AddMinutes(1);
            return await context.Paneles.AsNoTracking()
                .Where(panel => panel.FechaInstalacion <= instanteUtc && !context.LecturasPanel.Any(lectura =>
                    lectura.PanelId == panel.Id && lectura.FechaHora >= inicioMinuto && lectura.FechaHora < finMinuto))
                .OrderBy(panel => panel.Id).ToListAsync(cancellationToken);
        }

        public async Task GuardarAsync(IReadOnlyList<LecturaPanel> lecturas, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await using var command = CrearComando(context, $"SELECT IS_USED_LOCK({NombreBloqueoSql}) = CONNECTION_ID()");
            var propietario = await command.ExecuteScalarAsync(cancellationToken);
            if (propietario is null or DBNull || Convert.ToInt32(propietario) != 1)
                throw new InvalidOperationException("Se perdió la exclusión del recolector antes de guardar.");
            foreach (var lectura in lecturas) context.Attach(lectura.Panel);
            context.LecturasPanel.AddRange(lecturas);
            await context.SaveChangesAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                await using var command = CrearComando(context, $"SELECT RELEASE_LOCK({NombreBloqueoSql})");
                await command.ExecuteScalarAsync(CancellationToken.None);
            }
            finally { await context.DisposeAsync(); }
        }
    }
}
