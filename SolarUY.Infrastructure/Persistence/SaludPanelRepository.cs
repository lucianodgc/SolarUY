using System.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SolarUY.Application.Salud;
using SolarUY.Domain.Salud;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class SaludPanelRepository(IDbContextFactory<ApplicationDbContext> factory,
    EvaluacionSaludService evaluador) : ISaludPanelRepository
{
    public async Task<IReadOnlyList<Guid>> ListarPanelesAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Paneles.OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvisoSaludConfirmada>> EvaluarLoteAsync(IReadOnlyList<Guid> paneles,
        DateTime referenciaUtc, PoliticaSalud politica, CancellationToken cancellationToken)
    {
        if (paneles.Count == 0) return [];
        if (paneles.Count > 100 || referenciaUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Lote o referencia inválidos.");
        for (var intento = 1; intento <= 2; intento++)
        {
            try { return await ConfirmarLoteAsync(paneles, referenciaUtc, politica, cancellationToken); }
            catch (Exception ex) when (EsBloqueoTransitorio(ex))
            { await Task.Delay(50 * intento, cancellationToken); }
        }
        return await ConfirmarLoteAsync(paneles, referenciaUtc, politica, cancellationToken);
    }

    private static bool EsBloqueoTransitorio(Exception exception)
        => exception is MySqlException { Number: 1213 or 1205 }
            or DbUpdateException { InnerException: MySqlException { Number: 1213 or 1205 } };

    private async Task<IReadOnlyList<AvisoSaludConfirmada>> ConfirmarLoteAsync(IReadOnlyList<Guid> ids,
        DateTime referencia, PoliticaSalud politica, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // La fila del panel existe antes que la primera evaluación y serializa también su creación.
        var parametros = ids.Distinct().Order().Select((id, i) => new MySqlParameter("@p" + i, id)).ToArray();
        var sql = "SELECT * FROM Paneles WHERE Id IN (" + string.Join(",", parametros.Select(p => p.ParameterName)) + ") ORDER BY Id FOR UPDATE";
        var paneles = await db.Paneles.FromSqlRaw(sql, parametros).ToArrayAsync(ct);
        var presentes = paneles.Select(p => p.Id).ToArray();
        var nodosIds = paneles.Select(p => p.NodoId).Distinct().ToArray();
        var nodos = await db.Nodos.AsNoTracking().Where(n => nodosIds.Contains(n.Id)).ToDictionaryAsync(n => n.Id, ct);
        var estados = await db.Set<SaludPanel>().Where(s => presentes.Contains(s.PanelId)).ToDictionaryAsync(s => s.PanelId, ct);
        var activas = await db.Set<AlertaPanel>().Where(a => presentes.Contains(a.PanelId) && a.CierreUtc == null).ToDictionaryAsync(a => a.PanelId, ct);
        var lecturas = await db.LecturasPanel.AsNoTracking().Where(l => presentes.Contains(l.PanelId)
            && !db.LecturasPanel.Any(otra => otra.PanelId == l.PanelId && otra.FechaHora > l.FechaHora))
            .ToDictionaryAsync(l => l.PanelId, ct);
        var avisos = new List<AvisoSaludConfirmada>();
        var nuevasAlertas = new List<AlertaPanel>();
        foreach (var panel in paneles.Where(p => p.FechaInstalacion <= referencia))
        {
            if (!estados.TryGetValue(panel.Id, out var salud))
            { salud = new SaludPanel(panel, referencia); db.Add(salud); }
            if (referencia < salud.EvaluadoUtc) continue;
            var cambio = EvaluarPanel(panel, salud, lecturas.GetValueOrDefault(panel.Id), referencia, politica);
            avisos.Add(new(nodos[panel.NodoId].ParqueId, panel.Id, salud.Version));
            if (cambio is null) continue;
            db.Add(cambio);
            if (activas.TryGetValue(panel.Id, out var activa)) activa.Cerrar(referencia);
            if (cambio.EstadoNuevo != EstadoSaludPanel.Normal)
                nuevasAlertas.Add(new AlertaPanel(panel, cambio.EstadoNuevo, referencia));
        }
        // Liberar la clave activa antes de insertar el episodio siguiente, dentro de la misma transacción.
        await db.SaveChangesAsync(ct);
        db.AddRange(nuevasAlertas);
        await db.SaveChangesAsync(ct); await transaccion.CommitAsync(ct);
        return avisos;
    }

    private CambioEstadoSaludPanel? EvaluarPanel(Panel panel, SaludPanel salud, LecturaPanel? lectura,
        DateTime referencia, PoliticaSalud politica)
    {
        var inicio = salud.NodoEvaluadoId == panel.NodoId ? salud.PrimeraObservacionUtc : referencia;
        var evidencia = lectura is null ? null : new EvidenciaSalud(lectura.FechaHora, lectura.PotenciaGeneradaW,
            lectura.EstadoSalud, lectura.NodoId == panel.NodoId && lectura.FechaHora >= panel.FechaInstalacion);
        var resultado = evaluador.Evaluar(evidencia, panel.PotenciaNominalW, inicio, referencia, politica);
        // El futuro se conserva como motivo, nunca como fecha confirmada de evidencia.
        if (resultado.EvidenciaUtc > referencia) resultado = resultado with { EvidenciaUtc = null };
        return salud.Aplicar(resultado, referencia, panel.NodoId);
    }

    public async Task<DatosConsultaSaludDto?> ConsultarAsync(Guid parqueId, DateTime referenciaUtc,
        TimeSpan vigenciaEvaluacion, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var parque = await db.ParquesSolares.AsNoTracking().SingleOrDefaultAsync(p => p.Id == parqueId, cancellationToken);
        if (parque is null) return null;
        var nodos = await db.Nodos.AsNoTracking().Where(n => n.ParqueId == parqueId).OrderBy(n => n.Nombre).ToArrayAsync(cancellationToken);
        var paneles = await db.Paneles.AsNoTracking().Where(p => p.Nodo.ParqueId == parqueId).OrderBy(p => p.NumeroSerie).ToArrayAsync(cancellationToken);
        var estados = await db.Set<SaludPanel>().AsNoTracking().Where(s => s.Panel.Nodo.ParqueId == parqueId).ToDictionaryAsync(s => s.PanelId, cancellationToken);
        var activas = await db.Set<AlertaPanel>().AsNoTracking().Where(a => a.Panel.Nodo.ParqueId == parqueId && a.CierreUtc == null)
            .OrderByDescending(a => a.DeteccionUtc).ToArrayAsync(cancellationToken);
        var porNodo = nodos.ToDictionary(n => n.Id);
        var dtos = paneles.Select(p =>
        {
            estados.TryGetValue(p.Id, out var s);
            var vigente = s is { Suficiente: true, Estado: not null } && s.NodoEvaluadoId == p.NodoId
                && s.EvaluadoUtc <= referenciaUtc && referenciaUtc - s.EvaluadoUtc < vigenciaEvaluacion;
            return new SaludPanelDto(p.Id, p.NumeroSerie, p.NodoId, porNodo[p.NodoId].Nombre, s?.Estado, vigente,
                vigente ? s!.Motivo : "Sin evaluación vigente. " + s?.Motivo, s?.EvidenciaUtc, s?.EvaluadoUtc, s?.Version ?? 0);
        }).ToArray();
        var porPanel = dtos.ToDictionary(p => p.PanelId);
        var alertas = activas.Select(a =>
        {
            var p = porPanel[a.PanelId];
            return new AlertaActivaDto(a.Id, a.PanelId, p.NumeroSerie, p.NodoId, p.Nodo, parque.Id, parque.Nombre,
                a.Condicion, a.DeteccionUtc, p.Vigente);
        }).ToArray();
        await transaccion.CommitAsync(cancellationToken);
        return new(dtos, nodos.Select(n => (n.Id, n.Nombre)).ToArray(), alertas);
    }

    public async Task<PaginaCambiosSaludDto> HistorialAsync(Guid parqueId, Guid panelId, int pagina, int tamano, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Paneles.AnyAsync(p => p.Id == panelId && p.Nodo.ParqueId == parqueId, cancellationToken))
            throw new KeyNotFoundException("El panel no pertenece al parque seleccionado.");
        var consulta = db.Set<CambioEstadoSaludPanel>().AsNoTracking().Where(c => c.PanelId == panelId);
        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta.OrderByDescending(c => c.FechaUtc).ThenByDescending(c => c.Version)
            .Skip(pagina * tamano).Take(tamano).Select(c => new CambioEstadoSaludDto(c.EstadoAnterior, c.EstadoNuevo, c.FechaUtc, c.Version)).ToArrayAsync(cancellationToken);
        return new(items, total);
    }
}
