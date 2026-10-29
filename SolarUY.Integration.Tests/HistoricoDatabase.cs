using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using SolarUY.Application.Abstractions;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Infrastructure;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Integration.Tests;

public sealed class MariaDbHistoricoFactAttribute : FactAttribute
{
    public MariaDbHistoricoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = $"Configura {HistoricoDatabase.VariableConexion} para la base aislada de req007.";
    }
}

public sealed class MariaDbHistoricoTheoryAttribute : TheoryAttribute
{
    public MariaDbHistoricoTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)))
            Skip = $"Configura {HistoricoDatabase.VariableConexion} para la base aislada de req007.";
    }
}

public sealed class HistoricoDatabase : IAsyncLifetime
{
    public const string VariableConexion = "SOLARUY_HISTORICO_TEST_CONNECTION";
    private string Conexion => Environment.GetEnvironmentVariable(VariableConexion)
        ?? throw new InvalidOperationException($"Falta {VariableConexion}.");
    public ParqueSolar Parque { get; private set; } = null!;
    public ParqueSolar OtroParque { get; private set; } = null!;
    public Panel Panel { get; private set; } = null!;
    public Panel SegundoPanel { get; private set; } = null!;
    public Panel TercerPanel { get; private set; } = null!;
    public Panel PanelAjeno { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VariableConexion))) return;
        // Ninguna operación de preparación/borrado puede apuntar a la base habitual.
        if (!new MySqlConnectionStringBuilder(Conexion).Database.StartsWith("solaruy_req007_test", StringComparison.Ordinal))
            throw new InvalidOperationException("La base de integración debe comenzar con solaruy_req007_test.");
        await using var context = CrearContexto();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public ApplicationDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(Conexion, new MariaDbServerVersion(new Version(11, 8, 0))).Options;
        return new ApplicationDbContext(options);
    }

    public async Task ReiniciarAsync()
    {
        await using var context = CrearContexto();
        await context.LecturasPanel.ExecuteDeleteAsync();
        await context.TareasMantenimiento.ExecuteDeleteAsync();
        await context.Paneles.ExecuteDeleteAsync();
        await context.Nodos.ExecuteDeleteAsync();
        await context.ParquesSolares.ExecuteDeleteAsync();
        Parque = new ParqueSolar("Parque histórico", "Salto");
        OtroParque = new ParqueSolar("Otro parque", "Rocha");
        var nodo = new Nodo(Parque, "Nodo uno", "Norte");
        var otroNodo = new Nodo(Parque, "Nodo dos", "Sur");
        Panel = CrearPanel(nodo, "A");
        SegundoPanel = CrearPanel(nodo, "B");
        TercerPanel = CrearPanel(otroNodo, "C");
        PanelAjeno = CrearPanel(new Nodo(OtroParque, "Nodo ajeno", "Este"), "D");
        context.Paneles.AddRange(Panel, SegundoPanel, TercerPanel, PanelAjeno);
        context.LecturasPanel.AddRange(
            Lectura(Panel, new(2025, 1, 1, 12, 0, 0), 1200),
            Lectura(Panel, new(2026, 1, 31, 23, 58, 0), 1200),
            // req010 prohíbe dos filas por panel/instante; conservar la potencia media del escenario.
            Lectura(Panel, new(2026, 2, 1, 0, 2, 0), 1200),
            Lectura(Panel, new(2026, 2, 1, 0, 4, 0), 0),
            Lectura(Panel, new(2026, 2, 1, 0, 10, 0), 1200),
            Lectura(Panel, new(2026, 2, 1, 0, 15, 0), 1200),
            Lectura(Panel, new(2026, 3, 1, 0, 0, 0), 1200),
            Lectura(SegundoPanel, new(2026, 2, 1, 0, 0, 0), 2400),
            Lectura(TercerPanel, new(2026, 2, 1, 0, 0, 0), 3600),
            Lectura(PanelAjeno, new(2026, 2, 1, 0, 0, 0), 12000));
        await context.SaveChangesAsync();
    }

    public static Panel CrearPanel(Nodo nodo, string serie) => new(nodo, serie, "Marca", "Modelo", 450,
        new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), EstadoPanel.Activo);

    public static LecturaPanel Lectura(Panel panel, DateTime local, decimal potencia) => new(panel,
        TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo")), potencia, EstadoSaludLectura.Sano);

    public SesionHistorico AbrirSesion(string rol = "Administrador", bool autenticado = true, string id = "usuario-prueba")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SolarUY"] = Conexion,
            ["MariaDB:ServerVersion"] = "11.8.0"
        }).Build();
        var services = new ServiceCollection();
        services.AddSolarInfrastructure(configuration);
        services.AddSingleton<IUsuarioActual>(new IdentidadHistoricoPrueba(new UsuarioActualDto(autenticado, id, new[] { rol })));
        services.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
        return new SesionHistorico(services.BuildServiceProvider(validateScopes: true));
    }

    private sealed class IdentidadHistoricoPrueba(UsuarioActualDto usuario) : IUsuarioActual
    {
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(usuario);
        }
    }
}

public sealed class SesionHistorico : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    public IHistoricoGeneracionService Servicio { get; }

    public SesionHistorico(ServiceProvider provider)
    {
        _provider = provider;
        _scope = provider.CreateAsyncScope();
        Servicio = _scope.ServiceProvider.GetRequiredService<IHistoricoGeneracionService>();
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
