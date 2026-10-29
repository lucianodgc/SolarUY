using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Inversores;
using SolarUY.Domain.Enums;
using SolarUY.Infrastructure;
using SolarUY.Infrastructure.Inversores;

namespace SolarUY.Integration.Tests;

public sealed class InversorIntegrationTests
{
    [Fact]
    public async Task Composicion_UsaLaTopologiaEntregadaPorElInventario()
    {
        await using var provider = CrearProvider(new RelojPrueba(12), predeterminado: true);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();

        var nodos = await service.ListarNodosAsync();
        Assert.Equal(new[] { "nodo-1" }, nodos.Select(nodo => nodo.Id));
        var estado = await service.ConsultarAsync("nodo-1");

        Assert.Equal("Nodo existente", estado.NodoNombre);
        Assert.Equal("Parque existente", estado.ParqueNombre);
        Assert.Equal(new[] { "panel-1", "panel-2", "panel-3", "panel-4" }, estado.Paneles.Select(p => p.PanelId));
        Assert.All(estado.Paneles, p => Assert.True(p.EstaActivo));
        Assert.InRange(estado.PotenciaGeneradaW, 1440m, 1600m);
        Assert.True(estado.InversorEncendido);
        Assert.True(estado.PuedeOperar);
        Assert.False(estado.VolcadoRedActivo);
    }

    [Theory]
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

        var estado = await service.ConsultarAsync("nodo-a");

