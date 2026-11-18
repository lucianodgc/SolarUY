using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Inversores;
using SolarUY.Application.Mantenimiento;
using SolarUY.Application.Paneles;
using SolarUY.Application.ParquesSolares;
using SolarUY.Infrastructure.Inversores;
using SolarUY.Presentation.Components.Pages;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class InventarioPantallasIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task Parque_ValidaRegistraEditaYRespetaLaConfirmacionDeBaja()
    {
        await database.ReiniciarAsync();
        await using var app = EntornoPantallas.Crear(database, "Administrador");
        await using var scope = app.Services.CreateAsyncScope();
        var servicios = scope.ServiceProvider;
        await using var vista = EntornoPantallas.Renderizador(servicios);
        await vista.AbrirAsync<AltaParqueSolar>();
        await vista.EnviarAsync<CrearParqueSolarDto>();
        await vista.EscribirAsync("Nombre", "Parque en prueba de interfaz");
        await vista.EscribirAsync("Ubicacion", "Rocha");
        await vista.EnviarAsync<CrearParqueSolarDto>();
        var parque = Assert.Single(await servicios.GetRequiredService<IParqueSolarService>()
            .BuscarParquesSolaresAsync("Parque en prueba de interfaz"));
        Assert.Equal("Rocha", parque.Ubicacion);

        await vista.AbrirAsync<EditarParqueSolar>(new() { [nameof(EditarParqueSolar.Id)] = parque.Id });
        await vista.EscribirAsync("Nombre", database.Parque.Nombre);
        await vista.EnviarAsync<ActualizarParqueSolarDto>();
        await vista.EscribirAsync("Nombre", "Parque editado");
        await vista.EscribirAsync("Ubicacion", "Maldonado");
        await vista.EnviarAsync<ActualizarParqueSolarDto>();
        Assert.Equal("/parques-solares", new Uri(servicios.GetRequiredService<NavigationManager>().Uri).AbsolutePath);

        await vista.AbrirAsync<ParquesSolares>();
        await vista.EscribirAsync("BusquedaParques", "no existe este parque");
        await vista.PulsarAsync("Buscar");
        await vista.PulsarAsync("Limpiar");
        await vista.EscribirAsync("BusquedaParques", "Parque editado");
        await vista.PulsarAsync("Buscar");
        await ResolverBajaAsync(vista, servicios, confirmar: false);
        await using (var db = database.CrearContexto())
            Assert.Equal("Maldonado", await db.ParquesSolares.Where(p => p.Id == parque.Id).Select(p => p.Ubicacion).SingleAsync());
        await ResolverBajaAsync(vista, servicios, confirmar: true);
        await using (var db = database.CrearContexto())
            Assert.False(await db.ParquesSolares.AnyAsync(p => p.Id == parque.Id));
    }

    [MariaDbHistoricoFact]
    public async Task Panel_AltaYEdicionGuardanElNodoYLaPotenciaElegidos()
    {
        await database.ReiniciarAsync();
        await using (var preparacion = database.CrearContexto())
        {
            var nodoConCupo = await preparacion.Nodos.SingleAsync(nodo => nodo.Id == database.Panel.NodoId);
            nodoConCupo.ActualizarDatos(nodoConCupo.Nombre, nodoConCupo.UbicacionEnParque, 3);
            var nodoLleno = await preparacion.Nodos.SingleAsync(nodo => nodo.Id == database.PanelAjeno.NodoId);
            nodoLleno.ActualizarDatos(nodoLleno.Nombre, nodoLleno.UbicacionEnParque, 1);
            await preparacion.SaveChangesAsync();
        }
        await using var app = EntornoPantallas.Crear(database, "Administrador");
        await using var scope = app.Services.CreateAsyncScope();
        await using var vista = EntornoPantallas.Renderizador(scope.ServiceProvider);
        await vista.AbrirAsync<AltaPanel>();
        await vista.EnviarAsync<CrearPanelDto>();
        Assert.Contains("Seleccioná un nodo.", await vista.TextoAsync());
        var selector = vista.Control<RadzenDropDown<Guid?>>(control => control.Name == "NodoId");
        var nodos = Assert.IsAssignableFrom<IEnumerable<NodoParaPanelDto>>(selector.Data);
        var destino = Assert.Single(nodos, nodo => nodo.Id == database.Panel.NodoId);
        Assert.Contains(database.Parque.Nombre, destino.Etiqueta);
        Assert.Contains(destino.Nombre, destino.Etiqueta);
        Assert.Equal(1, destino.CuposDisponibles);
        Assert.True(Assert.Single(nodos, nodo => nodo.Id == database.PanelAjeno.NodoId).SinCupo);
        Assert.Null(Assert.Single(nodos, nodo => nodo.Id == database.TercerPanel.NodoId).CuposDisponibles);
        Assert.Equal(nameof(NodoParaPanelDto.SinCupo), selector.DisabledProperty);
        await vista.SeleccionarAsync<Guid?>("NodoId", destino.Id);
        await vista.EscribirAsync("NumeroSerie", "SERIE-INTERFAZ");
        await vista.EscribirAsync("Marca", "Marca prueba");
        await vista.EscribirAsync("Modelo", "Modelo prueba");
        await vista.Dispatcher.InvokeAsync(() => vista.Control<RadzenNumeric<decimal>>(n => n.Name == "PotenciaNominalW")
            .ValueChanged.InvokeAsync(505.25m));
        await vista.EnviarAsync<CrearPanelDto>();
        var registrado = Assert.Single(await scope.ServiceProvider.GetRequiredService<SolarUY.Application.Paneles.IPanelService>()
            .ListarAsync(), item => item.NumeroSerie == "SERIE-INTERFAZ");
        Assert.Equal(destino.Id, registrado.NodoId);
        var id = registrado.Id;

        await vista.AbrirAsync<EditarPanel>(new() { [nameof(EditarPanel.PanelId)] = id });
        await vista.EscribirAsync("nodoId", "no-es-un-guid");
        await vista.EnviarAsync<ActualizarPanelDto>();
        Assert.Contains("El identificador del nodo no es válido", await vista.TextoAsync());
        await vista.AbrirAsync<EditarPanel>(new() { [nameof(EditarPanel.PanelId)] = id });
        await vista.EscribirAsync("nodoId", database.TercerPanel.NodoId.ToString());
        await vista.EscribirAsync("Modelo", "Modelo corregido");
        await vista.EnviarAsync<ActualizarPanelDto>();
        await using var db = database.CrearContexto();
        var panel = await db.Paneles.SingleAsync(p => p.Id == id);
        Assert.Equal("Modelo corregido", panel.Modelo);
        Assert.Equal(database.TercerPanel.NodoId, panel.NodoId);
        Assert.Equal(505.25m, panel.PotenciaNominalW);
    }

    private static Task ResolverBajaAsync(RenderizadorInteractivo vista, IServiceProvider servicios, bool confirmar)
        => EntornoPantallas.ResolverDialogoAsync(vista, servicios, "Eliminar", confirmar);
}

