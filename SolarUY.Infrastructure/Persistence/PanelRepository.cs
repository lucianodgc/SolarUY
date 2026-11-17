using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Mantenimiento;
using PanelesApplication = SolarUY.Application.Paneles;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class PanelRepository : IPanelRepository, PanelesApplication.IPanelRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public PanelRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        => _contextFactory = contextFactory;

    public async Task<Panel?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Paneles.AsNoTracking().Include(panel => panel.Nodo).ThenInclude(nodo => nodo.Parque)
            .SingleOrDefaultAsync(panel => panel.Id == id && !panel.EstaDadoDeBaja, cancellationToken);
    }

    public async Task<IReadOnlyList<PanelDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Paneles.AsNoTracking().Where(panel => !panel.EstaDadoDeBaja).OrderBy(panel => panel.NumeroSerie)
            .Select(panel => new PanelDto(panel.Id, panel.NumeroSerie, panel.Nodo.Nombre, panel.Nodo.Parque.Nombre))
            .ToListAsync(cancellationToken);
    }

    public async Task<Nodo?> ObtenerNodoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Nodos.AsNoTracking().Include(nodo => nodo.Parque)
            .SingleOrDefaultAsync(nodo => nodo.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PanelesApplication.PanelDto>> ListarDetallesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await ProyectarAsync(context.Paneles.AsNoTracking().Where(panel => !panel.EstaDadoDeBaja), cancellationToken);
    }

    public async Task<IReadOnlyList<PanelesApplication.PanelDto>> ListarDetallesPorNodoAsync(Guid nodoId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await ProyectarAsync(context.Paneles.AsNoTracking()
            .Where(panel => panel.NodoId == nodoId && !panel.EstaDadoDeBaja), cancellationToken);
    }

    public async Task<bool> ExisteNumeroSerieAsync(string numeroSerie, Guid? panelIdExcluido = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Paneles.AnyAsync(panel => panel.NumeroSerie == numeroSerie
            && (!panelIdExcluido.HasValue || panel.Id != panelIdExcluido.Value), cancellationToken);
    }

    public async Task AgregarAsync(Panel panel, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var nodo = await BloquearNodoAsync(context, panel.NodoId, cancellationToken);
        nodo.ValidarCapacidadPaneles(await ContarPanelesActivosAsync(context, nodo.Id, cancellationToken) + 1);
        context.Attach(panel.Nodo);
        context.Paneles.Add(panel);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ActualizarAsync(Panel panel, Guid nodoOriginalId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (nodoOriginalId != panel.NodoId)
        {
            var nodos = new[] { nodoOriginalId, panel.NodoId }.OrderBy(id => id).ToArray();
            var primero = await BloquearNodoAsync(context, nodos[0], cancellationToken);
            var segundo = await BloquearNodoAsync(context, nodos[1], cancellationToken);
            var destino = primero.Id == panel.NodoId ? primero : segundo;
            destino.ValidarCapacidadPaneles(await ContarPanelesActivosAsync(context, destino.Id, cancellationToken) + 1);
        }
        context.Attach(panel.Nodo);
        context.Attach(panel);
        context.Entry(panel).State = EntityState.Modified;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DarDeBajaAsync(Panel panel, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Attach(panel);
        context.Entry(panel).Property(actual => actual.EstaDadoDeBaja).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static Task<int> ContarPanelesActivosAsync(ApplicationDbContext context, Guid nodoId,
        CancellationToken cancellationToken)
        => context.Paneles.CountAsync(panel => panel.NodoId == nodoId && !panel.EstaDadoDeBaja, cancellationToken);

    private static async Task<Nodo> BloquearNodoAsync(ApplicationDbContext context, Guid nodoId,
        CancellationToken cancellationToken)
    {
        var nodo = await context.Nodos.FromSqlInterpolated($"SELECT * FROM Nodos WHERE Id = {nodoId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        return nodo ?? throw new KeyNotFoundException("El nodo seleccionado no existe.");
    }

    private static Task<List<PanelesApplication.PanelDto>> ProyectarAsync(IQueryable<Panel> consulta,
        CancellationToken cancellationToken)
        => consulta.OrderBy(panel => panel.NumeroSerie)
            .Select(panel => new PanelesApplication.PanelDto(panel.Id, panel.NodoId, panel.Nodo.Nombre,
                panel.Nodo.Parque.Nombre, panel.NumeroSerie, panel.Marca, panel.Modelo, panel.PotenciaNominalW,
                panel.FechaInstalacion, panel.Estado)).ToListAsync(cancellationToken);
}