        Assert.All(estado.Paneles, p => Assert.InRange(p.PotenciaGeneradaW, 360m, 400m));
        Assert.InRange(estado.PotenciaGeneradaW, 720m, 800m);
        Assert.Equal(estado.Paneles.Sum(p => p.PotenciaGeneradaW), estado.PotenciaGeneradaW);
        Assert.InRange(await client.ObtenerPotenciaGeneradaAsync("nodo-a"), 720m, 800m);
    }

    [Fact]
    public async Task Simulador_UsaLaPotenciaNominalDelInventarioSinTopologiaPredeterminada()
    {
        var simulador = new InversorSimulador(new ConfigurationBuilder().Build());
        Assert.Empty(await simulador.ListarNodosAsync());
        await simulador.SincronizarInventarioAsync(
        [
            new InventarioNodoInversorDto("nodo-db", "Nodo DB", "Parque DB",
                [new InventarioPanelInversorDto("panel-db", "SERIE-DB", 200m, EstadoPanel.Activo)])
        ]);

        var panel = Assert.Single(await simulador.ObtenerEstadoPanelesAsync("nodo-db"));
        Assert.Equal("SERIE-DB", panel.NumeroSerie);
        Assert.InRange(panel.PotenciaGeneradaW, 180m, 200m);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => simulador.ObtenerEstadoPanelesAsync("nodo-1"));
    }

    [Fact]
    public async Task OrdenesPanel_SonIdempotentesYAislanPanelYNodo()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        Assert.Equal(new[] { "nodo-a", "nodo-b" }, (await service.ListarNodosAsync()).Select(nodo => nodo.Id));

        await service.EstablecerPanelActivoAsync("nodo-a", "panel-a", false);
        await service.EstablecerPanelActivoAsync("nodo-a", "panel-a", false);
        var estado = await service.ConsultarAsync("nodo-a");
        Assert.False(estado.Paneles.Single(p => p.PanelId == "panel-a").EstaActivo);
        Assert.Equal(0m, estado.Paneles.Single(p => p.PanelId == "panel-a").PotenciaGeneradaW);
        Assert.InRange(estado.Paneles.Single(p => p.PanelId == "panel-b").PotenciaGeneradaW, 360m, 400m);
        Assert.InRange((await service.ConsultarAsync("nodo-b")).PotenciaGeneradaW, 360m, 400m);

        await service.EstablecerPanelActivoAsync("nodo-a", "panel-a", true);
        await service.EstablecerPanelActivoAsync("nodo-a", "panel-a", true);
        Assert.InRange((await service.ConsultarAsync("nodo-a")).PotenciaGeneradaW, 720m, 800m);
    }

    [Fact]
    public async Task FallaConfigurada_NoGeneraNiDesapareceAlReactivar()
    {
        await using var provider = CrearProvider(new RelojPrueba(12), falla: "panel-b");
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        await service.EstablecerPanelActivoAsync("nodo-a", "panel-b", false);
        await service.EstablecerPanelActivoAsync("nodo-a", "panel-b", true);

        var estado = await service.ConsultarAsync("nodo-a");
        var fallado = estado.Paneles.Single(p => p.PanelId == "panel-b");
        Assert.True(fallado.EstaActivo);
        Assert.Equal(EstadoSaludPanel.Fallado, fallado.EstadoSalud);
        Assert.Equal(0m, fallado.PotenciaGeneradaW);
        Assert.InRange(estado.PotenciaGeneradaW, 360m, 400m);
    }

    [Fact]
    public async Task Composicion_ComparteEstadoEntreAmbitosYSoloDuranteElProceso()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using (var scope = provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
            await service.EstablecerPanelActivoAsync("nodo-a", "panel-a", false);
            await service.EstablecerVolcadoRedAsync(true);
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
            var estado = await service.ConsultarAsync("nodo-a");
            Assert.True(estado.VolcadoRedActivo);
            Assert.InRange(estado.PotenciaGeneradaW, 360m, 400m);
            await service.EstablecerVolcadoRedAsync(false);
            Assert.False((await service.ConsultarAsync("nodo-a")).VolcadoRedActivo);
        }
        await using var nuevoProvider = CrearProvider(new RelojPrueba(12));
        await using var nuevoScope = nuevoProvider.CreateAsyncScope();
        var inicial = await nuevoScope.ServiceProvider.GetRequiredService<IInversorService>().ConsultarAsync("nodo-a");
        Assert.False(inicial.VolcadoRedActivo);
        Assert.InRange(inicial.PotenciaGeneradaW, 720m, 800m);
    }

    [Theory]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task RolesLectura_NoEjecutanOrdenesAunqueInvoquenElServicio(string rol)
    {
        await using var provider = CrearProvider(new RelojPrueba(12), rol: rol);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        Assert.NotEmpty(await service.ListarNodosAsync());
        Assert.False((await service.ConsultarAsync("nodo-a")).PuedeOperar);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerPanelActivoAsync("nodo-a", "panel-a", false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerVolcadoRedAsync(true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerInversorEncendidoAsync(false));
        Assert.InRange((await service.ConsultarAsync("nodo-a")).PotenciaGeneradaW, 720m, 800m);
        Assert.False((await service.ConsultarAsync("nodo-a")).VolcadoRedActivo);
        Assert.True((await service.ConsultarAsync("nodo-a")).InversorEncendido);
    }

    [Theory]
    [InlineData(false, "usuario", "Administrador")]
    [InlineData(true, "", "Administrador")]
    [InlineData(true, "usuario", "SinPermiso")]
    public async Task IdentidadSinPermiso_NoConsultaNiOpera(bool autenticado, string id, string rol)
    {
        await using var provider = CrearProvider(new RelojPrueba(12), rol: rol, autenticado: autenticado, id: id);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListarNodosAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConsultarAsync("nodo-a"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerPanelActivoAsync("nodo-a", "panel-a", false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerVolcadoRedAsync(true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EstablecerInversorEncendidoAsync(false));
    }

    [Fact]
    public async Task IdentificadoresInvalidos_NoCambianElEstadoExistente()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConsultarAsync("inexistente"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.EstablecerPanelActivoAsync("nodo-a", "inexistente", false));
        Assert.InRange((await service.ConsultarAsync("nodo-a")).PotenciaGeneradaW, 720m, 800m);
    }

    [Fact]
    public async Task Cancelacion_NoEjecutaUnaOrden()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.EstablecerPanelActivoAsync("nodo-a", "panel-a", false, cancelacion.Token));
        Assert.InRange((await service.ConsultarAsync("nodo-a")).PotenciaGeneradaW, 720m, 800m);
    }

    [Fact]
    public async Task EstadoGlobal_ApagarCeroPotenciaYPrenderRestauraGeneracion()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();

        await service.EstablecerInversorEncendidoAsync(false);
        var apagado = await service.ConsultarAsync("nodo-a");
        Assert.False(apagado.InversorEncendido);
        Assert.Equal(0m, apagado.PotenciaGeneradaW);
        Assert.All(apagado.Paneles, panel => Assert.Equal(0m, panel.PotenciaGeneradaW));

        await service.EstablecerInversorEncendidoAsync(true);
        var encendido = await service.ConsultarAsync("nodo-a");
        Assert.True(encendido.InversorEncendido);
        Assert.InRange(encendido.PotenciaGeneradaW, 720m, 800m);
    }

    private static ServiceProvider CrearProvider(RelojPrueba reloj, bool predeterminado = false,
        string? falla = null, string rol = "Administrador", bool autenticado = true, string id = "usuario-prueba")
    {
        var valores = new Dictionary<string, string?>();
        if (!predeterminado)
        {
            valores["InversorSimulador:Nodos:nodo-a"] = "panel-a,panel-b";
            valores["InversorSimulador:Nodos:nodo-b"] = "panel-a";
        }
        valores["InversorSimulador:PanelesConFalla"] = falla;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSolarInfrastructure(configuration);
        services.RemoveAll<IInversorClient>();
        services.AddSingleton<IInversorClient, InversorSimulador>();
        services.AddSingleton<IInversorInventarioRepository>(predeterminado
            ? CrearInventarioPrueba(("nodo-1", "Nodo existente", "Parque existente", ["panel-1", "panel-2", "panel-3", "panel-4"]))
            : CrearInventarioPrueba(("nodo-a", "Nodo A", "Parque A", ["panel-a", "panel-b"]), ("nodo-b", "Nodo B", "Parque B", ["panel-a"])));
        services.AddSingleton<TimeProvider>(reloj);
        services.AddSingleton<IUsuarioActual>(new UsuarioPrueba(autenticado, id, rol));
        services.AddScoped<IInversorService, InversorService>();
        // Solo se resuelve la simulación. Sin JSON, .env, user-secrets ni conexión a MariaDB/Keycloak.
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IInversorInventarioRepository CrearInventarioPrueba(params (string Id, string Nombre, string Parque, string[] Paneles)[] valores)
        => new InventarioPrueba(valores.Select(nodo => new InventarioNodoInversorDto(nodo.Id, nodo.Nombre, nodo.Parque,
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
            return Task.FromResult(new UsuarioActualDto(autenticado, id, new[] { rol }));
        }
    }
}