internal static class EntornoPantallas
{
    internal static WebApplication Crear(HistoricoDatabase database, string rol)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(SolarUY.Presentation.Components.App).Assembly.GetName().Name,
            ContentRootPath = HistoricoDatabase.RutaProyecto("SolarUY.Presentation")
        });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SolarUY"] = database.ConexionConfigurada
        });
        Program.ConfigurarServicios(builder);
        builder.Services.AddScoped<AuthenticationStateProvider>(_ => new Identidad(rol));
        builder.Services.AddScoped<NavigationManager, NavegacionPruebas>();
        builder.Services.AddScoped<InteropPruebas>();
        builder.Services.AddScoped<IJSRuntime>(sp => sp.GetRequiredService<InteropPruebas>());
        builder.Services.AddSingleton<ITecnicoDirectory, DirectorioPruebas>();
        builder.Services.AddSingleton<IInversorClient, InversorSimulador>();
        return builder.Build();
    }

    internal static RenderizadorInteractivo Renderizador(IServiceProvider servicios)
        => new(servicios, servicios.GetRequiredService<ILoggerFactory>());

    internal static async Task ResolverDialogoAsync(RenderizadorInteractivo vista, IServiceProvider servicios, string boton, bool confirmar)
    {
        var dialogos = servicios.GetRequiredService<DialogService>();
        var abierto = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void RegistrarApertura(string titulo, Type tipo, Dictionary<string, object> parametros, DialogOptions opciones)
            => abierto.TrySetResult();
        dialogos.OnOpen += RegistrarApertura;
        try
        {
            var accion = vista.PulsarAsync(boton);
            await abierto.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await vista.Dispatcher.InvokeAsync(() => dialogos.Close(confirmar));
            await accion;
        }
        finally { dialogos.OnOpen -= RegistrarApertura; }
    }

    private sealed class Identidad(string rol) : AuthenticationStateProvider, IHostEnvironmentAuthenticationStateProvider
    {
        private Task<AuthenticationState> _estado = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, rol), new Claim("sub", "tecnico-prueba")], "Integración"))));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => _estado;

        public void SetAuthenticationState(Task<AuthenticationState> authenticationStateTask)
        {
            _estado = authenticationStateTask;
            NotifyAuthenticationStateChanged(authenticationStateTask);
        }
    }

    private sealed class NavegacionPruebas : NavigationManager
    {
        public NavegacionPruebas() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
            NotifyLocationChanged(false);
        }
    }

    internal sealed class InteropPruebas : IJSRuntime, IJSObjectReference
    {
        public List<(string Funcion, object?[] Argumentos)> Llamadas { get; } = [];

        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args)
            => InvokeAsync<T>(identifier, CancellationToken.None, args);

        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Llamadas.Add((identifier, args ?? []));
            if (identifier == "Radzen.createChart")
                return ValueTask.FromResult(JsonSerializer.Deserialize<T>("""{"width":800,"height":360}""", JsonSerializerOptions.Web)!);
            return ValueTask.FromResult(this is T referencia ? referencia : default!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DirectorioPruebas : ITecnicoDirectory
    {
        public Task<IReadOnlyList<TecnicoDto>> ListarAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TecnicoDto>>([new("tecnico-prueba", "Técnico de prueba")]);

        public Task<bool> EsTecnicoHabilitadoAsync(string responsableId, CancellationToken cancellationToken = default)
            => Task.FromResult(responsableId == "tecnico-prueba");
    }
}
