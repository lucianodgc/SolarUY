using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Radzen.Blazor;
using SolarUY.Application.Mantenimiento;
using SolarUY.Domain.Enums;
using SolarUY.Presentation.Components.Pages;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class MantenimientoPantallasIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task AdministradorProgramaYEditaYTecnicoEjecutaLaTareaAsignada()
    {
        await database.ReiniciarAsync();
        Guid id;
        await using (var app = EntornoPantallas.Crear(database, "Administrador"))
        await using (var scope = app.Services.CreateAsyncScope())
        await using (var vista = EntornoPantallas.Renderizador(scope.ServiceProvider))
        {
            await vista.AbrirAsync<CrearTareaMantenimiento>();
            await vista.EnviarAsync<CrearTareaMantenimientoDto>();
            await vista.Dispatcher.InvokeAsync(async () =>
            {
                await vista.Control<RadzenDropDown<Guid>>(c => c.Name == "Panel").ValueChanged.InvokeAsync(database.Panel.Id);
                await vista.Control<RadzenDropDown<string>>(c => c.Name == "Responsable").ValueChanged.InvokeAsync("tecnico-prueba");
                await vista.Control<RadzenDatePicker<DateTime?>>(c => c.Name == "Fecha").ValueChanged.InvokeAsync(DateTime.UtcNow.AddDays(5));
                await vista.Control<RadzenTextArea>(c => c.Name == "Descripcion").ValueChanged.InvokeAsync("Trabajo de prueba");
            });
            await vista.EscribirAsync("Tipo", "Revisión de prueba");
            await vista.EnviarAsync<CrearTareaMantenimientoDto>();
            await vista.AbrirAsync<MantenimientoPaneles>();
            var grid = vista.Control<RadzenDataGrid<TareaMantenimientoDto>>(_ => true);
            var tarea = Assert.Single(grid.Data, t => t.Tipo == "Revisión de prueba");
            id = tarea.Id;
            Assert.False(tarea.PuedeIniciar);
            await vista.PulsarAsync("Editar / Reasignar");
            await vista.EscribirAsync("EditarTipo", "Limpieza de prueba");
            await vista.EnviarAsync<EditarTareaMantenimientoDto>();
            Assert.Contains(grid.Data, t => t.Id == id && t.Tipo == "Limpieza de prueba");
        }

        await using (var app = EntornoPantallas.Crear(database, "Tecnico"))
        await using (var scope = app.Services.CreateAsyncScope())
        await using (var vista = EntornoPantallas.Renderizador(scope.ServiceProvider))
        {
            await vista.AbrirAsync<MantenimientoPaneles>();
            await vista.PulsarAsync("Iniciar");
            await vista.PulsarAsync("Completar");
        }

        await using var db = database.CrearContexto();
        var completada = await db.TareasMantenimiento.SingleAsync(t => t.Id == id);
        Assert.Equal(EstadoTareaMantenimiento.Completada, completada.Estado);
        Assert.NotNull(completada.FechaRealizacion);
        Assert.Equal("tecnico-prueba", completada.ResponsableId);
    }
}
