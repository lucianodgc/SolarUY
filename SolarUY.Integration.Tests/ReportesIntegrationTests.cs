using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.Mantenimiento;
using SolarUY.Application.Reportes;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Infrastructure;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class ReportesIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    private static readonly DateTime Dia = new(2026, 2, 1);
    private static readonly DateTime InicioUtc = new(2026, 2, 1, 3, 0, 0, DateTimeKind.Utc);

    [MariaDbHistoricoFact]
    public async Task ReporteYCsv_AdviertenLegadoSinAtribuirSuEnergiaAlParque()
    {
        await database.ReiniciarAsync();
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>();
        var filtro = Filtro(PeriodoHistorico.Diario, false);
        var antes = await servicio.ConsultarAsync(filtro);
        await using (var context = database.CrearContexto())
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO LecturasPanel (Id, PanelId, NodoId, FechaHora, PotenciaGeneradaW, EstadoSalud)
                VALUES ({Guid.NewGuid()}, {database.Panel.Id}, NULL, {InicioUtc.AddHours(1)}, 9000, 0)
                """);
        var despues = await servicio.ConsultarAsync(filtro);
        Assert.Equal(antes.Generacion.EnergiaKWh, despues.Generacion.EnergiaKWh);
        Assert.Equal(1, despues.Generacion.LecturasSinNodoHistorico);
        var csv = Encoding.UTF8.GetString((await servicio.ExportarCsvAsync(despues)).Contenido);
        Assert.Contains("\"Advertencia\"", csv);
        Assert.Contains("sin nodo histórico verificado", csv);
    }

    [MariaDbHistoricoTheory]
    [InlineData(PeriodoHistorico.Diario, false)]
    [InlineData(PeriodoHistorico.Diario, true)]
    [InlineData(PeriodoHistorico.Mensual, false)]
    [InlineData(PeriodoHistorico.Mensual, true)]
    [InlineData(PeriodoHistorico.Anual, false)]
    [InlineData(PeriodoHistorico.Anual, true)]
    [InlineData(PeriodoHistorico.Rango, false)]
    [InlineData(PeriodoHistorico.Rango, true)]
    public async Task Reporte_ReutilizaExactamenteElHistorico(PeriodoHistorico periodo, bool porNodo)
    {
        await database.ReiniciarAsync();
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var filtro = Filtro(periodo, porNodo);
        var reporte = await scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>().ConsultarAsync(filtro);
        var esperado = await scope.ServiceProvider.GetRequiredService<IHistoricoGeneracionService>().ConsultarAsync(
            new(porNodo ? TipoObjetivoHistorico.Nodo : TipoObjetivoHistorico.Parque,
                filtro.NodoId ?? filtro.ParqueId, periodo, Dia, Dia.AddDays(1)));
        Assert.Equal(esperado.EnergiaKWh, reporte.Generacion.EnergiaKWh);
        Assert.Equal(esperado.Cobertura, reporte.Generacion.Cobertura);
        Assert.Equal(esperado.Consulta.InicioUtc, reporte.Generacion.Consulta.InicioUtc);
        Assert.Equal(esperado.Consulta.FinUtc, reporte.Generacion.Consulta.FinUtc);
        Assert.Equal(esperado.Serie, reporte.Generacion.Serie);
        if (periodo == PeriodoHistorico.Diario) Assert.Equal(porNodo ? 0.48m : 0.78m, reporte.Generacion.EnergiaKWh);
        var opciones = await scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>().ListarOpcionesAsync();
        Assert.DoesNotContain(opciones, o => o.Tipo == TipoObjetivoHistorico.Panel);
        Assert.Contains(opciones, o => o.Id == database.OtroParque.Id);
    }

    [MariaDbHistoricoTheory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task Mantenimientos_UsanRealizacionLimitesYObjetivo(bool porNodo, int cantidad)
    {
        await database.ReiniciarAsync();
        await using (var context = database.CrearContexto())
        {
            context.AttachRange(database.Panel, database.TercerPanel, database.PanelAjeno);
            var valida = Completada(database.Panel, InicioUtc);
            var otraDelParque = Completada(database.TercerPanel, InicioUtc.AddHours(3));
            var sinFecha = Completada(database.Panel, InicioUtc.AddHours(1));
            context.TareasMantenimiento.AddRange(valida, otraDelParque, sinFecha,
                Completada(database.Panel, InicioUtc.AddTicks(-10)),
                Completada(database.Panel, InicioUtc.AddDays(1)),
                Completada(database.PanelAjeno, InicioUtc),
                new TareaMantenimiento(database.Panel, "Pendiente", "No realizada", InicioUtc, "otro-tecnico"));
            context.Entry(sinFecha).Property(t => t.FechaRealizacion).CurrentValue = null;
            var cancelada = new TareaMantenimiento(database.Panel, "Cancelada", "No realizada", InicioUtc, "otro-tecnico");
            cancelada.Cancelar();
            context.Add(cancelada);
            await context.SaveChangesAsync();
        }
        await using var provider = CrearProvider("Consulta");
        await using var scope = provider.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>().ConsultarAsync(Filtro(porNodo: porNodo));
        Assert.Equal(cantidad, resultado.Mantenimientos.Count);
        Assert.All(resultado.Mantenimientos, t =>
        {
            Assert.Equal(EstadoTareaMantenimiento.Completada, t.Estado);
            Assert.InRange(t.FechaRealizacion, InicioUtc, InicioUtc.AddDays(1).AddTicks(-1));
            Assert.Equal(DateTimeKind.Utc, t.FechaRealizacion.Kind);
            Assert.EndsWith("-03:00", t.FechaRealizacionLocal);
        });
    }

    [MariaDbHistoricoTheory]
    [InlineData("Administrador")]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task Roles_PuedenLeerYExportarGlobalmenteSinAmpliarGestion(string rol)
    {
        await database.ReiniciarAsync();
        await using (var context = database.CrearContexto())
        {
            context.Attach(database.PanelAjeno);
            context.Add(Completada(database.PanelAjeno, InicioUtc));
            await context.SaveChangesAsync();
        }
        await using var provider = CrearProvider(rol);
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>();
        var reporte = await servicio.ConsultarAsync(Filtro() with { ParqueId = database.OtroParque.Id });
        Assert.Single(reporte.Mantenimientos);
        Assert.NotEmpty((await servicio.ExportarCsvAsync(reporte)).Contenido);
        var mantenimiento = scope.ServiceProvider.GetRequiredService<ITareaMantenimientoService>();
        if (rol == "Consulta")
            await Assert.ThrowsAsync<MantenimientoException>(() => mantenimiento.ListarAsync(new()));
        else
        {
            var tareas = await mantenimiento.ListarAsync(new());
            if (rol == "Tecnico") Assert.Empty(tareas);
            else Assert.Single(tareas);
        }
    }

    [MariaDbHistoricoTheory]
    [InlineData(false, "Consulta", "usuario")]
    [InlineData(true, "SinPermiso", "usuario")]
    [InlineData(true, "Consulta", "")]
    public async Task Operaciones_RechazanIdentidadesSinAcceso(bool autenticado, string rol, string id)
    {
        await database.ReiniciarAsync();
        var identidad = new IdentidadReporte(new(true, "usuario", ["Administrador"]));
        await using var provider = CrearProvider(identidad: identidad);
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>();
        var resultado = await servicio.ConsultarAsync(Filtro());
        identidad.Usuario = new(autenticado, id, [rol]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ListarOpcionesAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ConsultarAsync(Filtro()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ExportarCsvAsync(resultado));
    }

    [MariaDbHistoricoTheory]
    [InlineData("parque-vacio")]
    [InlineData("parque-inexistente")]
    [InlineData("nodo-ajeno")]
    [InlineData("nodo-inexistente")]
    [InlineData("rango")]
    [InlineData("periodo")]
    [InlineData("fecha-utc")]
    public async Task Filtros_InvalidosNoProducenReporte(string caso)
    {
        await database.ReiniciarAsync();
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var filtro = Filtro();
        filtro = caso switch
        {
            "parque-vacio" => filtro with { ParqueId = Guid.Empty },
            "parque-inexistente" => filtro with { ParqueId = Guid.NewGuid() },
            "nodo-ajeno" => filtro with { NodoId = database.PanelAjeno.NodoId },
            "nodo-inexistente" => filtro with { NodoId = Guid.NewGuid() },
            "rango" => filtro with { Periodo = PeriodoHistorico.Rango, FinLocal = Dia },
            "periodo" => filtro with { Periodo = (PeriodoHistorico)99 },
            _ => filtro with { InicioLocal = InicioUtc }
        };
        var exception = await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>().ConsultarAsync(filtro));
        Assert.NotNull(exception);
        if (caso is "parque-inexistente" or "nodo-inexistente") Assert.IsType<KeyNotFoundException>(exception);
        else Assert.IsType<ArgumentException>(exception);
    }

    [MariaDbHistoricoFact]
    public async Task Csv_ConservaResultadoEscapaCamposYNoEscribeDatos()
    {
        await database.ReiniciarAsync();
        const string descripcion = "Revisión; \"válvula\"\r\nLínea ñ";
        await using (var context = database.CrearContexto())
        {
            context.Attach(database.Panel);
            context.Add(Completada(database.Panel, InicioUtc, descripcion));
            await context.SaveChangesAsync();
        }
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>();
        var antes = await HuellaAsync();
        var reporte = await servicio.ConsultarAsync(Filtro());
        var archivo = await servicio.ExportarCsvAsync(reporte);
        Assert.Equal(antes, await HuellaAsync());
        Assert.False(archivo.Contenido.Take(3).SequenceEqual(new byte[] { 239, 187, 191 }));
        var filas = LeerCsv(Encoding.UTF8.GetString(archivo.Contenido));
        Assert.Equal(26, filas.Count);
        Assert.All(filas, fila => Assert.Equal(24, fila.Count));
        Assert.Equal("TipoRegistro", filas[0][0]);
        Assert.Contains(filas, fila => fila[0] == "Mantenimiento" && fila[19] == descripcion);
        Assert.Equal(24, filas.Count(f => f[0] == "Generacion"));
        Assert.Contains(filas, fila => fila[0] == "Generacion" && fila[13].Length == 0 && fila[14] == "SinDatos");
        Assert.EndsWith("-03:00", filas[1][6]);
        Assert.Equal(reporte.Generacion.EnergiaKWh!.Value, decimal.Parse(filas[1][9], CultureInfo.InvariantCulture));
        await using (var context = database.CrearContexto())
        {
            context.Attach(database.Panel);
            context.Add(HistoricoDatabase.Lectura(database.Panel, Dia.AddHours(2), 1234.567891m));
            await context.SaveChangesAsync();
        }
        var despues = await HuellaAsync();
        Assert.Equal(archivo.Contenido, (await servicio.ExportarCsvAsync(reporte)).Contenido);
        Assert.Equal(despues, await HuellaAsync());
        var nuevo = await servicio.ConsultarAsync(Filtro());
        Assert.True(nuevo.Generacion.EnergiaKWh > reporte.Generacion.EnergiaKWh);
    }

    [MariaDbHistoricoFact]
    public async Task Grafico_NoUneHuecosYConservaCeroMedido()
    {
        await database.ReiniciarAsync();
        await using (var context = database.CrearContexto())
        {
            context.Attach(database.Panel);
            context.AddRange(HistoricoDatabase.Lectura(database.Panel, Dia.AddHours(2), 1200),
                HistoricoDatabase.Lectura(database.Panel, Dia.AddHours(4), 0));
            await context.SaveChangesAsync();
        }
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>();
        var reporte = await servicio.ConsultarAsync(Filtro(porNodo: true));
        Assert.Equal(3, reporte.Grafico.Segmentos.Count);
        Assert.Equal(3, reporte.Grafico.Puntos.Count);
        Assert.Equal(0, reporte.Grafico.Puntos[2].EnergiaKWh);
        Assert.Equal(new[] { 3, 5, 7 }, reporte.Grafico.Puntos.Select(p => p.InstanteUtc.Hour));
        var cero = await servicio.ConsultarAsync(new(database.Parque.Id, database.Panel.NodoId,
            PeriodoHistorico.Rango, Dia.AddMinutes(5), Dia.AddMinutes(9)));
        Assert.Equal(0m, cero.Generacion.EnergiaKWh);
        var filas = LeerCsv(Encoding.UTF8.GetString((await servicio.ExportarCsvAsync(cero)).Contenido));
        Assert.Equal(0m, decimal.Parse(filas[1][13], CultureInfo.InvariantCulture));
        Assert.NotEqual("SinDatos", filas[1][14]);
    }

    [MariaDbHistoricoFact]
    public async Task SinLecturas_ExportaIntervalosVaciosYMantenimiento()
    {
        await database.ReiniciarAsync();
        await using (var context = database.CrearContexto())
        {
            await context.LecturasPanel.ExecuteDeleteAsync();
            context.Attach(database.Panel);
            context.Add(Completada(database.Panel, InicioUtc));
            await context.SaveChangesAsync();
        }
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>();
        var reporte = await servicio.ConsultarAsync(Filtro());
        Assert.Null(reporte.Generacion.EnergiaKWh);
        Assert.Single(reporte.Mantenimientos);
        Assert.Empty(reporte.Grafico.Puntos);
        var filas = LeerCsv(Encoding.UTF8.GetString((await servicio.ExportarCsvAsync(reporte)).Contenido));
        Assert.Equal(26, filas.Count);
        Assert.All(filas.Where(f => f[0] == "Generacion"), fila => { Assert.Empty(fila[13]); Assert.Equal("SinDatos", fila[14]); });
        var vacio = await servicio.ConsultarAsync(Filtro() with { InicioLocal = Dia.AddYears(3) });
        Assert.Empty(vacio.Mantenimientos);
        Assert.NotEmpty((await servicio.ExportarCsvAsync(vacio)).Contenido);
    }

    [MariaDbHistoricoFact]
    public async Task Cancelacion_SePropagaEnTodasLasOperaciones()
    {
        await database.ReiniciarAsync();
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IReporteHistoricoService>();
        var reporte = await servicio.ConsultarAsync(Filtro());
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => servicio.ListarOpcionesAsync(cancelacion.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => servicio.ConsultarAsync(Filtro(), cancelacion.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => servicio.ExportarCsvAsync(reporte, cancelacion.Token));
    }

    private FiltroReporteDto Filtro(PeriodoHistorico periodo = PeriodoHistorico.Diario, bool porNodo = false)
        => new(database.Parque.Id, porNodo ? database.Panel.NodoId : null, periodo, Dia, Dia.AddDays(1));

    private static TareaMantenimiento Completada(Panel panel, DateTime fecha, string descripcion = "Mantenimiento realizado")
    {
        var tarea = new TareaMantenimiento(panel, "Limpieza", descripcion, InicioUtc.AddMonths(-1), "otro-tecnico");
        tarea.Iniciar();
        tarea.Completar(fecha);
        return tarea;
    }

    private async Task<string> HuellaAsync()
    {
        await using var context = database.CrearContexto();
        return JsonSerializer.Serialize(new
        {
            Lecturas = await context.LecturasPanel.AsNoTracking().OrderBy(l => l.Id)
                .Select(l => new { l.Id, l.PanelId, l.FechaHora, l.PotenciaGeneradaW, l.EstadoSalud }).ToArrayAsync(),
            Tareas = await context.TareasMantenimiento.AsNoTracking().OrderBy(t => t.Id)
                .Select(t => new { t.Id, t.PanelId, t.Tipo, t.Descripcion, t.Estado, t.ResponsableId, t.FechaProgramada, t.FechaRealizacion }).ToArrayAsync()
        });
    }

    private static List<List<string>> LeerCsv(string csv)
    {
        var filas = new List<List<string>>();
        var fila = new List<string>();
        var campo = new StringBuilder();
        var dentro = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var caracter = csv[i];
            if (caracter == '"')
            {
                if (dentro && i + 1 < csv.Length && csv[i + 1] == '"') { campo.Append('"'); i++; }
                else dentro = !dentro;
            }
            else if (!dentro && caracter == ';') { fila.Add(campo.ToString()); campo.Clear(); }
            else if (!dentro && caracter == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
            {
                fila.Add(campo.ToString()); campo.Clear(); filas.Add(fila); fila = []; i++;
            }
            else campo.Append(caracter);
        }
        Assert.False(dentro);
        return filas;
    }

    private static ServiceProvider CrearProvider(string rol = "Administrador", IdentidadReporte? identidad = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSolarInfrastructure(configuration);
        services.AddSingleton<IUsuarioActual>(identidad ?? new IdentidadReporte(new(true, "usuario-prueba", [rol])));
        services.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
        services.AddScoped<IReporteHistoricoService, ReporteHistoricoService>();
        services.AddScoped<ITareaMantenimientoService, TareaMantenimientoService>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class IdentidadReporte(UsuarioActualDto usuario) : IUsuarioActual
    {
        public UsuarioActualDto Usuario { get; set; } = usuario;
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Usuario);
        }
    }
}
