using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Presentation.BackgroundServices;
using Xunit.Abstractions;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class RecoleccionProcesosIntegrationTests(HistoricoDatabase database, ITestOutputHelper output)
    : IClassFixture<HistoricoDatabase>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("no-numerico")]
    [InlineData("2147483648")]
    [InlineData("2147483647")]
    public async Task Proceso_RechazaIntervaloInvalidoAlArrancar(string? intervalo)
    {
        var salida = new ConcurrentQueue<string>();
        using var proceso = Iniciar(salida, intervalo, habilitada: false);
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await proceso.WaitForExitAsync(limite.Token);
            // Esperar también el drenaje de stdout/stderr antes de comprobar el diagnóstico.
            proceso.WaitForExit();
            Assert.NotEqual(0, proceso.ExitCode);
            Assert.Contains("IntervaloSegundos", string.Join(Environment.NewLine, salida));
        }
        finally
        {
            if (!proceso.HasExited) { proceso.Kill(entireProcessTree: true); await proceso.WaitForExitAsync(); }
        }
    }

    [Theory]
    [InlineData("301")]
    [InlineData("4294967")]
    public async Task Proceso_AdmiteIntervalosValidosSuperioresACincoMinutos(string intervalo)
    {
        var salida = new ConcurrentQueue<string>();
        using var proceso = Iniciar(salida, intervalo, habilitada: false);
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (!salida.Any(linea => linea.Contains("Now listening on")))
            {
                Assert.False(proceso.HasExited, string.Join(Environment.NewLine, salida));
                await Task.Delay(100, limite.Token);
            }
        }
        finally
        {
            if (!proceso.HasExited) { proceso.Kill(entireProcessTree: true); await proceso.WaitForExitAsync(); }
        }
    }

    [MariaDbHistoricoFact]
    public async Task Proceso_RegistraFalloDeGuardadoYSeRecuperaEnElSiguienteCiclo()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER req010_fallo_persistencia BEFORE INSERT ON LecturasPanel
            FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Fallo transitorio de prueba req010'
            """);
        var salida = new ConcurrentQueue<string>();
        Process? proceso = null;
        try
        {
            proceso = Iniciar(salida);
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (!salida.Any(linea => linea.Contains("No se completó la recolección")))
            {
                Assert.False(proceso.HasExited, string.Join(Environment.NewLine, salida));
                await Task.Delay(100, limite.Token);
            }
            Assert.Equal(0, await context.LecturasPanel.CountAsync());
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER req010_fallo_persistencia");
            await EsperarCantidadAsync(4, proceso, salida);
        }
        finally
        {
            if (proceso is not null)
            {
                if (!proceso.HasExited) { proceso.Kill(entireProcessTree: true); await proceso.WaitForExitAsync(); }
                proceso.Dispose();
            }
            await context.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS req010_fallo_persistencia");
        }
    }

    [MariaDbHistoricoFact]
    public async Task DosProcesos_RecolectanSinDuplicarYContinuanTrasTerminarElPrimero()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        var salida = new ConcurrentQueue<string>();
        using var primero = Iniciar(salida);
        Process? segundo = null;
        try
        {
            await EsperarCantidadAsync(4, primero, salida);
            segundo = Iniciar(salida);
            await EsperarCantidadAsync(12, segundo, salida);
            Assert.False(primero.HasExited, string.Join(Environment.NewLine, salida));
            primero.Kill(entireProcessTree: true);
            await primero.WaitForExitAsync();
            var antes = await context.LecturasPanel.CountAsync();
            var recuperacion = Stopwatch.StartNew();
            await EsperarCantidadAsync(antes + 8, segundo, salida);
            output.WriteLine($"Continuidad tras terminar el primer proceso: {recuperacion.Elapsed.TotalMilliseconds:F0} ms para dos lotes nuevos con N=1 s.");
            Assert.False(await context.LecturasPanel.GroupBy(l => new { l.PanelId, l.FechaHora })
                .AnyAsync(grupo => grupo.Count() > 1));
            Assert.All(await context.LecturasPanel.ToArrayAsync(), l => Assert.NotEqual(Guid.Empty, l.NodoId));
        }
        finally
        {
            if (!primero.HasExited) { primero.Kill(entireProcessTree: true); await primero.WaitForExitAsync(); }
            if (segundo is not null)
            {
                if (!segundo.HasExited) { segundo.Kill(entireProcessTree: true); await segundo.WaitForExitAsync(); }
                segundo.Dispose();
            }
        }
    }

    [MariaDbHistoricoFact]
    public async Task ConexionPerdida_NoPermiteGuardarYOtraSesionPuedeRetomar()
    {
        await database.ReiniciarAsync();
        await using var context = database.CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        await using var provider = CrearProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRecoleccionLecturasRepository>();
        await using var sesion = await repository.IntentarAbrirSesionAsync();
        Assert.NotNull(sesion);
        using var conexion = new MySqlConnection(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion));
        await conexion.OpenAsync();
        using var propietario = conexion.CreateCommand();
        propietario.CommandText = "SELECT IS_USED_LOCK(CONCAT('solaruy:lecturas:', LEFT(SHA2(DATABASE(),256),40)))";
        var id = Convert.ToInt64(await propietario.ExecuteScalarAsync());
        Assert.True(id > 0);
        using var terminar = conexion.CreateCommand();
        terminar.CommandText = $"KILL CONNECTION {id}";
        await terminar.ExecuteNonQueryAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => sesion.GuardarAsync([]));
        var recuperacion = Stopwatch.StartNew();
        await using var siguiente = await repository.IntentarAbrirSesionAsync();
        output.WriteLine($"Nueva sesión tras KILL CONNECTION: {recuperacion.Elapsed.TotalMilliseconds:F0} ms.");
        Assert.NotNull(siguiente);
        Assert.Empty(await context.LecturasPanel.ToArrayAsync());
    }

    private static ServiceProvider CrearProvider()
    {
        var services = new ServiceCollection();
        var configuracion = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)
            }).Build();
        SolarUY.Infrastructure.DependencyInjection.AddSolarInfrastructure(services, configuracion);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static Process Iniciar(ConcurrentQueue<string> salida, string? intervalo = "1", bool habilitada = true)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "req010-content-root")).FullName
        };
        inicio.ArgumentList.Add(typeof(RecoleccionLecturasWorker).Assembly.Location);
        inicio.Environment["ConnectionStrings__SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)
            ?? "Server=127.0.0.1;Database=solaruy_req010_configuracion;User ID=prueba";
        if (intervalo is null) inicio.Environment.Remove("RecoleccionLecturas__IntervaloSegundos");
        else inicio.Environment["RecoleccionLecturas__IntervaloSegundos"] = intervalo;
        inicio.Environment["RecoleccionLecturas__Habilitada"] = habilitada.ToString();
        inicio.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        inicio.Environment["Authentication__OpenIdConnect__Authority"] = "http://localhost:8080/realms/solaruy";
        inicio.Environment["Authentication__OpenIdConnect__ClientId"] = "solaruy-web";
        inicio.Environment["Logging__EventLog__LogLevel__Default"] = "None";
        var proceso = new Process { StartInfo = inicio };
        proceso.OutputDataReceived += (_, args) => { if (args.Data is not null) salida.Enqueue(args.Data); };
        proceso.ErrorDataReceived += (_, args) => { if (args.Data is not null) salida.Enqueue(args.Data); };
        proceso.Start();
        proceso.BeginOutputReadLine();
        proceso.BeginErrorReadLine();
        return proceso;
    }

    private async Task EsperarCantidadAsync(int cantidad, Process proceso, ConcurrentQueue<string> salida)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!limite.IsCancellationRequested)
        {
            Assert.False(proceso.HasExited, string.Join(Environment.NewLine, salida));
            await using var context = database.CrearContexto();
            if (await context.LecturasPanel.CountAsync(limite.Token) >= cantidad) return;
            await Task.Delay(100, limite.Token);
        }
        Assert.Fail(string.Join(Environment.NewLine, salida));
    }
}
