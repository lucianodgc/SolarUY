using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class HistoricoGeneracionIntegrationTests(HistoricoDatabase database, ITestOutputHelper output)
    : IClassFixture<HistoricoDatabase>
{
    private static readonly DateTime Fecha = new(2026, 2, 1);
    private FiltroHistoricoDto Filtro(PeriodoHistorico periodo = PeriodoHistorico.Diario,
        TipoObjetivoHistorico tipo = TipoObjetivoHistorico.Panel, Guid? id = null) => new(tipo,
            id ?? database.Panel.Id, periodo, Fecha);

    [MariaDbHistoricoTheory]
    [InlineData(PeriodoHistorico.Diario, TipoObjetivoHistorico.Panel, "0.28", 24)]
    [InlineData(PeriodoHistorico.Mensual, TipoObjetivoHistorico.Panel, "0.28", 28)]
    [InlineData(PeriodoHistorico.Anual, TipoObjetivoHistorico.Panel, "0.42", 12)]
    [InlineData(PeriodoHistorico.Diario, TipoObjetivoHistorico.Nodo, "0.48", 24)]
    [InlineData(PeriodoHistorico.Mensual, TipoObjetivoHistorico.Nodo, "0.48", 28)]
    [InlineData(PeriodoHistorico.Anual, TipoObjetivoHistorico.Nodo, "0.62", 12)]
    [InlineData(PeriodoHistorico.Diario, TipoObjetivoHistorico.Parque, "0.78", 24)]
    [InlineData(PeriodoHistorico.Mensual, TipoObjetivoHistorico.Parque, "0.78", 28)]
    [InlineData(PeriodoHistorico.Anual, TipoObjetivoHistorico.Parque, "0.92", 12)]
    public async Task Periodos_AgreganEnergiaConocidaYConservanLecturas(PeriodoHistorico periodo, TipoObjetivoHistorico tipo, string esperado, int intervalos)
    {
        await database.ReiniciarAsync();
        await using var sesion = database.AbrirSesion();
        await using var context = database.CrearContexto();
        var antes = await context.LecturasPanel.AsNoTracking().OrderBy(lectura => lectura.Id).ToListAsync();
        var id = tipo switch { TipoObjetivoHistorico.Parque => database.Parque.Id, TipoObjetivoHistorico.Nodo => database.Panel.NodoId, _ => database.Panel.Id };
        var resultado = await sesion.Servicio.ConsultarAsync(Filtro(periodo, tipo, id));
        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), resultado.EnergiaKWh);
        Assert.Equal(intervalos, resultado.Serie.Count);
        Assert.Equal(CoberturaHistorico.Parcial, resultado.Cobertura);
        Assert.Equal("kWh", resultado.Unidad);
        Assert.Equal("America/Montevideo", resultado.ZonaHoraria);
        Assert.Equal(3, resultado.Consulta.InicioUtc.Hour);
        var despues = await context.LecturasPanel.AsNoTracking().OrderBy(lectura => lectura.Id).ToListAsync();
        Assert.Equal(antes.Select(lectura => (lectura.Id, lectura.FechaHora, lectura.PotenciaGeneradaW, lectura.EstadoSalud)),
            despues.Select(lectura => (lectura.Id, lectura.FechaHora, lectura.PotenciaGeneradaW, lectura.EstadoSalud)));
        Assert.All(despues, lectura => Assert.Equal(DateTimeKind.Utc, lectura.FechaHora.Kind));
    }

    [MariaDbHistoricoFact]
    public async Task Jerarquia_AgregaSoloPanelesDelObjetivo()
    {
        await database.ReiniciarAsync();
        await using var sesion = database.AbrirSesion();
        var panel = await sesion.Servicio.ConsultarAsync(Filtro());
        var nodo = await sesion.Servicio.ConsultarAsync(Filtro(tipo: TipoObjetivoHistorico.Nodo, id: database.Panel.NodoId));
        var parque = await sesion.Servicio.ConsultarAsync(Filtro(tipo: TipoObjetivoHistorico.Parque, id: database.Parque.Id));
        var ajeno = await sesion.Servicio.ConsultarAsync(Filtro(tipo: TipoObjetivoHistorico.Parque, id: database.OtroParque.Id));
        Assert.Equal(0.28m, panel.EnergiaKWh);
        Assert.Equal(0.48m, nodo.EnergiaKWh);
        Assert.Equal(0.78m, parque.EnergiaKWh);
        Assert.Equal(1m, ajeno.EnergiaKWh);
        Assert.Equal(0.78m, parque.Serie.Single(punto => punto.EsMaximo).EnergiaKWh);
    }

    [MariaDbHistoricoFact]
    public async Task Rango_RecortaMuestrasPreviasPromediaDuplicadosYExcluyeFin()
    {
        await database.ReiniciarAsync();
        await using var sesion = database.AbrirSesion();
        var resultado = await sesion.Servicio.ConsultarAsync(new(TipoObjetivoHistorico.Panel, database.Panel.Id,
            PeriodoHistorico.Rango, Fecha, Fecha.AddMinutes(15)));
        Assert.Equal(0.18m, resultado.EnergiaKWh);
        Assert.Single(resultado.Serie);
        Assert.Equal(CoberturaHistorico.Parcial, resultado.Cobertura);
        var recortado = await sesion.Servicio.ConsultarAsync(new(TipoObjetivoHistorico.Panel, database.Panel.Id,
            PeriodoHistorico.Rango, Fecha.AddMinutes(1), Fecha.AddMinutes(3)));
        Assert.Equal(0.04m, recortado.EnergiaKWh);
        Assert.Equal(CoberturaHistorico.Completa, recortado.Cobertura);
    }

    [MariaDbHistoricoFact]
    public async Task Cobertura_DistingueCeroMedidoHuecosYObjetivoSinPaneles()
    {
        await database.ReiniciarAsync();
        await using var sesion = database.AbrirSesion();
        var cero = await sesion.Servicio.ConsultarAsync(new(TipoObjetivoHistorico.Panel, database.Panel.Id,
            PeriodoHistorico.Rango, Fecha.AddMinutes(4), Fecha.AddMinutes(9)));
        Assert.Equal(0m, cero.EnergiaKWh);
        Assert.Equal(CoberturaHistorico.Completa, cero.Cobertura);
        Assert.True(cero.Serie[0].EsMinimo);
        var hueco = await sesion.Servicio.ConsultarAsync(new(TipoObjetivoHistorico.Panel, database.Panel.Id,
            PeriodoHistorico.Rango, Fecha.AddMinutes(9), Fecha.AddMinutes(10)));
        Assert.Null(hueco.EnergiaKWh);
        Assert.Equal(CoberturaHistorico.SinDatos, hueco.Cobertura);
        Assert.False(hueco.Serie[0].EsMaximo);
        Assert.False(hueco.Serie[0].EsMinimo);
        await using var context = database.CrearContexto();
        var vacio = new SolarUY.Domain.Entities.ParqueSolar("Sin paneles", "Salto");
        context.ParquesSolares.Add(vacio);
        await context.SaveChangesAsync();
        Assert.Equal(CoberturaHistorico.SinDatos,
            (await sesion.Servicio.ConsultarAsync(Filtro(tipo: TipoObjetivoHistorico.Parque, id: vacio.Id))).Cobertura);
    }

    [MariaDbHistoricoFact]
    public async Task Extremos_ConservaTodosLosEmpatesYNoIncluyeIntervalosSinDatos()
    {
        await database.ReiniciarAsync();
        await using (var context = database.CrearContexto())
        {
            var panel = await context.Paneles.SingleAsync(panel => panel.Id == database.Panel.Id);
            foreach (var hora in new[] { 2, 3 })
                context.LecturasPanel.Add(HistoricoDatabase.Lectura(panel, Fecha.AddHours(hora), 1200));
            await context.SaveChangesAsync();
        }
        await using var sesion = database.AbrirSesion();
        var resultado = await sesion.Servicio.ConsultarAsync(Filtro());
        Assert.Single(resultado.Serie.Where(punto => punto.EsMaximo));
        Assert.Equal(0.28m, resultado.Serie.Single(punto => punto.EsMaximo).EnergiaKWh);
        Assert.Equal(2, resultado.Serie.Count(punto => punto.EsMinimo));
        Assert.All(resultado.Serie.Where(punto => punto.EsMinimo), punto => Assert.Equal(0.1m, punto.EnergiaKWh));
        Assert.All(resultado.Serie.Where(punto => punto.EnergiaKWh is null), punto => Assert.False(punto.EsMinimo || punto.EsMaximo));
    }

    [MariaDbHistoricoFact]
    public async Task Comparacion_ConservaTotalesObservadosYCambiaDeMesYAnio()
    {
        await database.ReiniciarAsync();
        await using var sesion = database.AbrirSesion();
        var comparacion = await sesion.Servicio.CompararAsync(Filtro() with { InicioLocal = new(2026, 1, 31) }, Filtro());
        Assert.Equal(0.04m, comparacion.Primero.EnergiaKWh);
        Assert.Equal(0.28m, comparacion.Segundo.EnergiaKWh);
        var anios = await sesion.Servicio.CompararAsync(Filtro(PeriodoHistorico.Anual) with { InicioLocal = new(2025, 1, 1) },
            Filtro(PeriodoHistorico.Anual));
        Assert.Equal(0.1m, anios.Primero.EnergiaKWh);
        Assert.Equal(0.42m, anios.Segundo.EnergiaKWh);
        var rango = await sesion.Servicio.ConsultarAsync(new(TipoObjetivoHistorico.Panel, database.Panel.Id,
            PeriodoHistorico.Rango, new(2025, 12, 31), new(2026, 2, 2)));
        Assert.Equal(0.32m, rango.EnergiaKWh);
        Assert.Equal(33, rango.Serie.Count);
    }

    [MariaDbHistoricoTheory]
    [InlineData("Administrador")]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task Roles_PuedenLeerTodosLosObjetivosYComparar(string rol)
    {
        await database.ReiniciarAsync();
        await using var sesion = database.AbrirSesion(rol);
        var opciones = await sesion.Servicio.ListarOpcionesAsync();
        Assert.Equal(9, opciones.Count);
        Assert.Contains(opciones, opcion => opcion.Id == database.OtroParque.Id);
        Assert.Equal(1m, (await sesion.Servicio.ConsultarAsync(Filtro(id: database.PanelAjeno.Id))).EnergiaKWh);
        Assert.Equal(0.28m, (await sesion.Servicio.CompararAsync(Filtro(), Filtro())).Primero.EnergiaKWh);
    }

    [MariaDbHistoricoTheory]
    [InlineData(2014, 10, 5, 23)]
    [InlineData(2015, 3, 8, 25)]
    public async Task Calendario_RespetaCambiosHistoricosDeHorario(int anio, int mes, int dia, int horas)
    {
        await database.ReiniciarAsync();
        await using var sesion = database.AbrirSesion();
        var resultado = await sesion.Servicio.ConsultarAsync(Filtro() with { InicioLocal = new(anio, mes, dia) });
        Assert.Equal(horas, resultado.Serie.Count);
        Assert.Equal(horas, (resultado.Consulta.FinUtc - resultado.Consulta.InicioUtc).TotalHours);
        if (horas == 25)
            Assert.Equal(2, resultado.Serie.Count(punto => punto.Intervalo.Etiqueta.StartsWith("2015-03-08 01:00")));
        await Assert.ThrowsAsync<ArgumentException>(() => sesion.Servicio.ConsultarAsync(new(TipoObjetivoHistorico.Panel,
            database.Panel.Id, PeriodoHistorico.Rango, new(anio, mes, dia, horas == 25 ? 1 : 2, 30, 0), new(anio, mes, dia, 5, 0, 0))));
    }

    [MariaDbHistoricoFact]
    public async Task CambioDeAnio_IntegraSoloSolapamientoYConservaCalendarioBisiesto()
    {
        await database.ReiniciarAsync();
        await using (var context = database.CrearContexto())
        {
            var panel = await context.Paneles.SingleAsync(panel => panel.Id == database.Panel.Id);
            context.LecturasPanel.AddRange(HistoricoDatabase.Lectura(panel, new(2025, 12, 31, 23, 58, 0), 1200),
                HistoricoDatabase.Lectura(panel, new(2026, 1, 1, 0, 1, 0), 600));
            await context.SaveChangesAsync();
        }
        await using var sesion = database.AbrirSesion();
        var comparacion = await sesion.Servicio.CompararAsync(Filtro(PeriodoHistorico.Anual) with { InicioLocal = new(2025, 1, 1) },
            Filtro(PeriodoHistorico.Anual));
        Assert.Equal(0.14m, comparacion.Primero.EnergiaKWh);
        Assert.Equal(0.49m, comparacion.Segundo.EnergiaKWh);
        Assert.Equal(29, (await sesion.Servicio.ConsultarAsync(Filtro(PeriodoHistorico.Mensual) with { InicioLocal = new(2024, 2, 1) })).Serie.Count);
    }

    [MariaDbHistoricoTheory]
    [InlineData(false, "usuario", "Administrador")]
    [InlineData(true, "", "Administrador")]
    [InlineData(true, "usuario", "SinPermiso")]
    public async Task Autorizacion_RechazaConsultaOpcionesYComparacion(bool autenticado, string id, string rol)
    {
        await database.ReiniciarAsync();
        await using var sesion = database.AbrirSesion(rol, autenticado, id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sesion.Servicio.ListarOpcionesAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sesion.Servicio.ConsultarAsync(Filtro()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sesion.Servicio.CompararAsync(Filtro(), Filtro()));
    }

    [MariaDbHistoricoFact]
    public async Task Validacion_RechazaIdentificadoresRangosYPeriodosInvalidos()
    {
        await database.ReiniciarAsync();
        await using var sesion = database.AbrirSesion();
        await Assert.ThrowsAsync<ArgumentException>(() => sesion.Servicio.ConsultarAsync(Filtro(id: Guid.Empty)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sesion.Servicio.ConsultarAsync(Filtro(id: Guid.NewGuid())));
        await Assert.ThrowsAsync<ArgumentException>(() => sesion.Servicio.ConsultarAsync(Filtro((PeriodoHistorico)99)));
        await Assert.ThrowsAsync<ArgumentException>(() => sesion.Servicio.ConsultarAsync(Filtro(tipo: (TipoObjetivoHistorico)99)));
        foreach (var fin in new[] { Fecha, Fecha.AddDays(-1) })
            await Assert.ThrowsAsync<ArgumentException>(() => sesion.Servicio.ConsultarAsync(Filtro(PeriodoHistorico.Rango) with { FinLocal = fin }));
        await Assert.ThrowsAsync<ArgumentException>(() => sesion.Servicio.ConsultarAsync(Filtro(PeriodoHistorico.Rango)));
        await Assert.ThrowsAsync<ArgumentException>(() => sesion.Servicio.ConsultarAsync(Filtro() with { InicioLocal = DateTime.UtcNow }));
        await Assert.ThrowsAsync<ArgumentException>(() => sesion.Servicio.CompararAsync(Filtro(), Filtro(id: database.PanelAjeno.Id)));
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sesion.Servicio.ConsultarAsync(Filtro(), cancelacion.Token));
    }

    [MariaDbHistoricoFact]
    public async Task BajaExistente_EliminaLecturasPropiasYConservaOtroParque()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        var parque = await context.ParquesSolares.SingleAsync(parque => parque.Id == database.Parque.Id);
        await context.EliminarParqueSolarEnCascadaAsync(parque);
        Assert.Single(await context.LecturasPanel.ToListAsync());
        Assert.True(await context.Paneles.AnyAsync(panel => panel.Id == database.PanelAjeno.Id));
        await using var sesion = database.AbrirSesion();
        Assert.Equal(1m, (await sesion.Servicio.ConsultarAsync(Filtro(id: database.PanelAjeno.Id))).EnergiaKWh);
    }

    [MariaDbHistoricoFact]
    public async Task Volumen_UnMillonDeLecturasAgregaEnServidorConCuatroConsultasConcurrentes()
    {
        await database.ReiniciarAsync();
        await using (var context = database.CrearContexto())
        {
            var nodo = await context.Nodos.SingleAsync(nodo => nodo.Id == database.Panel.NodoId);
            for (var indice = 0; indice < 96; indice++) context.Paneles.Add(HistoricoDatabase.CrearPanel(nodo, $"Volumen-{indice}"));
            await context.SaveChangesAsync();
            await context.LecturasPanel.ExecuteDeleteAsync();
            context.Database.SetCommandTimeout(120);
            await context.Database.ExecuteSqlRawAsync("""
                INSERT INTO LecturasPanel (Id, PanelId, FechaHora, PotenciaGeneradaW, EstadoSalud)
                SELECT UUID(), p.Id, DATE_ADD('2026-01-01 03:00:00', INTERVAL s.seq * 5 MINUTE), 1200, 0
                FROM Paneles p CROSS JOIN seq_0_to_9999 s
                """);
            Assert.Equal(1000000, await context.LecturasPanel.CountAsync());
        }
        output.WriteLine($"Entorno: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}; .NET {Environment.Version}; CPU lógicas {Environment.ProcessorCount}; MariaDB 11.8; 100 paneles x 10000 muestras cada 5 min desde 2026-01-01 Uruguay.");
        foreach (var tipo in Enum.GetValues<TipoObjetivoHistorico>())
        foreach (var periodo in new[] { PeriodoHistorico.Diario, PeriodoHistorico.Mensual, PeriodoHistorico.Anual })
        {
            var id = tipo switch { TipoObjetivoHistorico.Parque => database.Parque.Id, TipoObjetivoHistorico.Nodo => database.Panel.NodoId, _ => database.Panel.Id };
            var filtro = new FiltroHistoricoDto(tipo, id, periodo, new(2026, 1, 1));
            await MedirAsync($"{tipo}/{periodo}", filtro, false);
        }
        await MedirAsync("Comparación parque/mes", new(TipoObjetivoHistorico.Parque, database.Parque.Id,
            PeriodoHistorico.Mensual, new(2026, 1, 1)), true);
        await VerificarPlanAsync();
    }

    private async Task MedirAsync(string escenario, FiltroHistoricoDto filtro, bool comparar)
    {
        GC.Collect();
        using var proceso = Process.GetCurrentProcess();
        proceso.Refresh();
        var memoriaInicial = proceso.WorkingSet64;
        var memoriaMaxima = memoriaInicial;
        var consultas = Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var sesion = database.AbrirSesion();
            var reloj = Stopwatch.StartNew();
            if (comparar) await sesion.Servicio.CompararAsync(filtro, filtro with { InicioLocal = new(2026, 2, 1) });
            else Assert.NotNull((await sesion.Servicio.ConsultarAsync(filtro)).EnergiaKWh);
            return reloj.Elapsed.TotalSeconds;
        }).ToArray();
        var conjunto = Task.WhenAll(consultas);
        while (!conjunto.IsCompleted)
        {
            proceso.Refresh();
            memoriaMaxima = Math.Max(memoriaMaxima, proceso.WorkingSet64);
            await Task.WhenAny(conjunto, Task.Delay(20));
        }
        var tiempos = await conjunto;
        var memoriaMiB = (memoriaMaxima - memoriaInicial) / 1048576d;
        output.WriteLine($"{escenario}: segundos [{string.Join(", ", tiempos.Select(tiempo => tiempo.ToString("F3")))}], memoria adicional máxima {memoriaMiB:F2} MiB.");
        Assert.All(tiempos, tiempo => Assert.True(tiempo < 5, $"{escenario}: {tiempo:F3}s excede 5s."));
        Assert.True(memoriaMiB < 256, $"{escenario}: memoria adicional {memoriaMiB:F2} MiB excede 256 MiB.");
    }

    private async Task VerificarPlanAsync()
    {
        await using var context = database.CrearContexto();
        await context.Database.OpenConnectionAsync();
        // Inspeccionar el comando real de producción; no se ejecuta una consulta alternativa.
        var consulta = new ConsultaHistoricoDto(TipoObjetivoHistorico.Panel, database.Panel.Id,
            new(2026, 1, 1, 3, 0, 0, DateTimeKind.Utc), new(2026, 1, 2, 3, 0, 0, DateTimeKind.Utc),
            new[] { new IntervaloHistoricoDto(new(2026, 1, 1, 3, 0, 0, DateTimeKind.Utc), new(2026, 1, 2, 3, 0, 0, DateTimeKind.Utc), "2026-01-01") });
        var creador = typeof(HistoricoGeneracionRepository).GetMethod("CrearComando", BindingFlags.NonPublic | BindingFlags.Static)!;
        await using var real = (System.Data.Common.DbCommand)creador.Invoke(null, new object[] { context, consulta })!;
        real.CommandText = real.CommandText.Insert(real.CommandText.IndexOf("WITH", StringComparison.Ordinal), "EXPLAIN FORMAT=JSON ");
        var plan = (string)(await real.ExecuteScalarAsync())!;
        output.WriteLine("Plan SQL: " + plan);
        Assert.Contains("IX_LecturasPanel_PanelId_FechaHora_PotenciaGeneradaW", plan);
        Assert.Contains("window_functions_computation", plan);
    }
}
