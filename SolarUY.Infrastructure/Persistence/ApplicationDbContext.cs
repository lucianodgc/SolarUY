using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<ParqueSolar> ParquesSolares { get; set; } = null!;
    public DbSet<Nodo> Nodos { get; set; } = null!;
    public DbSet<Panel> Paneles { get; set; } = null!;
    public DbSet<LecturaPanel> LecturasPanel { get; set; } = null!;
    public DbSet<TareaMantenimiento> TareasMantenimiento { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

    public Task<bool> ExisteNombreParqueSolarAsync(
        string nombre,
        Guid? parqueIdExcluido = null,
        CancellationToken cancellationToken = default)
        => ParquesSolares.AnyAsync(
            parque => parque.Nombre == nombre
                && (!parqueIdExcluido.HasValue || parque.Id != parqueIdExcluido.Value),
            cancellationToken);

    public Task<ParqueSolar?> ObtenerParqueSolarPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ParquesSolares.FirstOrDefaultAsync(parque => parque.Id == id, cancellationToken);

    public void AgregarParqueSolar(ParqueSolar parque) => ParquesSolares.Add(parque);

    public void ActualizarParqueSolar(ParqueSolar parque) => ParquesSolares.Update(parque);

    public async Task EliminarParqueSolarEnCascadaAsync(
        ParqueSolar parque,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        await EliminarDependenciasAsync(parque.Id, cancellationToken);
        ParquesSolares.Remove(parque);
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParqueSolar>> ListarParquesSolaresAsync(CancellationToken cancellationToken = default)
        => await ParquesSolares.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ParqueSolar>> BuscarParquesSolaresAsync(
        string? criterio,
        CancellationToken cancellationToken = default)
    {
        var consulta = ParquesSolares.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(criterio))
        {
            var texto = criterio.Trim();
            consulta = consulta.Where(parque => parque.Nombre.Contains(texto) || parque.Ubicacion.Contains(texto));
        }

        return await consulta.OrderBy(parque => parque.Nombre).ToListAsync(cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await base.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 }
            && exception.Entries.All(entry => entry.Entity is ParqueSolar))
        {
            foreach (var entry in exception.Entries) entry.State = EntityState.Detached;
            throw new NombreParqueDuplicadoException(exception);
        }
    }

    private async Task EliminarDependenciasAsync(Guid parqueId, CancellationToken cancellationToken)
    {
        if (await ExisteTablaAsync("TareasMantenimiento", cancellationToken))
        {
            await Database.ExecuteSqlInterpolatedAsync($"""
                DELETE tareas
                FROM TareasMantenimiento AS tareas
                INNER JOIN Paneles AS panel ON panel.Id = tareas.PanelId
                INNER JOIN Nodos AS nodo ON nodo.Id = panel.NodoId
                WHERE nodo.ParqueId = {parqueId}
                """, cancellationToken);
        }

        if (await ExisteTablaAsync("LecturasPanel", cancellationToken))
        {
            await Database.ExecuteSqlInterpolatedAsync($"""
                DELETE lectura
                FROM LecturasPanel AS lectura
                INNER JOIN Paneles AS panel ON panel.Id = lectura.PanelId
                INNER JOIN Nodos AS nodo ON nodo.Id = panel.NodoId
                WHERE nodo.ParqueId = {parqueId}
                """, cancellationToken);
        }

        if (await ExisteTablaAsync("Paneles", cancellationToken))
        {
            await Database.ExecuteSqlInterpolatedAsync($"""
                DELETE panel
                FROM Paneles AS panel
                INNER JOIN Nodos AS nodo ON nodo.Id = panel.NodoId
                WHERE nodo.ParqueId = {parqueId}
                """, cancellationToken);
        }

        if (await ExisteTablaAsync("Nodos", cancellationToken))
        {
            await Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Nodos WHERE ParqueId = {parqueId}", cancellationToken);
        }
    }

    private async Task<bool> ExisteTablaAsync(string nombreTabla, CancellationToken cancellationToken)
    {
        var connection = Database.GetDbConnection();
        var debeCerrarConexion = connection.State != ConnectionState.Open;
        if (debeCerrarConexion) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @nombreTabla
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@nombreTabla";
            parameter.Value = nombreTabla;
            command.Parameters.Add(parameter);

            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        }
        finally
        {
            if (debeCerrarConexion) await connection.CloseAsync();
        }
    }
}
