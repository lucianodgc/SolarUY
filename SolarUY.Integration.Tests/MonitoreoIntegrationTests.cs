using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Entities;
using SolarUY.Infrastructure;
using SolarUY.Application.Abstractions;
using SolarUY.Domain.Enums;
using SolarUY.Domain.Monitoreo;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SolarUY.Infrastructure.Persistence;
using System.Text.Json;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class MonitoreoIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task Consulta_UsaUnNumeroAcotadoDeConsultasYElIndiceTemporalReal()
    {
        await database.ReiniciarAsync();
        var observador = new ObservadorConsultas();
        await using var provider = CrearProvider(observador: observador);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMonitoreoService>().ConsultarAsync(database.Parque.Id);
        Assert.Equal(4, observador.Consultas.Count);
        var consulta = Assert.Single(observador.Consultas.Where(c => c.Sql.Contains("`LecturasPanel`")));
        await using var context = database.CrearContexto();
        await context.Database.OpenConnectionAsync();
        await using var comando = context.Database.GetDbConnection().CreateCommand();
        comando.CommandText = "EXPLAIN FORMAT=JSON " + consulta.Sql;
        foreach (var (nombre, valor) in consulta.Parametros)
        {
            var parametro = comando.CreateParameter(); parametro.ParameterName = nombre; parametro.Value = valor;
            comando.Parameters.Add(parametro);
        }
        using var plan = JsonDocument.Parse((string)(await comando.ExecuteScalarAsync())!);
        Assert.True(UsaIndice(plan.RootElement), plan.RootElement.GetRawText());
    }

    [MariaDbHistoricoTheory]
    [InlineData("Administrador")]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task Servicio_AutorizaRolesYCalculaEstadoParcial(string rol)
    {
        await database.ReiniciarAsync();
        await using var provider = CrearProvider(rol);
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IMonitoreoService>();
        Assert.Equal(2, (await servicio.ListarParquesAsync()).Count);
        var estado = await servicio.ConsultarAsync(database.Parque.Id);
        Assert.Equal(1200m, estado.Generacion.PotenciaW);
        Assert.Equal(1, estado.Generacion.PanelesConDatos);
        Assert.False(estado.Generacion.Completo);
        Assert.Equal(2, estado.Nodos.Count);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servicio.ConsultarAsync(Guid.NewGuid()));
    }

    [MariaDbHistoricoTheory]
    [InlineData("Invitado", true, "usuario")]
    [InlineData("Administrador", false, "usuario")]
    [InlineData("Administrador", true, "")]
    public async Task Servicio_RechazaIdentidadesNoAutorizadas(string rol, bool autenticado, string id)
    {
        await using var provider = CrearProvider(rol, autenticado, id);
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IMonitoreoService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ListarParquesAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ConsultarAsync(Guid.Empty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ReevaluarAsync(Guid.Empty));
    }

    [MariaDbHistoricoFact]
    public async Task Reevaluacion_ExpiraElEstadoCargadoSinVolverAConsultarMariaDb()
    {
        await database.ReiniciarAsync();
        var reloj = new RelojMonitoreo();
        await using var provider = CrearProvider(reloj: reloj);
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IMonitoreoService>();
        await servicio.ConsultarAsync(database.Parque.Id);
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        Assert.Equal(1200m, (await servicio.ReevaluarAsync(database.Parque.Id)).Generacion.PotenciaW);
        reloj.Ahora = reloj.Ahora.AddMinutes(2);
        var expirado = await servicio.ReevaluarAsync(database.Parque.Id);
        Assert.Null(expirado.Generacion.PotenciaW);
        Assert.Contains(expirado.Nodos.SelectMany(n => n.Paneles), p => p.Generacion.Disponibilidad == DisponibilidadMonitoreo.FueraDeVigencia);
    }

    [MariaDbHistoricoFact]
    public async Task TopologiaActual_NoSumaUnaMedicionDelNodoAnterior()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        var nuevoNodo = await context.Nodos.SingleAsync(n => n.Id == database.TercerPanel.NodoId);
        await context.Paneles.Where(p => p.Id == database.Panel.Id)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(p => p.NodoId, nuevoNodo.Id));
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var estado = await scope.ServiceProvider.GetRequiredService<IMonitoreoService>().ConsultarAsync(database.Parque.Id);
        var panel = estado.Nodos.SelectMany(n => n.Paneles).Single(p => p.Id == database.Panel.Id);
        Assert.Equal(DisponibilidadMonitoreo.NodoAnterior, panel.Generacion.Disponibilidad);
        Assert.Null(panel.Generacion.PotenciaW);
        Assert.Null(estado.Generacion.PotenciaW);
    }

    [MariaDbHistoricoFact]
    public async Task Serie_AcotaDeduplicaDescartaAtrasosYCortaLosHuecos()
    {
        await database.ReiniciarAsync();
        var reloj = new RelojMonitoreo();
        await using var provider = CrearProvider(reloj: reloj);
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IMonitoreoService>();
        var serie = new SerieMonitoreoService(2, TimeSpan.FromMinutes(1));
        var inicial = await servicio.ConsultarAsync(database.Parque.Id);
        Assert.Single(serie.Actualizar(inicial));
        Assert.Single(serie.Actualizar(inicial));
        await using var context = database.CrearContexto();
        var panel = await context.Paneles.SingleAsync(p => p.Id == database.Panel.Id);
        await context.LecturasPanel.ExecuteDeleteAsync();
        for (var indice = 0; indice < 3; indice++)
        {
            context.LecturasPanel.Add(new LecturaPanel(panel, reloj.Ahora.UtcDateTime, 100 + indice, EstadoSaludLectura.Sano));
            await context.SaveChangesAsync();
            serie.Actualizar(await servicio.ConsultarAsync(database.Parque.Id));
            reloj.Ahora = reloj.Ahora.AddMinutes(1);
        }
        var actual = serie.Actualizar(await servicio.ReevaluarAsync(database.Parque.Id));
        Assert.Equal(2, actual.Count);
        Assert.Equal(actual, serie.Actualizar(inicial));
        reloj.Ahora = reloj.Ahora.AddMinutes(3);
        context.LecturasPanel.Add(new LecturaPanel(panel, reloj.Ahora.UtcDateTime, 200, EstadoSaludLectura.Sano));
        await context.SaveChangesAsync();
        Assert.Single(serie.Actualizar(await servicio.ConsultarAsync(database.Parque.Id)));
    }

    [MariaDbHistoricoFact]
    public async Task Consulta_ObtieneUltimaMuestraPorPanelSinEscribirNiIncluirOtrosParques()
    {
        await database.ReiniciarAsync();
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMonitoreoRepository>();
        await using var context = database.CrearContexto();
        var cantidadAntes = await context.LecturasPanel.CountAsync();
        var referencia = new DateTime(2026, 2, 1, 3, 3, 0, DateTimeKind.Utc);
        var datos = await repository.ObtenerDatosAsync(database.Parque.Id, referencia);
        Assert.NotNull(datos);
        Assert.Equal(database.Parque.Id, datos.Parque.Id);
        Assert.Equal(2, datos.Nodos.Count);
        Assert.Equal(3, datos.Paneles.Count);
        Assert.Equal(3, datos.Lecturas.Count);
        Assert.Equal(3, datos.Lecturas.Select(l => l.PanelId).Distinct().Count());
        Assert.DoesNotContain(datos.Lecturas, l => l.PanelId == database.PanelAjeno.Id);
        var ultima = Assert.Single(datos.Lecturas.Where(l => l.PanelId == database.Panel.Id));
        Assert.Equal(referencia.AddMinutes(-1), ultima.FechaHora);
        Assert.All(datos.Lecturas, l => Assert.True(l.FechaHora <= referencia));
        Assert.Equal(cantidadAntes, await context.LecturasPanel.CountAsync());
    }

    [MariaDbHistoricoFact]
    public async Task Consulta_DistingueParqueInexistenteVacioYPanelesSinLecturas()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        var vacio = new ParqueSolar("Parque vacío monitoreo", "Salto");
        context.ParquesSolares.Add(vacio);
        await context.SaveChangesAsync();
        await context.LecturasPanel.ExecuteDeleteAsync();
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMonitoreoRepository>();
        var referencia = DateTime.UtcNow;
        Assert.Null(await repository.ObtenerDatosAsync(Guid.NewGuid(), referencia));
        var datosVacios = await repository.ObtenerDatosAsync(vacio.Id, referencia);
        Assert.NotNull(datosVacios);
        Assert.Empty(datosVacios.Nodos);
        Assert.Empty(datosVacios.Paneles);
        Assert.Empty(datosVacios.Lecturas);
        var sinMuestras = await repository.ObtenerDatosAsync(database.Parque.Id, referencia);
        Assert.NotNull(sinMuestras);
        Assert.Equal(3, sinMuestras.Paneles.Count);
        Assert.Empty(sinMuestras.Lecturas);
        Assert.Contains(await repository.ListarParquesAsync(), p => p.Id == vacio.Id);
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.ListarParquesAsync(cancelacion.Token));
    }

    private static ServiceProvider CrearProvider(string rol = "Administrador", bool autenticado = true,
        string id = "usuario-monitoreo", RelojMonitoreo? reloj = null, ObservadorConsultas? observador = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)
        }).Build();
        var services = new ServiceCollection();
        services.AddSolarInfrastructure(configuration);
        if (observador is not null)
        {
            var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseMySql(configuration.GetConnectionString("SolarUY"), new MariaDbServerVersion(new Version(11, 8, 0)))
                .AddInterceptors(observador).Options;
            services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(new FabricaObservada(opciones));
        }
        services.AddSingleton<IUsuarioActual>(new UsuarioMonitoreo(new(autenticado, id, [rol])));
        services.AddSingleton<TimeProvider>(reloj ?? new RelojMonitoreo());
        services.AddSingleton(new PoliticaMonitoreo(60, 2));
        services.AddSingleton<GeneracionActualService>();
        services.AddScoped<IMonitoreoService, MonitoreoService>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class UsuarioMonitoreo(UsuarioActualDto usuario) : IUsuarioActual
    {
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(usuario); }
    }

    private sealed class RelojMonitoreo : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = new(2026, 2, 1, 3, 3, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private static bool UsaIndice(JsonElement elemento)
    {
        if (elemento.ValueKind == JsonValueKind.Object)
            return elemento.EnumerateObject().Any(p => p.Name == "key" && p.Value.ValueKind == JsonValueKind.String
                && p.Value.GetString()!.StartsWith("IX_LecturasPanel_PanelId_FechaHora", StringComparison.Ordinal)
                || UsaIndice(p.Value));
        return elemento.ValueKind == JsonValueKind.Array && elemento.EnumerateArray().Any(UsaIndice);
    }

    private sealed class FabricaObservada(DbContextOptions<ApplicationDbContext> opciones) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(opciones);
    }

    private sealed class ObservadorConsultas : DbCommandInterceptor
    {
        public List<(string Sql, (string Nombre, object Valor)[] Parametros)> Consultas { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Consultas.Add((command.CommandText, command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value!)).ToArray()));
            return ValueTask.FromResult(result);
        }
    }
}
