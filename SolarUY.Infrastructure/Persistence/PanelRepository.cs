using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Mantenimiento;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class PanelRepository : IPanelRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public PanelRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        => _contextFactory = contextFactory;

    public async Task<Panel?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Paneles.AsNoTracking().Include(panel => panel.Nodo).ThenInclude(nodo => nodo.Parque)
            .SingleOrDefaultAsync(panel => panel.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PanelDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Paneles.AsNoTracking().OrderBy(panel => panel.NumeroSerie)
            .Select(panel => new PanelDto(panel.Id, panel.NumeroSerie, panel.Nodo.Nombre, panel.Nodo.Parque.Nombre))
            .ToListAsync(cancellationToken);
    }
}
