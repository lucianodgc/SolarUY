using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarUY.Application.Abstractions;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Infrastructure;
using SolarUY.Infrastructure.Inversores;
using SolarUY.Presentation.BackgroundServices;

namespace SolarUY.Integration.Tests;

[CollectionDefinition("MariaDB histórico", DisableParallelization = true)]
public sealed class HistoricoCollection;

[Collection("MariaDB histórico")]
public sealed class RecoleccionLecturasIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    private static readonly DateTime Instante = new(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);

    private async Task PrepararAsync()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
    }

    [MariaDbHistoricoTheory]
    [InlineData(15, 450)]
    [InlineData(3, 0)]
    public async Task Ciclo_PersisteInventarioRealYAlimentaHistorico(int horaUtc, decimal potencia)
    {
        await PrepararAsync();
        var reloj = new RelojRecoleccion(Instante.Date.AddHours(horaUtc));
        await using var provider = CrearProvider(reloj);
        await using var scope = provider.CreateAsyncScope();
        var colector = scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>();
        Assert.Equal(4, (await colector.RecolectarAsync()).LecturasGuardadas);
        await using var context = database.CrearContexto();
        var lecturas = await context.LecturasPanel.AsNoTracking().ToArrayAsync();
        Assert.Equal(4, lecturas.Length);
        Assert.All(lecturas, lectura =>
        {
            Assert.Equal(reloj.GetUtcNow().UtcDateTime, lectura.FechaHora);
            Assert.Equal(DateTimeKind.Utc, lectura.FechaHora.Kind);
            Assert.Equal(potencia, lectura.PotenciaGeneradaW);
            Assert.Equal(EstadoSaludLectura.Sano, lectura.EstadoSalud);
        });
        Assert.Equal(new[] { "nodo-1" }, await scope.ServiceProvider.GetRequiredService<IInversorClient>().ListarNodosAsync());
        await using var sesion = database.AbrirSesion();
        var inicio = new DateTime(2026, 10, 22, horaUtc - 3, 0, 0);
        var resultado = await sesion.Servicio.ConsultarAsync(new(TipoObjetivoHistorico.Parque, database.Parque.Id,
            PeriodoHistorico.Rango, inicio, inicio.AddMinutes(1)));
        Assert.Equal(potencia * 3m / 60000m, resultado.EnergiaKWh);
        Assert.Equal(CoberturaHistorico.Completa, resultado.Cobertura);
    }

    [MariaDbHistoricoFact]
    public async Task Ciclos_SonIdempotentesEntreProveedoresYReinicios()
    {
        await PrepararAsync();
        var reloj = new RelojRecoleccion(Instante.AddSeconds(12));
        await using var primero = CrearProvider(reloj);
        await using (var scope = primero.CreateAsyncScope())
            Assert.Equal(4, (await scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync()).LecturasGuardadas);
        reloj.Avanzar(TimeSpan.FromSeconds(20));
        await using var reiniciado = CrearProvider(reloj);
        await using var otroScope = reiniciado.CreateAsyncScope();
        var colector = otroScope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>();
        Assert.Equal(0, (await colector.RecolectarAsync()).LecturasGuardadas);
        reloj.Avanzar(TimeSpan.FromMinutes(1));
        Assert.Equal(4, (await colector.RecolectarAsync()).LecturasGuardadas);
        await using var context = database.CrearContexto();
        Assert.Equal(8, await context.LecturasPanel.CountAsync());
    }

    [MariaDbHistoricoFact]
    public async Task Ciclos_AdmitenMedicionesDistintasDentroDelMismoMinuto()
    {
        await PrepararAsync();
        var reloj = new RelojRecoleccion(Instante.AddSeconds(12).AddTicks(9876543));
        await using var provider = CrearProvider(reloj, intervaloSegundos: 1);
        await using var scope = provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>();
        Assert.Equal(4, (await servicio.RecolectarAsync()).LecturasGuardadas);
        Assert.Equal(0, (await servicio.RecolectarAsync()).LecturasGuardadas);
        reloj.Avanzar(TimeSpan.FromSeconds(1));
        Assert.Equal(4, (await servicio.RecolectarAsync()).LecturasGuardadas);
        await using var context = database.CrearContexto();
        var lecturas = await context.LecturasPanel.Where(l => l.PanelId == database.Panel.Id)
            .OrderBy(l => l.FechaHora).ToArrayAsync();
        Assert.Equal(2, lecturas.Length);
        Assert.Equal(TimeSpan.FromSeconds(1), lecturas[1].FechaHora - lecturas[0].FechaHora);
        Assert.All(lecturas, lectura => Assert.Equal(database.Panel.NodoId, lectura.NodoId));
    }

    [MariaDbHistoricoFact]
    public async Task Persistencia_RechazaLaMismaMedicionConcurrenteSinSobrescribirla()
    {
        await PrepararAsync();
        await using var primero = database.CrearContexto();
        await using var segundo = database.CrearContexto();
        var panel1 = await primero.Paneles.SingleAsync(p => p.Id == database.Panel.Id);
        var panel2 = await segundo.Paneles.SingleAsync(p => p.Id == database.Panel.Id);
        primero.LecturasPanel.Add(new LecturaPanel(panel1, Instante, 100, EstadoSaludLectura.Sano));
        segundo.LecturasPanel.Add(new LecturaPanel(panel2, Instante, 200, EstadoSaludLectura.Fallado));
        static async Task<bool> GuardarAsync(SolarUY.Infrastructure.Persistence.ApplicationDbContext context)
        {
            try { await context.SaveChangesAsync(); return true; }
            catch (DbUpdateException exception) when (exception.InnerException is MySqlConnector.MySqlException { Number: 1062 })
            { return false; }
        }
        var resultados = await Task.WhenAll(GuardarAsync(primero), GuardarAsync(segundo));
        Assert.Single(resultados.Where(resultado => resultado));
        await using var consulta = database.CrearContexto();
        Assert.Single(await consulta.LecturasPanel.ToListAsync());
    }

    [MariaDbHistoricoFact]
    public async Task Historico_ConservaNodoYSaludAnteCambiosPosterioresDelPanel()
    {
        await PrepararAsync();
        await using var provider = CrearProvider(new RelojRecoleccion(Instante));
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync();
        await using var context = database.CrearContexto();
        var parque = await context.ParquesSolares.SingleAsync(p => p.Id == database.Parque.Id);
        var nuevoNodo = new Nodo(parque, "Nodo posterior", "Sur");
        context.Nodos.Add(nuevoNodo);
        await context.SaveChangesAsync();
        await context.Paneles.Where(p => p.Id == database.Panel.Id).ExecuteUpdateAsync(cambios => cambios
            .SetProperty(p => p.NodoId, nuevoNodo.Id).SetProperty(p => p.Estado, EstadoPanel.Falla));
        var lectura = await context.LecturasPanel.Include(l => l.Panel).SingleAsync(l => l.PanelId == database.Panel.Id);
        Assert.Equal(database.Panel.NodoId, lectura.NodoId);
        Assert.Equal(nuevoNodo.Id, lectura.Panel.NodoId);
        Assert.Equal(EstadoSaludLectura.Sano, lectura.EstadoSalud);
        Assert.Equal(EstadoPanel.Falla, lectura.Panel.Estado);
    }

    [MariaDbHistoricoFact]
    public async Task Inventario_RespetaEstadoInstalacionFallasYPanelesNuevos()
    {
        await PrepararAsync();
        Guid inactivoId, falladoId;
        await using (var context = database.CrearContexto())
        {
            var nodo = await context.Nodos.FirstAsync();
            var inactivo = new Panel(nodo, "Inactivo", "Marca", "Modelo", 400, Instante.AddDays(-1), EstadoPanel.Inactivo);
            var fallado = new Panel(nodo, "Fallado", "Marca", "Modelo", 500, Instante.AddDays(-1), EstadoPanel.Falla);
            context.Paneles.AddRange(inactivo, fallado, new Panel(nodo, "Futuro", "Marca", "Modelo", 400, Instante.AddDays(1), EstadoPanel.Activo));
            await context.SaveChangesAsync();
            inactivoId = inactivo.Id;
            falladoId = fallado.Id;
        }
        await using var provider = CrearProvider(new RelojRecoleccion(Instante));
        await using var scope = provider.CreateAsyncScope();
        var colector = scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>();
        Assert.Equal(6, (await colector.RecolectarAsync()).LecturasGuardadas);
        await using (var context = database.CrearContexto())
        {
            var inactivo = await context.LecturasPanel.SingleAsync(lectura => lectura.PanelId == inactivoId);
            var fallado = await context.LecturasPanel.SingleAsync(lectura => lectura.PanelId == falladoId);
            Assert.Equal(0, inactivo.PotenciaGeneradaW);
            Assert.Equal(0, fallado.PotenciaGeneradaW);
            Assert.Equal(EstadoSaludLectura.Fallado, fallado.EstadoSalud);
            var nodo = await context.Nodos.FirstAsync();
            context.Paneles.Add(HistoricoDatabase.CrearPanel(nodo, "Nuevo"));
            await context.SaveChangesAsync();
        }
        Assert.Equal(1, (await colector.RecolectarAsync()).LecturasGuardadas);
    }

    [MariaDbHistoricoFact]
    public async Task DosRecolectores_SoloElPropietarioDelBloqueoLeeYGuarda()
    {
        await PrepararAsync();
        var reloj = new RelojRecoleccion(Instante);
        var iniciado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continuar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var real = new InversorSimulador(new ConfigurationBuilder().Build(), reloj);
        var detenido = new InversorIntervenido(real, async (paneles, instante, ct) =>
        {
            iniciado.TrySetResult();
            await continuar.Task.WaitAsync(ct);
            return await real.ObtenerMedicionesRegistradasAsync(paneles, instante, ct);
        });
        await using var primero = CrearProvider(reloj, detenido);
        await using var segundo = CrearProvider(reloj);
        await using var scope1 = primero.CreateAsyncScope();
        await using var scope2 = segundo.CreateAsyncScope();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ciclo = scope1.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync(limite.Token);
        await iniciado.Task.WaitAsync(limite.Token);
        try
        {
            var otro = await scope2.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync(limite.Token);
            Assert.False(otro.AdquirioExclusion);
            Assert.Equal(0, otro.LecturasGuardadas);
        }
        finally { continuar.TrySetResult(); }
        Assert.Equal(4, (await ciclo).LecturasGuardadas);
        Assert.Equal(0, (await scope2.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync()).LecturasGuardadas);
    }

    [MariaDbHistoricoTheory]
    [InlineData("fallo")]
    [InlineData("faltante")]
    [InlineData("duplicado")]
    [InlineData("ajeno")]
    [InlineData("invalido")]
    [InlineData("cancelado")]
    public async Task Fallos_NoPersistenParcialmenteYSiempreLiberanSesion(string fallo)
    {
        await PrepararAsync();
        var reloj = new RelojRecoleccion(Instante);
        var iniciado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var real = new InversorSimulador(new ConfigurationBuilder().Build(), reloj);
        var intervenido = new InversorIntervenido(real, async (paneles, instante, ct) =>
        {
            iniciado.TrySetResult();
            if (fallo == "fallo") throw new InvalidOperationException("Fallo de fuente controlado.");
            if (fallo == "cancelado") await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            var lecturas = (await real.ObtenerMedicionesRegistradasAsync(paneles, instante, ct)).ToArray();
            if (fallo == "faltante") return lecturas.Skip(1).ToArray();
            if (fallo == "duplicado") lecturas[1] = lecturas[0];
            if (fallo == "ajeno") lecturas[0] = lecturas[0] with { PanelId = Guid.NewGuid() };
            if (fallo == "invalido") lecturas[^1] = lecturas[^1] with { PotenciaGeneradaW = -1 };
            return lecturas;
        });
        await using var provider = CrearProvider(reloj, intervenido);
        await using var scope = provider.CreateAsyncScope();
        using var cancelacion = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ciclo = scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync(cancelacion.Token);
        await iniciado.Task.WaitAsync(cancelacion.Token);
        if (fallo == "cancelado") cancelacion.Cancel();
        if (fallo == "cancelado") await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ciclo);
        else if (fallo == "invalido") await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await ciclo);
        else await Assert.ThrowsAsync<InvalidOperationException>(async () => await ciclo);
        await using var context = database.CrearContexto();
        Assert.Equal(0, await context.LecturasPanel.CountAsync());
        await using var nuevo = CrearProvider(reloj);
        await using var nuevoScope = nuevo.CreateAsyncScope();
        Assert.Equal(4, (await nuevoScope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync()).LecturasGuardadas);
    }

    [MariaDbHistoricoFact]
    public async Task Workers_DosHostsRecolectanPeriodicamenteSinDuplicarYSeDetienen()
    {
        await PrepararAsync();
        var reloj = new RelojRecoleccion(Instante);
        using var host1 = CrearHost(reloj);
        using var host2 = CrearHost(reloj);
        await Task.WhenAll(host1.StartAsync(), host2.StartAsync());
        try
        {
            await EsperarCantidadAsync(4);
            reloj.Avanzar(TimeSpan.FromMinutes(1));
            await EsperarCantidadAsync(8);
        }
        finally { await Task.WhenAll(host1.StopAsync(), host2.StopAsync()); }
        reloj.Avanzar(TimeSpan.FromMinutes(1));
        await Task.Delay(1200);
        await using var context = database.CrearContexto();
        Assert.Equal(8, await context.LecturasPanel.CountAsync());
    }

    private async Task EsperarCantidadAsync(int cantidad)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var context = database.CrearContexto();
            if (await context.LecturasPanel.CountAsync(limite.Token) == cantidad) return;
            await Task.Delay(100, limite.Token);
        }
    }

    [MariaDbHistoricoFact]
    public async Task Worker_SeRecuperaDelErrorDeFuenteEnElSiguienteCiclo()
    {
        await PrepararAsync();
        var reloj = new RelojRecoleccion(Instante);
        var real = new InversorSimulador(new ConfigurationBuilder().Build(), reloj);
        var intentos = 0;
        var fuente = new InversorIntervenido(real, (paneles, instante, ct) =>
        {
            if (Interlocked.Increment(ref intentos) == 1) throw new InvalidOperationException("Error temporal controlado.");
            return real.ObtenerMedicionesRegistradasAsync(paneles, instante, ct);
        });
        using var host = CrearHost(reloj, fuente);
        await host.StartAsync();
        try { await EsperarCantidadAsync(4); }
        finally { await host.StopAsync(); }
        Assert.True(intentos >= 2);
    }

    [MariaDbHistoricoFact]
    public async Task Worker_PuertoOcupadoNoRecolectaNiFallaDuranteElCierre()
    {
        await PrepararAsync();
        using var puertoOcupado = new TcpListener(IPAddress.Loopback, 0);
        puertoOcupado.ExclusiveAddressUse = true;
        puertoOcupado.Start();
        var puerto = ((IPEndPoint)puertoOcupado.LocalEndpoint).Port;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        // Evitar que permisos del EventLog de Windows oculten el error de puerto que se comprueba.
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, puerto));
        Registrar(builder.Services, new RelojRecoleccion(Instante));
        builder.Services.Configure<RecoleccionLecturasOptions>(options => options.IntervaloSegundos = 1);
        builder.Services.AddHostedService<RecoleccionLecturasWorker>();
        builder.Services.AddHostedService<InicioDemoradoService>();
        var host = builder.Build();
        var worker = host.Services.GetServices<IHostedService>().OfType<RecoleccionLecturasWorker>().Single();
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<IOException>(() => host.StartAsync(limite.Token));
        }
        finally { await host.DisposeAsync(); }
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        await using var context = database.CrearContexto();
        Assert.Equal(0, await context.LecturasPanel.CountAsync());
    }

    [MariaDbHistoricoFact]
    public async Task Worker_DeshabilitadoNoGeneraLecturas()
    {
        await PrepararAsync();
        using var host = CrearHost(new RelojRecoleccion(Instante), habilitada: false);
        await host.StartAsync();
        try
        {
            await Task.Delay(1200);
            await using var context = database.CrearContexto();
            Assert.Equal(0, await context.LecturasPanel.CountAsync());
        }
        finally { await host.StopAsync(); }
    }

    [MariaDbHistoricoFact]
    public async Task Fuente_AdmiteFallasConfiguradasPorSerieYGuid()
    {
        await PrepararAsync();
        var reloj = new RelojRecoleccion(Instante);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["InversorSimulador:PanelesConFalla"] = $"{database.Panel.NumeroSerie},{database.SegundoPanel.Id:D}"
        }).Build();
        await using var provider = CrearProvider(reloj, new InversorSimulador(configuration, reloj));
        await using var scope = provider.CreateAsyncScope();
        Assert.Equal(4, (await scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync()).LecturasGuardadas);
        await using var context = database.CrearContexto();
        var falladas = await context.LecturasPanel.Where(lectura => lectura.EstadoSalud == EstadoSaludLectura.Fallado).ToArrayAsync();
        Assert.Equal(2, falladas.Length);
        Assert.All(falladas, lectura => Assert.Equal(0m, lectura.PotenciaGeneradaW));
    }

    private static ServiceProvider CrearProvider(RelojRecoleccion reloj, IInversorClient? inversor = null,
        int intervaloSegundos = 60)
    {
        var services = new ServiceCollection();
        Registrar(services, reloj, inversor);
        services.AddSingleton(new ConfiguracionRecoleccion(intervaloSegundos));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IHost CrearHost(RelojRecoleccion reloj, IInversorClient? inversor = null, bool habilitada = true) => new HostBuilder().ConfigureServices(services =>
    {
        Registrar(services, reloj, inversor);
        services.AddLogging();
        services.Configure<RecoleccionLecturasOptions>(options => { options.IntervaloSegundos = 1; options.Habilitada = habilitada; });
        services.AddHostedService<RecoleccionLecturasWorker>();
    }).Build();

    private static void Registrar(IServiceCollection services, RelojRecoleccion reloj, IInversorClient? inversor = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)
        }).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSolarInfrastructure(configuration);
        services.AddSingleton<TimeProvider>(reloj);
        services.AddScoped<IRecoleccionLecturasService, RecoleccionLecturasService>();
        services.AddSingleton(new ConfiguracionRecoleccion(60));
        if (inversor is not null) services.AddSingleton(inversor);
    }

    // Deja transcurrir un ciclo de planificación antes de que Kestrel intente abrir el puerto.
    private sealed class InicioDemoradoService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.Delay(200, cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RelojRecoleccion(DateTime instante) : TimeProvider
    {
        private long _ticks = instante.Ticks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        public void Avanzar(TimeSpan periodo) => Interlocked.Add(ref _ticks, periodo.Ticks);
    }

    private sealed class InversorIntervenido(IInversorClient original,
        Func<IReadOnlyList<PanelMedicionDto>, DateTime, CancellationToken, Task<IReadOnlyList<MedicionPanelDto>>> medir) : IInversorClient
    {
        public Task<IReadOnlyList<MedicionPanelDto>> ObtenerMedicionesRegistradasAsync(IReadOnlyList<PanelMedicionDto> paneles, DateTime instanteUtc, CancellationToken cancellationToken = default) => medir(paneles, instanteUtc, cancellationToken);
        public Task<IReadOnlyList<string>> ListarNodosAsync(CancellationToken cancellationToken = default) => original.ListarNodosAsync(cancellationToken);
        public Task<IReadOnlyList<PanelInversorEstadoDto>> ObtenerEstadoPanelesAsync(string nodoId, CancellationToken cancellationToken = default) => original.ObtenerEstadoPanelesAsync(nodoId, cancellationToken);
        public Task<decimal> ObtenerPotenciaGeneradaAsync(string nodoId, CancellationToken cancellationToken = default) => original.ObtenerPotenciaGeneradaAsync(nodoId, cancellationToken);
        public Task ActivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default) => original.ActivarPanelAsync(nodoId, panelId, cancellationToken);
        public Task DesactivarPanelAsync(string nodoId, string panelId, CancellationToken cancellationToken = default) => original.DesactivarPanelAsync(nodoId, panelId, cancellationToken);
        public Task ActivarVolcadoRedAsync(CancellationToken cancellationToken = default) => original.ActivarVolcadoRedAsync(cancellationToken);
        public Task DesactivarVolcadoRedAsync(CancellationToken cancellationToken = default) => original.DesactivarVolcadoRedAsync(cancellationToken);
        public Task<bool> ObtenerEstadoVolcadoRedAsync(CancellationToken cancellationToken = default) => original.ObtenerEstadoVolcadoRedAsync(cancellationToken);
    }
}
