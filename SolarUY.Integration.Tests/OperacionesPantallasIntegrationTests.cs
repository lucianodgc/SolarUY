using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Inversores;
using SolarUY.Application.Salud;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Domain.Salud;
using SolarUY.Presentation.Components.Pages;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class OperacionesPantallasIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoFact]
    public async Task Simulador_ConfirmaOrdenesYConservaSuAuditoriaAlCambiarDeParque()
    {
        await database.ReiniciarAsync();
        await using var app = EntornoPantallas.Crear(database, "Administrador");
        await using var scope = app.Services.CreateAsyncScope();
        var servicios = scope.ServiceProvider;
        await using var vista = EntornoPantallas.Renderizador(servicios);
        var pagina = await vista.AbrirAsync<SimuladorInversor>();
        await vista.SeleccionarAsync("ParqueSimulado", database.Parque.Id);
        await vista.SeleccionarAsync("NodoSimulado", database.TercerPanel.NodoId.ToString());

        await EntornoPantallas.ResolverDialogoAsync(vista, servicios, "Desactivar", confirmar: false);
        await using var db = database.CrearContexto();
        Assert.Equal(0, await db.Set<IntentoOrdenInversor>().CountAsync());
        await EntornoPantallas.ResolverDialogoAsync(vista, servicios, "Desactivar", confirmar: true);
        Assert.Contains("Confirmada", await vista.TextoAsync());
        await EntornoPantallas.ResolverDialogoAsync(vista, servicios, "Activar", confirmar: true);
        await EntornoPantallas.ResolverDialogoAsync(vista, servicios, "Activar volcado a red", confirmar: true);
        await EntornoPantallas.ResolverDialogoAsync(vista, servicios, "Desactivar volcado a red", confirmar: true);
        await vista.PulsarAsync("Apagar inversor");
        Assert.Contains("Apagado", await vista.TextoAsync());
        await vista.PulsarAsync("Prender inversor");
        var telemetria = await servicios.GetRequiredService<ITelemetriaEnVivoService>().ConsultarAsync();
        await pagina.TelemetriaActualizada(telemetria[database.Parque.Id].ToList());
        await vista.SeleccionarAsync("ParqueSimulado", database.OtroParque.Id);
        Assert.Contains("Nodo ajeno", await vista.TextoAsync());
        Assert.DoesNotContain("Confirmada", await vista.TextoAsync());

        var auditoria = await db.Set<IntentoOrdenInversor>().ToListAsync();
        Assert.Equal(4, auditoria.Count);
        Assert.All(auditoria, orden => Assert.Equal(EstadoOrdenInversor.Confirmada, orden.Estado));
        Assert.Equal(2, auditoria.Count(orden => orden.PanelValidado == database.TercerPanel.Id));
    }

    [MariaDbHistoricoFact]
    public async Task Monitoreo_ActualizaLaSerieDelParqueSeleccionadoYDescartaMuestrasAjenas()
    {
        await database.ReiniciarAsync();
        await using var app = EntornoPantallas.Crear(database, "Consulta");
        await using var scope = app.Services.CreateAsyncScope();
        var servicios = scope.ServiceProvider;
        var navegacion = servicios.GetRequiredService<NavigationManager>();
        navegacion.NavigateTo("/monitoreo");
        await using var vista = EntornoPantallas.Renderizador(servicios);
        var pagina = await vista.AbrirAsync<Monitoreo>();
        await vista.SeleccionarAsync("ParqueMonitoreo", database.Parque.Id);
        await pagina.NuevaLectura(database.Parque.Id.ToString());
        var telemetria = await servicios.GetRequiredService<ITelemetriaEnVivoService>().ConsultarAsync();
        var muestra = telemetria[database.Parque.Id].ToList();
        await pagina.TelemetriaActualizada(muestra);
        Assert.Contains("Conectado", await vista.TextoAsync());
        Assert.Contains("Nodo uno", await vista.TextoAsync());
        Assert.Contains("Fechas y cobertura de los puntos", await vista.TextoAsync());

        await vista.SeleccionarAsync("ParqueMonitoreo", database.OtroParque.Id);
        await pagina.TelemetriaActualizada(muestra);
        Assert.DoesNotContain("Nodo uno", await vista.TextoAsync());
        telemetria = await servicios.GetRequiredService<ITelemetriaEnVivoService>().ConsultarAsync();
        await pagina.TelemetriaActualizada(telemetria[database.OtroParque.Id].ToList());
        Assert.Contains("Nodo ajeno", await vista.TextoAsync());
        Assert.Contains(database.OtroParque.Id.ToString(), navegacion.Uri);
        await RegistrarFallaAsync(servicios);
        await pagina.SaludActualizada(database.OtroParque.Id.ToString());
        await vista.PulsarAsync("Ver cambios");
        Assert.Contains("Falla", await vista.TextoAsync());
        Assert.Contains("Sin estado previo", await vista.TextoAsync());
        await pagina.EstadoConexion("Sesión no autorizada");
        Assert.Contains("Sesión no autorizada", await vista.TextoAsync());
    }

    private async Task RegistrarFallaAsync(IServiceProvider servicios)
    {
        var ahora = DateTime.UtcNow;
        await using var db = database.CrearContexto();
        var panel = await db.Paneles.SingleAsync(p => p.Id == database.PanelAjeno.Id);
        db.LecturasPanel.Add(new LecturaPanel(panel, ahora.AddSeconds(-1), 0m, EstadoSaludLectura.Fallado));
        await db.SaveChangesAsync();
        await servicios.GetRequiredService<ISaludPanelRepository>().EvaluarLoteAsync([panel.Id], ahora,
            new PoliticaSalud(TimeSpan.FromSeconds(30)), CancellationToken.None);
    }
}
