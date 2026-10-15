using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Exceptions;
using SolarUY.Application.Mantenimiento;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;

namespace SolarUY.Infrastructure.Persistence;

public sealed class TareaMantenimientoRepository : ITareaMantenimientoRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public TareaMantenimientoRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        => _contextFactory = contextFactory;

    public async Task AgregarAsync(TareaMantenimiento tarea, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Attach(tarea.Panel);
        context.TareasMantenimiento.Add(tarea);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        { throw new MantenimientoException("No se pudo guardar la tarea. Comprueba que el panel siga disponible."); }
    }

    public async Task<TareaMantenimiento?> ObtenerAsync(Guid id, string? responsableAutorizado,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await ConAlcance(context, responsableAutorizado).SingleOrDefaultAsync(tarea => tarea.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TareaMantenimiento>> ListarAsync(FiltroTareasDto filtro,
        string? responsableAutorizado, DateTime referenciaUtc, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = ConAlcance(context, responsableAutorizado);
        if (filtro.Estado is { } estado) query = query.Where(tarea => tarea.Estado == estado);
        if (filtro.PanelId is { } panelId) query = query.Where(tarea => tarea.PanelId == panelId);
        if (!string.IsNullOrWhiteSpace(filtro.ResponsableId))
            query = query.Where(tarea => EF.Functions.Collate(tarea.ResponsableId, "utf8mb4_bin") == filtro.ResponsableId);
        if (filtro.SoloVencidas)
            query = query.Where(tarea => tarea.FechaProgramada < referenciaUtc
                && (tarea.Estado == EstadoTareaMantenimiento.Pendiente || tarea.Estado == EstadoTareaMantenimiento.EnCurso));
        return await query.OrderBy(tarea => tarea.FechaProgramada).ThenBy(tarea => tarea.Id).ToListAsync(cancellationToken);
    }

    public async Task<bool> GuardarAsync(TareaMantenimiento tarea, TareaMantenimientoOriginal original,
        string? responsableAutorizado, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.TareasMantenimiento.Where(actual => actual.Id == tarea.Id
            && (actual.Estado == EstadoTareaMantenimiento.Pendiente || actual.Estado == EstadoTareaMantenimiento.EnCurso)
            && actual.Estado == original.Estado
            && EF.Functions.Collate(actual.ResponsableId, "utf8mb4_bin") == original.ResponsableId
            && EF.Functions.Collate(actual.Tipo, "utf8mb4_bin") == original.Tipo
            && EF.Functions.Collate(actual.Descripcion, "utf8mb4_bin") == original.Descripcion
            && actual.FechaProgramada == original.FechaProgramada && actual.FechaRealizacion == original.FechaRealizacion);
        if (responsableAutorizado is not null)
            query = query.Where(actual => EF.Functions.Collate(actual.ResponsableId, "utf8mb4_bin") == responsableAutorizado);
        var filas = await query.ExecuteUpdateAsync(setters => setters
            .SetProperty(actual => actual.Tipo, tarea.Tipo)
            .SetProperty(actual => actual.Descripcion, tarea.Descripcion)
            .SetProperty(actual => actual.FechaProgramada, tarea.FechaProgramada)
            .SetProperty(actual => actual.ResponsableId, tarea.ResponsableId)
            .SetProperty(actual => actual.Estado, tarea.Estado)
            .SetProperty(actual => actual.FechaRealizacion, tarea.FechaRealizacion), cancellationToken);
        return filas == 1;
    }

    private static IQueryable<TareaMantenimiento> ConAlcance(ApplicationDbContext context, string? responsableAutorizado)
    {
        var query = context.TareasMantenimiento.AsNoTracking().Include(tarea => tarea.Panel)
            .ThenInclude(panel => panel.Nodo).ThenInclude(nodo => nodo.Parque).AsQueryable();
        return responsableAutorizado is null ? query
            : query.Where(tarea => EF.Functions.Collate(tarea.ResponsableId, "utf8mb4_bin") == responsableAutorizado);
    }
}
