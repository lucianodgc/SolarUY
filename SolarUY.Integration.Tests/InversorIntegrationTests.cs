using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Inversores;
using SolarUY.Infrastructure;

namespace SolarUY.Integration.Tests;

public sealed class InversorIntegrationTests
{
    [Fact]
    public async Task Composicion_ExponeTopologiaPredeterminadaSinBaseDeDatos()
    {
        await using var provider = CrearProvider(new RelojPrueba(12), predeterminado: true);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();

        Assert.Equal(new[] { "nodo-1" }, await service.ListarNodosAsync());
        var estado = await service.ConsultarAsync("nodo-1");

        Assert.Equal(new[] { "panel-1", "panel-2", "panel-3", "panel-4" }, estado.Paneles.Select(p => p.PanelId));
        Assert.All(estado.Paneles, p => Assert.True(p.EstaActivo));
        Assert.Equal(1600m, estado.PotenciaGeneradaW);
        Assert.True(estado.PuedeOperar);
        Assert.False(estado.VolcadoRedActivo);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(6, false)]
    [InlineData(8, true)]
    [InlineData(12, true)]
    [InlineData(16, true)]
    [InlineData(18, false)]
    [InlineData(23, false)]
    public async Task Consulta_AplicaCurvaSolarSegunReloj(int hora, bool genera)
    {
        await using var provider = CrearProvider(new RelojPrueba(hora));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        var client = scope.ServiceProvider.GetRequiredService<IInversorClient>();

        var estado = await service.ConsultarAsync("nodo-a");

        Assert.All(estado.Paneles, p => Assert.InRange(p.PotenciaGeneradaW, 0m, 400m));
        Assert.Equal(genera, estado.PotenciaGeneradaW > 0m);
        Assert.Equal(estado.Paneles.Sum(p => p.PotenciaGeneradaW), estado.PotenciaGeneradaW);
        Assert.Equal(estado.PotenciaGeneradaW, await client.ObtenerPotenciaGeneradaAsync("nodo-a"));
    }

    [Fact]
    public async Task Consulta_MediodiaSuperaMananaYTardeYUsaHoraLocal()
    {
        var reloj = new RelojPrueba(8);
        await using var provider = CrearProvider(reloj);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        var manana = await service.ConsultarAsync("nodo-a");
        reloj.Instante = reloj.Instante.AddHours(4);
        var mediodia = await service.ConsultarAsync("nodo-a");
        reloj.Instante = reloj.Instante.AddHours(4);
        var tarde = await service.ConsultarAsync("nodo-a");

        Assert.Equal(800m, mediodia.PotenciaGeneradaW);
        Assert.True(manana.PotenciaGeneradaW < mediodia.PotenciaGeneradaW);
        Assert.True(tarde.PotenciaGeneradaW < mediodia.PotenciaGeneradaW);

        reloj.Zona = TimeZoneInfo.CreateCustomTimeZone("UTC-3-prueba", TimeSpan.FromHours(-3), "UTC-3", "UTC-3");
        reloj.Instante = new DateTimeOffset(2026, 10, 22, 15, 0, 0, TimeSpan.Zero);
        Assert.Equal(800m, (await service.ConsultarAsync("nodo-a")).PotenciaGeneradaW);
    }

    [Fact]
    public async Task OrdenesPanel_SonIdempotentesYAislanPanelYNodo()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        Assert.Equal(new[] { "nodo-a", "nodo-b" }, await service.ListarNodosAsync());

        await service.EstablecerPanelActivoAsync("nodo-a", "panel-a", false);
        await service.EstablecerPanelActivoAsync("nodo-a", "panel-a", false);
        var estado = await service.ConsultarAsync("nodo-a");
        Assert.False(estado.Paneles.Single(p => p.PanelId == "panel-a").EstaActivo);
        Assert.Equal(0m, estado.Paneles.Single(p => p.PanelId == "panel-a").PotenciaGeneradaW);
        Assert.Equal(400m, estado.Paneles.Single(p => p.PanelId == "panel-b").PotenciaGeneradaW);
        Assert.Equal(400m, (await service.ConsultarAsync("nodo-b")).PotenciaGeneradaW);

        await service.EstablecerPanelActivoAsync("nodo-a", "panel-a", true);
        await service.EstablecerPanelActivoAsync("nodo-a", "panel-a", true);
        Assert.Equal(800m, (await service.ConsultarAsync("nodo-a")).PotenciaGeneradaW);
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
        Assert.Equal(400m, estado.PotenciaGeneradaW);
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
            Assert.Equal(400m, estado.PotenciaGeneradaW);
            await service.EstablecerVolcadoRedAsync(false);
            Assert.False((await service.ConsultarAsync("nodo-a")).VolcadoRedActivo);
        }
        await using var nuevoProvider = CrearProvider(new RelojPrueba(12));
        await using var nuevoScope = nuevoProvider.CreateAsyncScope();
        var inicial = await nuevoScope.ServiceProvider.GetRequiredService<IInversorService>().ConsultarAsync("nodo-a");
        Assert.False(inicial.VolcadoRedActivo);
        Assert.Equal(800m, inicial.PotenciaGeneradaW);
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
        Assert.Equal(800m, (await service.ConsultarAsync("nodo-a")).PotenciaGeneradaW);
        Assert.False((await service.ConsultarAsync("nodo-a")).VolcadoRedActivo);
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
    }

    [Fact]
    public async Task IdentificadoresInvalidos_NoCambianElEstadoExistente()
    {
        await using var provider = CrearProvider(new RelojPrueba(12));
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInversorService>();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConsultarAsync("inexistente"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.EstablecerPanelActivoAsync("nodo-a", "inexistente", false));
        Assert.Equal(800m, (await service.ConsultarAsync("nodo-a")).PotenciaGeneradaW);
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
        Assert.Equal(800m, (await service.ConsultarAsync("nodo-a")).PotenciaGeneradaW);
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
        services.AddSingleton<TimeProvider>(reloj);
        services.AddSingleton<IUsuarioActual>(new UsuarioPrueba(autenticado, id, rol));
        services.AddScoped<IInversorService, InversorService>();
        // Solo se resuelve la simulación. Sin JSON, .env, user-secrets ni conexión a MariaDB/Keycloak.
        return services.BuildServiceProvider(validateScopes: true);
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
