using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Nodos;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class NodoRepository : INodoRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public NodoRepository(IDbContextFactory<ApplicationDbContext> contextFactory) => _contextFactory = contextFactory;

    public async Task<ParqueSolar?> ObtenerParqueAsync(Guid parqueId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ParquesSolares.FirstOrDefaultAsync(parque => parque.Id == parqueId, cancellationToken);
    }

    public async Task<Nodo?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Nodos.AsNoTracking().Include(nodo => nodo.Inversor)
            .FirstOrDefaultAsync(nodo => nodo.Id == id, cancellationToken);
    }

    public async Task<Nodo?> ObtenerParaEliminarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Nodos.Include(nodo => nodo.Parque).ThenInclude(parque => parque.Nodos)
            .FirstOrDefaultAsync(nodo => nodo.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Nodo>> ListarPorParqueAsync(Guid parqueId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Nodos.AsNoTracking().Include(nodo => nodo.Inversor)
            .Where(nodo => nodo.ParqueId == parqueId).OrderBy(nodo => nodo.Nombre).ToListAsync(cancellationToken);
    }

    public async Task AgregarAsync(Nodo nodo, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Attach(nodo.Parque);
        context.Nodos.Add(nodo);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ActualizarAsync(Nodo nodo, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Nodos.Update(nodo);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task EliminarEnCascadaAsync(Nodo nodo, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM TareasMantenimiento WHERE PanelId IN (SELECT Id FROM Paneles WHERE NodoId = {nodo.Id})", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM LecturasPanel WHERE PanelId IN (SELECT Id FROM Paneles WHERE NodoId = {nodo.Id})", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Paneles WHERE NodoId = {nodo.Id}", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Inversores WHERE NodoId = {nodo.Id}", cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Nodos WHERE Id = {nodo.Id}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
