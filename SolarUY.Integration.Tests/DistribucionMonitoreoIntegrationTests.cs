using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Monitoreo;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Infrastructure;
using SolarUY.Presentation.Monitoreo;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class DistribucionMonitoreoIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task AvisoConfirmado_LlegaACadaInstanciaYSeRecuperaSinPublicadorActivo()
    {
        await PrepararAsync();
        var a = Guid.NewGuid().ToString(); var b = Guid.NewGuid().ToString();
        await using var provider = CrearProvider(a);
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDistribucionMonitoreoRepository>();
        await VaciarAsync(repository, a); await VaciarAsync(repository, b);
        var resultado = await scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync();
        Assert.Equal(4, resultado.LecturasGuardadas);
        var avisoA = Assert.Single(await repository.LeerPendientesAsync(a, 100, default));
        var avisoB = Assert.Single(await repository.LeerPendientesAsync(b, 100, default));
        Assert.Equal(avisoA.Secuencia, avisoB.Secuencia);
        Assert.Equal(4, avisoA.Lecturas.Count);
        await repository.ConfirmarAsync(a, avisoA.Secuencia, default);
        Assert.Empty(await repository.LeerPendientesAsync(a, 100, default));
        await using var reiniciado = CrearProvider(b);
        await using var nuevaSesion = reiniciado.CreateAsyncScope();
        var nuevo = nuevaSesion.ServiceProvider.GetRequiredService<IDistribucionMonitoreoRepository>();
        Assert.Equal(avisoB.Secuencia, Assert.Single(await nuevo.LeerPendientesAsync(b, 100, default)).Secuencia);
        await nuevo.ConfirmarAsync(b, avisoB.Secuencia, default);
        Assert.Empty(await nuevo.LeerPendientesAsync(b, 100, default));
    }

    [MariaDbHistoricoFact]
    public async Task FalloDelAviso_RevierteLecturasYSecuenciaEnLaMismaTransaccion()
    {
        await PrepararAsync();
        await using var context = database.CrearContexto();
        var antes = await LeerSecuenciaAsync();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER req14_aviso_fallido BEFORE INSERT ON AvisosMonitoreo
            FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Fallo controlado de outbox'
            """);
        try
        {
            await using var provider = CrearProvider(Guid.NewGuid().ToString());
            await using var scope = provider.CreateAsyncScope();
            await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() => scope.ServiceProvider
                .GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync());
            Assert.Equal(0, await context.LecturasPanel.CountAsync());
            Assert.Equal(antes, await LeerSecuenciaAsync());
        }
        finally { await context.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS req14_aviso_fallido"); }
    }

    [MariaDbHistoricoFact]
    public async Task ReceptorFallido_ReintentaElAvisoSinAvanzarCursorNiRecolectarOtraVez()
    {
        await PrepararAsync();
        var instancia = Guid.NewGuid().ToString();
        var entrega = new EntregaControlada();
        await using var provider = CrearProvider(instancia, entrega);
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDistribucionMonitoreoRepository>();
        await VaciarAsync(repository, instancia);
        await scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasService>().RecolectarAsync();
        using (var worker = ActivatorUtilities.CreateInstance<DistribucionMonitoreoWorker>(provider))
        {
            await worker.StartAsync(default);
            await entrega.Intentada.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await worker.StopAsync(default);
        }
        Assert.Single(await repository.LeerPendientesAsync(instancia, 100, default));
        entrega.Fallar = false;
        using (var worker = ActivatorUtilities.CreateInstance<DistribucionMonitoreoWorker>(provider))
        {
            await worker.StartAsync(default);
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while ((await repository.LeerPendientesAsync(instancia, 100, limite.Token)).Count > 0)
                await Task.Delay(50, limite.Token);
            await worker.StopAsync(default);
        }
        await using var context = database.CrearContexto();
        Assert.Equal(4, await context.LecturasPanel.CountAsync());
        Assert.True(entrega.Cantidad >= 2);
    }

    private async Task PrepararAsync()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto(); await context.LecturasPanel.ExecuteDeleteAsync();
    }

    private async Task<long> LeerSecuenciaAsync()
    {
        await using var context = database.CrearContexto(); await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT Valor FROM SecuenciaMonitoreo WHERE Id = 1";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task VaciarAsync(IDistribucionMonitoreoRepository repository, string id)
    {
        IReadOnlyList<AvisoMonitoreoDto> avisos;
        while ((avisos = await repository.LeerPendientesAsync(id, 1000, default)).Count > 0)
            await repository.ConfirmarAsync(id, avisos[^1].Secuencia, default);
    }

    private static ServiceProvider CrearProvider(string id, INotificadorLecturasConfirmadas? entrega = null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion) }).Build();
        services.AddSolarInfrastructure(config);
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton(new ConfiguracionRecoleccion(60));
        services.AddScoped<IRecoleccionLecturasService, RecoleccionLecturasService>();
        services.Configure<DistribucionMonitoreoOptions>(o => { o.InstanciaId = id; o.IntervaloSegundos = 1; });
        if (entrega is not null) services.AddSingleton(entrega);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class EntregaControlada : INotificadorLecturasConfirmadas
    {
        public bool Fallar { get; set; } = true;
        public int Cantidad;
        public TaskCompletionSource Intentada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task NotificarAsync(IReadOnlyList<LecturaConfirmadaDto> lecturas, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Cantidad); Intentada.TrySetResult();
            return Fallar ? Task.FromException(new InvalidOperationException("Entrega interrumpida.")) : Task.CompletedTask;
        }
    }
}
