using Microsoft.EntityFrameworkCore;
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
    public DbSet<TareaMantenimiento> TareasMantenimiento { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

    public Task<bool> ExisteNombreParqueSolarAsync(string nombre, CancellationToken cancellationToken = default)
        => ParquesSolares.AnyAsync(parque => parque.Nombre == nombre, cancellationToken);

    public void AgregarParqueSolar(ParqueSolar parque) => ParquesSolares.Add(parque);

    public async Task<IReadOnlyList<ParqueSolar>> ListarParquesSolaresAsync(CancellationToken cancellationToken = default)
        => await ParquesSolares.AsNoTracking().ToListAsync(cancellationToken);

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
}
