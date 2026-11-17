using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;
using SolarUY.Application.Abstractions;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.Mantenimiento;
using SolarUY.Application.Reportes;
using SolarUY.Domain.Entities;
using SolarUY.Infrastructure;
using SolarUY.Presentation.Components.Pages;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class ReportesConcurrenciaIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task Pagina_DescartaRespuestaTardiaAunqueElServicioNoRespeteCancelacion()
    {
        await database.ReiniciarAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)
        });
        builder.Services.AddSolarInfrastructure(builder.Configuration);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddRadzenComponents();
        builder.Services.AddSingleton<IUsuarioActual, IdentidadConcurrencia>();
        builder.Services.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
        builder.Services.AddScoped<ReporteHistoricoService>();
        builder.Services.AddScoped<ReporteDemorado>();
        builder.Services.AddScoped<IReporteHistoricoService>(sp => sp.GetRequiredService<ReporteDemorado>());
        var activador = new CapturaComponente();
        builder.Services.AddSingleton<IComponentActivator>(activador);
        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var raiz = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<ReportesHistorico>());
        var pagina = activador.Pagina!;
        var servicio = scope.ServiceProvider.GetRequiredService<ReporteDemorado>();
        servicio.DemorarProxima = true;
        Task primera = Task.CompletedTask;
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            Establecer(pagina, "_parqueId", database.Parque.Id);
            Establecer(pagina, "_inicio", new DateTime(2026, 2, 1));
            primera = Actualizar(pagina);
        });
        await servicio.PrimeraLista.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var cargando = await renderer.Dispatcher.InvokeAsync(raiz.ToHtmlString);
        Assert.Contains("Consultando reporte", cargando);
        Assert.DoesNotContain("Mantenimientos realizados</h2>", cargando);
        try
        {
            await renderer.Dispatcher.InvokeAsync(async () =>
            {
                Establecer(pagina, "_inicio", new DateTime(2026, 2, 2));
                await Actualizar(pagina);
            });
            var ultimo = await renderer.Dispatcher.InvokeAsync(raiz.ToHtmlString);
            Assert.Contains("2026-02-02 00:00", ultimo);
            Assert.Contains("No hay energ", ultimo);
            servicio.LiberarPrimera.TrySetResult();
            await primera.WaitAsync(TimeSpan.FromSeconds(10));
            var despues = await renderer.Dispatcher.InvokeAsync(raiz.ToHtmlString);
            Assert.Equal(ultimo, despues);
            Assert.True(servicio.CancelacionRecibida.IsCancellationRequested);
        }
        finally { servicio.LiberarPrimera.TrySetResult(); }
    }

    [MariaDbHistoricoFact]
    public async Task Pagina_ConservaUltimaSeleccionCuandoUnaCancelacionAnteriorSeDemora()
        => await VerificarCancelacionDemoradaAsync(usarMariaDb: true);

    [Fact]
    public async Task PaginaYServicios_ConservanUltimaSeleccionConCancelacionDemoradaSinMariaDb()
        => await VerificarCancelacionDemoradaAsync(usarMariaDb: false);

    private async Task VerificarCancelacionDemoradaAsync(bool usarMariaDb)
    {
        if (usarMariaDb) await database.ReiniciarAsync();
        var parqueId = usarMariaDb ? database.Parque.Id : Guid.NewGuid();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        if (usarMariaDb)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SolarUY"] = Environment.GetEnvironmentVariable(HistoricoDatabase.VariableConexion)
            });
            builder.Services.AddSolarInfrastructure(builder.Configuration);
        }
        else
        {
            builder.Services.AddSingleton<IHistoricoGeneracionRepository>(new HistoricoControladoRepository(parqueId));
            builder.Services.AddSingleton<ITareaMantenimientoRepository, MantenimientoVacioRepository>();
        }
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddRadzenComponents();
        builder.Services.AddSingleton<IUsuarioActual, IdentidadConcurrencia>();
        builder.Services.AddScoped<IHistoricoGeneracionService, HistoricoGeneracionService>();
        builder.Services.AddScoped<ReporteHistoricoService>();
        builder.Services.AddScoped<ReporteDemorado>();
        builder.Services.AddScoped<IReporteHistoricoService>(sp => sp.GetRequiredService<ReporteDemorado>());
        var activador = new CapturaComponente();
        builder.Services.AddSingleton<IComponentActivator>(activador);
        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var raiz = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<ReportesHistorico>());
        var pagina = activador.Pagina!;
        var servicio = scope.ServiceProvider.GetRequiredService<ReporteDemorado>();
        servicio.DemorarProxima = true;
        servicio.DemorarCancelacion = true;
        Task primera = Task.CompletedTask, segunda = Task.CompletedTask;
        try
        {
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                Establecer(pagina, "_parqueId", parqueId);
                Establecer(pagina, "_inicio", new DateTime(2026, 2, 1));
                primera = Actualizar(pagina);
            });
            await servicio.PrimeraLista.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                Establecer(pagina, "_inicio", new DateTime(2026, 2, 2));
                segunda = Actualizar(pagina);
            });
            await servicio.CancelacionIniciada.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var cargando = await renderer.Dispatcher.InvokeAsync(raiz.ToHtmlString);
            Assert.Contains("Consultando reporte", cargando);
            Assert.DoesNotContain("Mantenimientos realizados</h2>", cargando);
            await renderer.Dispatcher.InvokeAsync(async () =>
            {
                Establecer(pagina, "_inicio", new DateTime(2026, 2, 3));
                await Actualizar(pagina);
            });
            var ultimo = await renderer.Dispatcher.InvokeAsync(raiz.ToHtmlString);
            Assert.Contains("2026-02-03 00:00", ultimo);
            servicio.LiberarCancelacion.TrySetResult();
            await segunda.WaitAsync(TimeSpan.FromSeconds(10));
            servicio.LiberarPrimera.TrySetResult();
            await primera.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(ultimo, await renderer.Dispatcher.InvokeAsync(raiz.ToHtmlString));
        }
        finally
        {
            servicio.LiberarCancelacion.TrySetResult();
            servicio.LiberarPrimera.TrySetResult();
            await Task.WhenAll(primera, segunda).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    // Se envía por IHandleEvent, como los callbacks de Blazor; solo la asignación de filtros
    // sustituye el binding del navegador. El componente, el renderer y los servicios son reales;
    // los repositorios también lo son en las variantes que requieren MariaDB.
    private static Task Actualizar(ReportesHistorico pagina) => EventCallback.Factory.Create(pagina,
        () => (Task)typeof(ReportesHistorico).GetMethod("ActualizarAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(pagina, null)!).InvokeAsync();

    private static void Establecer(ReportesHistorico pagina, string campo, object valor)
        => typeof(ReportesHistorico).GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pagina, valor);

    private sealed class CapturaComponente : IComponentActivator
    {
        public ReportesHistorico? Pagina { get; private set; }
        public IComponent CreateInstance(Type componentType)
        {
            var componente = (IComponent)Activator.CreateInstance(componentType)!;
            if (componente is ReportesHistorico pagina) Pagina = pagina;
            return componente;
        }
    }

    private sealed class IdentidadConcurrencia : IUsuarioActual
    {
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UsuarioActualDto(true, "usuario-prueba", ["Consulta"]));
    }

    // Esta variante integra la página, Radzen y ambos servicios reales sin MariaDB.
    // Los repositorios controlan solo la frontera de persistencia; el caso con MariaDB
    // conserva la verificación de los repositorios reales.
    private sealed class HistoricoControladoRepository(Guid parqueId) : IHistoricoGeneracionRepository
    {
        public Task<bool> ExisteObjetivoAsync(TipoObjetivoHistorico tipo, Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(tipo == TipoObjetivoHistorico.Parque && id == parqueId);
        public Task<IReadOnlyList<OpcionHistoricoDto>> ListarOpcionesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OpcionHistoricoDto>>([new(TipoObjetivoHistorico.Parque, parqueId, "Parque de concurrencia", parqueId)]);
        public Task<ResultadoHistoricoDto> ConsultarAsync(ConsultaHistoricoDto consulta, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResultadoHistoricoDto(consulta, null, CoberturaHistorico.SinDatos,
                consulta.Intervalos.Select(intervalo => new PuntoHistoricoDto(intervalo, null, CoberturaHistorico.SinDatos, false, false)).ToArray()));
    }

    private sealed class MantenimientoVacioRepository : ITareaMantenimientoRepository
    {
        public Task<IReadOnlyList<MantenimientoRealizadoReporteDto>> ListarRealizadosParaReporteAsync(
            ConsultaMantenimientosReporteDto consulta, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MantenimientoRealizadoReporteDto>>([]);
        public Task AgregarAsync(TareaMantenimiento tarea, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("El reporte no debe escribir tareas.");
        public Task<TareaMantenimiento?> ObtenerAsync(Guid id, string? responsableAutorizado, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<TareaMantenimiento>> ListarAsync(FiltroTareasDto filtro, string? responsableAutorizado,
            DateTime referenciaUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> GuardarAsync(TareaMantenimiento tarea, TareaMantenimientoOriginal original,
            string? responsableAutorizado, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("El reporte no debe escribir tareas.");
    }

    private sealed class ReporteDemorado(ReporteHistoricoService real) : IReporteHistoricoService
    {
        public bool DemorarProxima { get; set; }
        public bool DemorarCancelacion { get; set; }
        public TaskCompletionSource PrimeraLista { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LiberarPrimera { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancelacionIniciada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LiberarCancelacion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken CancelacionRecibida { get; private set; }
        public Task<IReadOnlyList<OpcionHistoricoDto>> ListarOpcionesAsync(CancellationToken cancellationToken = default)
            => real.ListarOpcionesAsync(cancellationToken);
        public Task<ArchivoReporteCsvDto> ExportarCsvAsync(ResultadoReporteDto reporte, CancellationToken cancellationToken = default)
            => real.ExportarCsvAsync(reporte, cancellationToken);
        public async Task<ResultadoReporteDto> ConsultarAsync(FiltroReporteDto filtro, CancellationToken cancellationToken = default)
        {
            if (!DemorarProxima) return await real.ConsultarAsync(filtro, cancellationToken);
            DemorarProxima = false;
            CancelacionRecibida = cancellationToken;
            var resultado = await real.ConsultarAsync(filtro, CancellationToken.None);
            using var registro = cancellationToken.Register(() =>
            {
                if (!DemorarCancelacion) return;
                CancelacionIniciada.TrySetResult();
                LiberarCancelacion.Task.GetAwaiter().GetResult();
            });
            PrimeraLista.TrySetResult();
            await LiberarPrimera.Task;
            return resultado;
        }
    }
}
