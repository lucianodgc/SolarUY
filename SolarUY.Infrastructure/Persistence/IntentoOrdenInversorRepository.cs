using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;
using SolarUY.Application.Inversores;
using SolarUY.Domain.Entities;
using SolarUY.Infrastructure.Inversores.Tcp;

namespace SolarUY.Infrastructure.Persistence;

public sealed class IntentoOrdenInversorRepository(IDbContextFactory<ApplicationDbContext> factory,
    IOptions<InversorTcpOptions> opciones) : IIntentoOrdenInversorRepository
{
    public async Task<IAsyncDisposable?> IntentarBloqueoAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var conexion = new MySqlConnection(db.Database.GetConnectionString());
        try
        {
            await conexion.OpenAsync(cancellationToken);
            var endpoint = $"{opciones.Value.Host.ToLowerInvariant()}:{opciones.Value.Port}";
            var nombre = "solaruy-orden-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)))[..40];
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT GET_LOCK(@nombre, 0)";
            comando.Parameters.AddWithValue("@nombre", nombre);
            if (Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken)) == 1)
                return new Bloqueo(conexion, nombre);
            await conexion.DisposeAsync(); return null;
        }
        catch { await conexion.DisposeAsync(); throw; }
    }

    public async Task<IntentoOrdenInversor?> BuscarAsync(string actorSub, Guid correlacion, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Set<IntentoOrdenInversor>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorSub == actorSub && x.Correlacion == correlacion, cancellationToken);
    }

    public async Task GuardarAsync(IntentoOrdenInversor intento, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existe = await db.Set<IntentoOrdenInversor>().AsNoTracking().AnyAsync(x => x.Id == intento.Id, cancellationToken);
        if (!existe) db.Add(intento);
        else
        {
            var actual = await db.Set<IntentoOrdenInversor>().SingleAsync(x => x.Id == intento.Id, cancellationToken);
            if (actual.Estado != EstadoOrdenInversor.Pendiente)
            {
                if (actual.Estado == intento.Estado && actual.Codigo == intento.Codigo) return;
                throw new InvalidOperationException("El intento ya tiene un resultado terminal.");
            }
            db.Entry(actual).CurrentValues.SetValues(intento);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecuperarPendientesAsync(DateTime ahoraUtc, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var pendientes = await db.Set<IntentoOrdenInversor>().Where(x => x.Estado == EstadoOrdenInversor.Pendiente).ToListAsync(cancellationToken);
        foreach (var intento in pendientes)
            intento.Finalizar(EstadoOrdenInversor.Indeterminada, "recuperada-sin-reenvio", ahoraUtc < intento.SolicitadaUtc ? intento.SolicitadaUtc : ahoraUtc);
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed class Bloqueo(MySqlConnection conexion, string nombre) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var comando = conexion.CreateCommand();
                comando.CommandText = "SELECT RELEASE_LOCK(@nombre)"; comando.Parameters.AddWithValue("@nombre", nombre);
                using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await comando.ExecuteScalarAsync(limite.Token);
            }
            catch { /* Al cerrar la conexión, MariaDB libera el bloqueo de la sesión. */ }
            finally { await conexion.DisposeAsync(); }
        }
    }
}
