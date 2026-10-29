using System.Data;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Monitoreo;

namespace SolarUY.Infrastructure.Persistence;

public sealed class MonitoreoRepository : IMonitoreoRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public MonitoreoRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        => _contextFactory = contextFactory;

    public async Task<IReadOnlyList<ParqueMonitoreoDto>> ListarParquesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ParquesSolares.AsNoTracking().OrderBy(parque => parque.Nombre)
            .Select(parque => new ParqueMonitoreoDto(parque.Id, parque.Nombre)).ToListAsync(cancellationToken);
    }

    public async Task<DatosMonitoreoDto?> ObtenerDatosAsync(Guid parqueId, DateTime referenciaUtc,
        CancellationToken cancellationToken = default)
    {
        if (parqueId == Guid.Empty) throw new ArgumentException("Selecciona un parque válido.", nameof(parqueId));
        if (referenciaUtc == default || referenciaUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La referencia debe ser un instante UTC válido.", nameof(referenciaUtc));

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Las cuatro consultas leen la misma instantánea, sin bloquear la adquisición de nuevas muestras.
        await using var snapshot = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var parque = await context.ParquesSolares.AsNoTracking().SingleOrDefaultAsync(p => p.Id == parqueId, cancellationToken);
        if (parque is null) return null;
        var nodos = await context.Nodos.AsNoTracking().Where(n => n.ParqueId == parqueId)
            .OrderBy(n => n.Nombre).ToArrayAsync(cancellationToken);
        var paneles = await context.Paneles.AsNoTracking().Where(p => p.Nodo.ParqueId == parqueId)
            .OrderBy(p => p.NumeroSerie).ToArrayAsync(cancellationToken);
        var lecturas = await context.LecturasPanel.AsNoTracking()
            .Where(lectura => lectura.Panel.Nodo.ParqueId == parqueId && lectura.FechaHora <= referenciaUtc
                && !context.LecturasPanel.Any(otra => otra.PanelId == lectura.PanelId
                    && otra.FechaHora <= referenciaUtc && otra.FechaHora > lectura.FechaHora))
            .ToArrayAsync(cancellationToken);
        await snapshot.CommitAsync(cancellationToken);
        return new DatosMonitoreoDto(parque, nodos, paneles, lecturas);
    }
}
