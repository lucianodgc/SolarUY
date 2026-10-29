using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Inversores;

namespace SolarUY.Infrastructure.Persistence;

public sealed class InversorInventarioRepository : IInversorInventarioRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public InversorInventarioRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<InventarioNodoInversorDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var nodos = await context.Nodos.AsNoTracking()
            .Include(nodo => nodo.Parque)
            .OrderBy(nodo => nodo.Parque.Nombre)
            .ThenBy(nodo => nodo.Nombre)
            .ToArrayAsync(cancellationToken);
        var paneles = await context.Paneles.AsNoTracking().OrderBy(panel => panel.NumeroSerie).ToArrayAsync(cancellationToken);
        var panelesPorNodo = paneles.GroupBy(panel => panel.NodoId).ToDictionary(grupo => grupo.Key, grupo => grupo.ToArray());

        return nodos.Select(nodo => new InventarioNodoInversorDto(
            nodo.Id.ToString("D"), nodo.Nombre, nodo.Parque.Nombre,
            panelesPorNodo.GetValueOrDefault(nodo.Id, [])
                .Select(panel => new InventarioPanelInversorDto(
                    panel.Id.ToString("D"), panel.NumeroSerie, panel.PotenciaNominalW, panel.Estado))
                .ToArray()))
            .ToArray();
    }
}
