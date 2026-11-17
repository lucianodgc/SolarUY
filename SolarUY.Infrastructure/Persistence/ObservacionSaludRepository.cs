using Microsoft.EntityFrameworkCore;
using System.Data;
using SolarUY.Application.Salud;
using SolarUY.Domain.Salud;

namespace SolarUY.Infrastructure.Persistence;

public sealed class ObservacionSaludRepository(IDbContextFactory<ApplicationDbContext> factory) : IObservacionSaludRepository
{
    public async Task<SnapshotSaludDto> LeerSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        // Incluir parques vacíos permite reconciliar un traslado perdido al reiniciar el observador.
        var parques = await db.ParquesSolares.AsNoTracking().Select(p => p.Id).ToArrayAsync(cancellationToken);
        var paneles = await (from panel in db.Paneles.AsNoTracking()
                      join estado in db.Set<SaludPanel>().AsNoTracking() on panel.Id equals estado.PanelId into estados
                      from estado in estados.DefaultIfEmpty()
                      orderby panel.Id
                      select new FirmaSaludDto(panel.Id, panel.NodoId, panel.Nodo.ParqueId,
                          estado == null ? 0 : estado.Version,
                          estado == null ? null : estado.EvaluadoUtc,
                          estado == null ? null : estado.NodoEvaluadoId,
                          estado != null && estado.Suficiente)).ToArrayAsync(cancellationToken);
        await snapshot.CommitAsync(cancellationToken);
        return new(parques, paneles);
    }
}
