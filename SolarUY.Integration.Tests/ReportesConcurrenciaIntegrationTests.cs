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
using SolarUY.Application.Reportes;
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

    // Se envía por IHandleEvent, como los callbacks de Blazor; solo la asignación de filtros
    // sustituye el binding del navegador. El componente, el renderer, los servicios y MariaDB son reales.
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

    private sealed class ReporteDemorado(ReporteHistoricoService real) : IReporteHistoricoService
    {
        public bool DemorarProxima { get; set; }
        public TaskCompletionSource PrimeraLista { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LiberarPrimera { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
            PrimeraLista.TrySetResult();
            await LiberarPrimera.Task;
            return resultado;
        }
    }
}
