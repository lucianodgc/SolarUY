using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.HistoricoGeneracion;

namespace SolarUY.Infrastructure.Persistence;

public sealed class HistoricoGeneracionRepository : IHistoricoGeneracionRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public HistoricoGeneracionRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        => _contextFactory = contextFactory;

    public async Task<bool> ExisteObjetivoAsync(TipoObjetivoHistorico tipo, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return tipo switch
        {
            TipoObjetivoHistorico.Parque => await context.ParquesSolares.AnyAsync(parque => parque.Id == id, cancellationToken),
            TipoObjetivoHistorico.Nodo => await context.Nodos.AnyAsync(nodo => nodo.Id == id, cancellationToken),
            TipoObjetivoHistorico.Panel => await context.Paneles.AnyAsync(panel => panel.Id == id, cancellationToken),
            _ => throw new ArgumentException("Tipo de objetivo inválido.", nameof(tipo))
        };
    }

    public async Task<IReadOnlyList<OpcionHistoricoDto>> ListarOpcionesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var opciones = new List<OpcionHistoricoDto>();
        opciones.AddRange(await context.ParquesSolares.AsNoTracking().OrderBy(parque => parque.Nombre)
            .Select(parque => new OpcionHistoricoDto(TipoObjetivoHistorico.Parque, parque.Id, parque.Nombre, parque.Id, null))
            .ToListAsync(cancellationToken));
        opciones.AddRange(await context.Nodos.AsNoTracking().OrderBy(nodo => nodo.Parque.Nombre).ThenBy(nodo => nodo.Nombre)
            .Select(nodo => new OpcionHistoricoDto(TipoObjetivoHistorico.Nodo, nodo.Id,
                nodo.Parque.Nombre + " / " + nodo.Nombre, nodo.ParqueId, nodo.Id)).ToListAsync(cancellationToken));
        opciones.AddRange(await context.Paneles.AsNoTracking().OrderBy(panel => panel.NumeroSerie)
            .Select(panel => new OpcionHistoricoDto(TipoObjetivoHistorico.Panel, panel.Id,
                panel.Nodo.Parque.Nombre + " / " + panel.Nodo.Nombre + " / " + panel.NumeroSerie,
                panel.Nodo.ParqueId, panel.NodoId)).ToListAsync(cancellationToken));
        return opciones;
    }

    public async Task<ResultadoHistoricoDto> ConsultarAsync(ConsultaHistoricoDto consulta, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var command = CrearComando(context, consulta);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var serie = new List<PuntoHistoricoDto>();
        decimal? total = null;
        var cobertura = CoberturaHistorico.SinDatos;
        while (await reader.ReadAsync(cancellationToken))
        {
            var intervalo = consulta.Intervalos[reader.GetInt32(0) - 1];
            var energia = reader.IsDBNull(1) ? (decimal?)null : reader.GetDecimal(1);
            serie.Add(new PuntoHistoricoDto(intervalo, energia, (CoberturaHistorico)reader.GetInt32(2),
                reader.GetInt32(3) == 1, reader.GetInt32(4) == 1));
            total = reader.IsDBNull(5) ? null : reader.GetDecimal(5);
            cobertura = (CoberturaHistorico)reader.GetInt32(6);
        }
        await reader.DisposeAsync();
        await using var pendientes = context.Database.GetDbConnection().CreateCommand();
        // En nodo/parque el aviso es global al período: no conocemos su pertenencia.
        pendientes.CommandText = "SELECT COUNT(*) FROM LecturasPanel WHERE NodoId IS NULL "
            + "AND FechaHora >= DATE_SUB(@inicio, INTERVAL 5 MINUTE) AND FechaHora < @fin"
            + (consulta.Tipo == TipoObjetivoHistorico.Panel ? " AND PanelId = @objetivo" : "");
        AgregarParametro(pendientes, "@inicio", consulta.InicioUtc);
        AgregarParametro(pendientes, "@fin", consulta.FinUtc);
        AgregarParametro(pendientes, "@objetivo", consulta.ObjetivoId.ToString());
        return new ResultadoHistoricoDto(consulta, total, cobertura, serie)
        {
            LecturasSinNodoHistorico = Convert.ToInt64(await pendientes.ExecuteScalarAsync(cancellationToken))
        };
    }

    private static DbCommand CrearComando(ApplicationDbContext context, ConsultaHistoricoDto consulta)
    {
        var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = CrearSql(consulta.Tipo);
        AgregarParametro(command, "@objetivo", consulta.ObjetivoId.ToString());
        AgregarParametro(command, "@inicio", consulta.InicioUtc);
        AgregarParametro(command, "@fin", consulta.FinUtc);
        // Clave de día UTC para un equijoin indexable sobre los tramos materializados.
        // Se incluye el día de los cinco minutos previos por las muestras de borde.
        var intervalos = consulta.Intervalos.SelectMany((intervalo, indice) =>
            Enumerable.Range(0, (intervalo.FinUtc.AddTicks(-1).Date - intervalo.InicioUtc.AddMinutes(-5).Date).Days + 1)
                .Select(dia => new
                {
                    Ordinal = indice + 1,
                    DiaUtc = intervalo.InicioUtc.AddMinutes(-5).Date.AddDays(dia).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    InicioUtc = intervalo.InicioUtc.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
                    FinUtc = intervalo.FinUtc.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)
                }));
        AgregarParametro(command, "@intervalos", JsonSerializer.Serialize(intervalos));
        return command;
    }

    private static void AgregarParametro(DbCommand command, string nombre, object valor)
    {
        var parametro = command.CreateParameter();
        parametro.ParameterName = nombre;
        parametro.Value = valor;
        command.Parameters.Add(parametro);
    }

    // Solo se interpolan predicados constantes. Todos los valores externos son parámetros.
    private static string CrearSql(TipoObjetivoHistorico tipo)
    {
        var predicado = tipo switch
        {
            TipoObjetivoHistorico.Parque => "n.ParqueId = @objetivo",
            TipoObjetivoHistorico.Nodo => "p.NodoId = @objetivo",
            TipoObjetivoHistorico.Panel => "p.Id = @objetivo",
            _ => throw new ArgumentException("Tipo de objetivo inválido.", nameof(tipo))
        };
        var procedencia = tipo switch
        {
            TipoObjetivoHistorico.Parque => "l.NodoId IN (SELECT Id FROM Nodos WHERE ParqueId = @objetivo)",
            TipoObjetivoHistorico.Nodo => "l.NodoId = @objetivo",
            _ => "l.PanelId = @objetivo"
        };
        var fueraDelObjetivoActual = tipo == TipoObjetivoHistorico.Nodo ? "p.NodoId <> @objetivo"
            : "NOT EXISTS (SELECT 1 FROM Nodos n WHERE n.Id = p.NodoId AND n.ParqueId = @objetivo)";
        var panelesHistoricos = tipo == TipoObjetivoHistorico.Panel ? "" : $$"""
                UNION SELECT p.Id FROM Paneles p WHERE {{fueraDelObjetivoActual}} AND (
                    SELECT 1 FROM LecturasPanel l
                        FORCE INDEX (IX_LecturasPanel_PanelId_FechaHora_NodoId_PotenciaGeneradaW)
                    WHERE l.PanelId = p.Id AND {{procedencia}}
                        AND l.FechaHora >= DATE_SUB(@inicio, INTERVAL 5 MINUTE) AND l.FechaHora < @fin
                    LIMIT 1) = 1
                """;
        // La migración impone unicidad (PanelId, FechaHora): no se necesita
        // reagrupar/mediar todas las muestras antes de calcular sus tramos.
        var pertenece = tipo == TipoObjetivoHistorico.Panel ? "1" : $"CASE WHEN {procedencia} THEN 1 ELSE 0 END";
        var indiceMuestras = tipo == TipoObjetivoHistorico.Panel
            ? "IX_LecturasPanel_PanelId_FechaHora_PotenciaGeneradaW"
            : "IX_LecturasPanel_PanelId_FechaHora_NodoId_PotenciaGeneradaW";
        return $$"""
            SET STATEMENT tmp_table_size=268435456, max_heap_table_size=268435456 FOR
            WITH paneles_objetivo AS (
                SELECT DISTINCT p.Id FROM Paneles p INNER JOIN Nodos n ON n.Id = p.NodoId WHERE {{predicado}}
                {{panelesHistoricos}}
            ), muestras AS (
                SELECT l.PanelId, {{pertenece}} AS EsObjetivo, l.FechaHora, l.PotenciaGeneradaW AS Potencia
                FROM paneles_objetivo objetivo STRAIGHT_JOIN LecturasPanel l FORCE INDEX ({{indiceMuestras}})
                    ON l.PanelId = objetivo.Id
                WHERE l.FechaHora >= DATE_SUB(@inicio, INTERVAL 5 MINUTE) AND l.FechaHora < @fin
            ), tramos AS (
                SELECT EsObjetivo, DATE(FechaHora) AS DiaUtc, FechaHora, Potencia,
                    LEAST(COALESCE(LEAD(FechaHora) OVER (PARTITION BY PanelId ORDER BY FechaHora),
                        DATE_ADD(FechaHora, INTERVAL 5 MINUTE)), DATE_ADD(FechaHora, INTERVAL 5 MINUTE)) AS Fin
                FROM muestras
            ), intervalos AS (
                SELECT j.Ordinal, j.DiaUtc, j.InicioUtc, j.FinUtc
                FROM JSON_TABLE(@intervalos, '$[*]' COLUMNS (
                    Ordinal INT PATH '$.Ordinal', DiaUtc DATE PATH '$.DiaUtc',
                    InicioUtc DATETIME(6) PATH '$.InicioUtc', FinUtc DATETIME(6) PATH '$.FinUtc'
                )) j
            ), aportes AS (
                SELECT i.Ordinal,
                    SUM(t.Potencia * TIMESTAMPDIFF(MICROSECOND,
                        GREATEST(t.FechaHora, i.InicioUtc), LEAST(t.Fin, i.FinUtc))) AS EnergiaMicroWh,
                    COALESCE(SUM(TIMESTAMPDIFF(MICROSECOND,
                        GREATEST(t.FechaHora, i.InicioUtc), LEAST(t.Fin, i.FinUtc))), 0) AS Cubierto,
                    TIMESTAMPDIFF(MICROSECOND, i.InicioUtc, i.FinUtc)
                        * (SELECT COUNT(*) FROM paneles_objetivo) AS Esperado
                FROM intervalos i LEFT JOIN tramos t ON t.DiaUtc = i.DiaUtc
                    AND t.FechaHora < i.FinUtc AND t.Fin > i.InicioUtc
                    AND t.EsObjetivo = 1
                GROUP BY i.Ordinal, i.InicioUtc, i.FinUtc
            ), energias AS (
                SELECT Ordinal, CAST(EnergiaMicroWh AS DECIMAL(65,24)) / 3600000000000 AS Energia,
                    Cubierto, Esperado FROM aportes
            )
            SELECT Ordinal, Energia,
                CASE WHEN Cubierto = 0 THEN 0 WHEN Cubierto = Esperado THEN 2 ELSE 1 END AS Cobertura,
                CASE WHEN Energia = MAX(Energia) OVER () THEN 1 ELSE 0 END AS EsMaximo,
                CASE WHEN Energia = MIN(Energia) OVER () THEN 1 ELSE 0 END AS EsMinimo,
                SUM(Energia) OVER () AS Total,
                CASE WHEN SUM(Cubierto) OVER () = 0 THEN 0
                     WHEN SUM(Cubierto) OVER () = SUM(Esperado) OVER () THEN 2 ELSE 1 END AS CoberturaTotal
            FROM energias ORDER BY Ordinal
            """;
    }
}
