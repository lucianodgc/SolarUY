using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Inversores;
using SolarUY.Domain.Enums;
using SolarUY.Infrastructure;
using SolarUY.Infrastructure.Inversores;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class InversorIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    private static readonly string[] NodoTres = ["00000000-0000-0000-0000-000000000003"];
    private static readonly string[] PrimerosNodos = ["00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002"];
    private static readonly string[] PanelesNodoTres =
    [
        "00000000-0000-0000-0000-000000000006", "00000000-0000-0000-0000-000000000007",
        "00000000-0000-0000-0000-000000000008", "00000000-0000-0000-0000-000000000009"
    ];

    [MariaDbHistoricoFact]
    public async Task Composicion_UsaLaTopologiaEntregadaPorElInventario()
    {
        await using var provider = CrearProvider(new RelojPrueba(12), predeterminado: true);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();

        var nodos = await service.ListarNodosAsync();
        Assert.Equal(NodoTres, nodos.Select(nodo => nodo.Id));
        var estado = await service.ConsultarAsync("00000000-0000-0000-0000-000000000003");

        Assert.Equal("Nodo existente", estado.NodoNombre);
        Assert.Equal("Parque existente", estado.ParqueNombre);
        Assert.Equal(PanelesNodoTres, estado.Paneles.Select(p => p.PanelId));
        Assert.All(estado.Paneles, p => Assert.True(p.EstaActivo));
        Assert.InRange(estado.PotenciaGeneradaW, 1440m, 1600m);
        Assert.True(estado.InversorEncendido);
        Assert.True(estado.PuedeOperar);
        Assert.False(estado.VolcadoRedActivo);
    }

    [MariaDbHistoricoTheory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(18)]
    [InlineData(23)]
    public async Task Consulta_GeneraPotenciaIndependientementeDeLaHora(int hora)
    {
        await using var provider = CrearProvider(new RelojPrueba(hora));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        var client = scope.ServiceProvider.GetRequiredService<IInversorClient>();

        var estado = await service.ConsultarAsync("00000000-0000-0000-0000-000000000001");

        Assert.All(estado.Paneles, p => Assert.InRange(p.PotenciaGeneradaW, 360m, 400m));
        Assert.InRange(estado.PotenciaGeneradaW, 720m, 800m);
        Assert.Equal(estado.Paneles.Sum(p => p.PotenciaGeneradaW), estado.PotenciaGeneradaW);
        Assert.InRange(await client.ObtenerPotenciaGeneradaAsync("00000000-0000-0000-0000-000000000001"), 720m, 800m);
    }

    [MariaDbHistoricoFact]
    public async Task Simulador_UsaLaPotenciaNominalDelInventarioSinTopologiaPredeterminada()
    {
        var simulador = new InversorSimulador(new ConfigurationBuilder().Build());
        Assert.Empty(await simulador.ListarNodosAsync());
        await simulador.SincronizarInventarioAsync(
        [
            new InventarioNodoInversorDto("nodo-db", "Nodo DB", "Parque DB", Guid.NewGuid(),
                [new InventarioPanelInversorDto("panel-db", "SERIE-DB", 200m, EstadoPanel.Activo)])
        ]);

        var panel = Assert.Single(await simulador.ObtenerEstadoPanelesAsync("nodo-db"));
        Assert.Equal("SERIE-DB", panel.NumeroSerie);
        Assert.InRange(panel.PotenciaGeneradaW, 180m, 200m);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => simulador.ObtenerEstadoPanelesAsync("00000000-0000-0000-0000-000000000003"));
    }

    [MariaDbHistoricoFact]
    public async Task OrdenesPanel_SonIdempotentesYAislanPanelYNodo()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        Assert.Equal(PrimerosNodos, (await service.ListarNodosAsync()).Select(nodo => nodo.Id));

        await service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000004", false);
        await service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000004", false);
        var estado = await service.ConsultarAsync("00000000-0000-0000-0000-000000000001");
        Assert.False(estado.Paneles.Single(p => p.PanelId == "00000000-0000-0000-0000-000000000004").EstaActivo);
        Assert.Equal(0m, estado.Paneles.Single(p => p.PanelId == "00000000-0000-0000-0000-000000000004").PotenciaGeneradaW);
        Assert.InRange(estado.Paneles.Single(p => p.PanelId == "00000000-0000-0000-0000-000000000005").PotenciaGeneradaW, 360m, 400m);
        Assert.InRange((await service.ConsultarAsync("00000000-0000-0000-0000-000000000002")).PotenciaGeneradaW, 360m, 400m);

        await service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000004", true);
        await service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000004", true);
        Assert.InRange((await service.ConsultarAsync("00000000-0000-0000-0000-000000000001")).PotenciaGeneradaW, 720m, 800m);
    }

    [MariaDbHistoricoFact]
    public async Task FallaConfigurada_NoGeneraNiDesapareceAlReactivar()
    {
        await using var provider = CrearProvider(new RelojPrueba(12), falla: "00000000-0000-0000-0000-000000000005");
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        await service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000005", false);
        await service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000005", true);

        var estado = await service.ConsultarAsync("00000000-0000-0000-0000-000000000001");
        var fallado = estado.Paneles.Single(p => p.PanelId == "00000000-0000-0000-0000-000000000005");
        Assert.True(fallado.EstaActivo);
        Assert.Equal(EstadoSaludPanel.Fallado, fallado.EstadoSalud);
        Assert.Equal(0m, fallado.PotenciaGeneradaW);
        Assert.InRange(estado.PotenciaGeneradaW, 360m, 400m);
    }

    [MariaDbHistoricoFact]
    public async Task Composicion_ComparteEstadoEntreAmbitosYSoloDuranteElProceso()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using (var scope = provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
            await service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000004", false);
            await service.EstablecerVolcadoRedAsync(true);
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
            var estado = await service.ConsultarAsync("00000000-0000-0000-0000-000000000001");
            Assert.True(estado.VolcadoRedActivo);
            Assert.InRange(estado.PotenciaGeneradaW, 360m, 400m);
            await service.EstablecerVolcadoRedAsync(false);
            Assert.False((await service.ConsultarAsync("00000000-0000-0000-0000-000000000001")).VolcadoRedActivo);
        }
        await using var nuevoProvider = CrearProvider(new RelojPrueba(12));
        await using var nuevoScope = nuevoProvider.CreateAsyncScope();
        var inicial = await nuevoScope.ServiceProvider.GetRequiredService<IInversorService>().ConsultarAsync("00000000-0000-0000-0000-000000000001");
        Assert.False(inicial.VolcadoRedActivo);
        Assert.InRange(inicial.PotenciaGeneradaW, 720m, 800m);
    }

    [MariaDbHistoricoTheory]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task RolesLectura_NoEjecutanOrdenesAunqueInvoquenElServicio(string rol)
    {
        await using var provider = CrearProvider(new RelojPrueba(12), rol: rol);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        Assert.NotEmpty(await service.ListarNodosAsync());
        Assert.False((await service.ConsultarAsync("00000000-0000-0000-0000-000000000001")).PuedeOperar);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000004", false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerVolcadoRedAsync(true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerInversorEncendidoAsync(false));
        Assert.InRange((await service.ConsultarAsync("00000000-0000-0000-0000-000000000001")).PotenciaGeneradaW, 720m, 800m);
        Assert.False((await service.ConsultarAsync("00000000-0000-0000-0000-000000000001")).VolcadoRedActivo);
        Assert.True((await service.ConsultarAsync("00000000-0000-0000-0000-000000000001")).InversorEncendido);
    }

    [MariaDbHistoricoTheory]
    [InlineData(false, "usuario", "Administrador")]
    [InlineData(true, "", "Administrador")]
    [InlineData(true, "usuario", "SinPermiso")]
    public async Task IdentidadSinPermiso_NoConsultaNiOpera(bool autenticado, string id, string rol)
    {
        await using var provider = CrearProvider(new RelojPrueba(12), rol: rol, autenticado: autenticado, id: id);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListarNodosAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConsultarAsync("00000000-0000-0000-0000-000000000001"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000004", false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerVolcadoRedAsync(true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerInversorEncendidoAsync(false));
    }

    [MariaDbHistoricoFact]
    public async Task IdentificadoresInvalidos_NoCambianElEstadoExistente()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConsultarAsync("inexistente"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "inexistente", false));
        Assert.InRange((await service.ConsultarAsync("00000000-0000-0000-0000-000000000001")).PotenciaGeneradaW, 720m, 800m);
    }

    [MariaDbHistoricoFact]
    public async Task Cancelacion_NoEjecutaUnaOrden()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.EstablecerPanelActivoAsync("00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000004", false, cancelacion.Token));
        Assert.InRange((await service.ConsultarAsync("00000000-0000-0000-0000-000000000001")).PotenciaGeneradaW, 720m, 800m);
    }

    [MariaDbHistoricoFact]
    public async Task EstadoGlobal_ApagarCeroPotenciaYPrenderRestauraGeneracion()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();

        await service.EstablecerInversorEncendidoAsync(false);
        var apagado = await service.ConsultarAsync("00000000-0000-0000-0000-000000000001");
        Assert.False(apagado.InversorEncendido);
        Assert.Equal(0m, apagado.PotenciaGeneradaW);
        Assert.All(apagado.Paneles, panel => Assert.Equal(0m, panel.PotenciaGeneradaW));

        await service.EstablecerInversorEncendidoAsync(true);
        var encendido = await service.ConsultarAsync("00000000-0000-0000-0000-000000000001");
        Assert.True(encendido.InversorEncendido);
        Assert.InRange(encendido.PotenciaGeneradaW, 720m, 800m);
    }

    private ServiceProvider CrearProvider(RelojPrueba reloj, bool predeterminado = false,
        string? falla = null, string rol = "Administrador", bool autenticado = true, string id = "usuario-prueba")
    {
        var valores = new Dictionary<string, string?>
        { ["ConnectionStrings:SolarUY"] = database.ConexionConfigurada };
        if (!predeterminado)
        {
            valores["InversorSimulador:Nodos:nodo-a"] = "panel-a,panel-b";
            valores["InversorSimulador:Nodos:nodo-b"] = "00000000-0000-0000-0000-000000000004";
        }
        valores["InversorSimulador:PanelesConFalla"] = falla;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSolarInfrastructure(configuration);
        services.RemoveAll<IInversorClient>();
        services.AddSingleton<IInversorClient, InversorSimulador>();
        services.AddSingleton<IInversorInventarioRepository>(predeterminado
            ? CrearInventarioPrueba(("00000000-0000-0000-0000-000000000003", "Nodo existente", "Parque existente", ["00000000-0000-0000-0000-000000000006", "00000000-0000-0000-0000-000000000007", "00000000-0000-0000-0000-000000000008", "00000000-0000-0000-0000-000000000009"]))
            : CrearInventarioPrueba(("00000000-0000-0000-0000-000000000001", "Nodo A", "Parque A", ["00000000-0000-0000-0000-000000000004", "00000000-0000-0000-0000-000000000005"]), ("00000000-0000-0000-0000-000000000002", "Nodo B", "Parque B", ["00000000-0000-0000-0000-000000000004"])));
        services.AddSingleton<TimeProvider>(reloj);
        services.AddSingleton<IUsuarioActual>(new UsuarioPrueba(autenticado, id, rol));
        services.AddScoped<IInversorService, InversorService>();
        services.AddScoped<IOrdenInversorService, OrdenInversorService>();
        services.AddSingleton(new OrdenInversorOptions());
        // Las mutaciones usan ahora la auditoría real de MariaDB aislada.
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static InventarioPrueba CrearInventarioPrueba(params (string Id, string Nombre, string Parque, string[] Paneles)[] valores)
        => new InventarioPrueba(valores.Select(nodo => new InventarioNodoInversorDto(nodo.Id, nodo.Nombre, nodo.Parque, Guid.NewGuid(),
            nodo.Paneles.Select(id => new InventarioPanelInversorDto(id, id, 400m, EstadoPanel.Activo)).ToArray())).ToArray());

    private sealed class InventarioPrueba(IReadOnlyList<InventarioNodoInversorDto> nodos) : IInversorInventarioRepository
    {
        public Task<IReadOnlyList<InventarioNodoInversorDto>> ListarAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(nodos);
        }
    }

    private sealed class RelojPrueba(int hora) : TimeProvider
    {
        public DateTimeOffset Instante { get; set; } = new(2026, 10, 22, hora, 0, 0, TimeSpan.Zero);
        public TimeZoneInfo Zona { get; set; } = TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => Instante;
        public override TimeZoneInfo LocalTimeZone => Zona;
    }

    private sealed class UsuarioPrueba(bool autenticado, string id, string rol) : IUsuarioActual
    {
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new UsuarioActualDto(autenticado, id, new[] { rol }, id));
        }
    }
}
