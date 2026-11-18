using Microsoft.Extensions.DependencyInjection;
using Radzen.Blazor;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Presentation.Components.Pages;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class ReportesFiltrosIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task RangoHorarioFiltraPorPanelYExportaAmbosPeriodos()
    {
        await database.ReiniciarAsync();
        await using var app = EntornoPantallas.Crear(database, "Consulta");
        await using var scope = app.Services.CreateAsyncScope();
        await using var vista = EntornoPantallas.Renderizador(scope.ServiceProvider);
        await vista.AbrirAsync<ReportesHistorico>();
        await vista.SeleccionarAsync("ParqueReporte", database.Parque.Id);
        await vista.SeleccionarAsync<Guid?>("NodoReporte", database.Panel.NodoId);
        await vista.SeleccionarAsync<Guid?>("PanelReporte", database.Panel.Id);
        await vista.SeleccionarAsync("PeriodoReporte", PeriodoHistorico.Rango);
        await EstablecerFechaAsync(vista, "FinReporte", null);
        Assert.Contains("Completá las fechas del período", await vista.TextoAsync());

        var inicio = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Unspecified);
        await EstablecerFechaAsync(vista, "InicioReporte", inicio);
        await EstablecerFechaAsync(vista, "FinReporte", inicio.AddHours(1));
        await vista.Dispatcher.InvokeAsync(async () =>
        {
            var comparar = vista.Control<RadzenCheckBox<bool>>(_ => true);
            await comparar.ValueChanged.InvokeAsync(true);
            await comparar.Change.InvokeAsync(true);
        });
        var html = await vista.TextoAsync();
        Assert.Contains("Segundo período", html);
        Assert.Contains("2026-02-01 01:00", html);
        await vista.PulsarAsync("Exportar CSV");
        await vista.PulsarAsync("Exportar CSV del segundo período");
        var interop = scope.ServiceProvider.GetRequiredService<EntornoPantallas.InteropPruebas>();
        var descargas = interop.Llamadas.Where(l => l.Funcion == "descargarCsv").ToArray();
        Assert.Equal(2, descargas.Length);
        Assert.All(descargas, llamada => Assert.EndsWith(".csv", Assert.IsType<string>(llamada.Argumentos[0])));
    }

    private static Task EstablecerFechaAsync(RenderizadorInteractivo vista, string nombre, DateTime? fecha)
        => vista.Dispatcher.InvokeAsync(async () =>
        {
            var campo = vista.Control<RadzenDatePicker<DateTime?>>(c => c.Name == nombre);
            await campo.ValueChanged.InvokeAsync(fecha);
            await campo.Change.InvokeAsync(fecha);
        });
}
