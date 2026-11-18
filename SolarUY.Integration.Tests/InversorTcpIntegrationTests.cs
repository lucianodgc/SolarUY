using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Application.Inversores;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Domain.Enums;
using SolarUY.Infrastructure.Inversores;
using SolarUY.Infrastructure.Inversores.Tcp;

namespace SolarUY.Integration.Tests;

public sealed class InversorTcpIntegrationTests
{
    [Fact]
    public async Task ClienteTcp_ConsultaVariacionYOrdenesConEstadoCoherente()
    {
        var opciones = Opciones(ObtenerPuertoLibre());
        var configuracion = new ConfigurationBuilder().Build();
        var motor = new InversorSimulador(configuracion);
        var servidor = new InversorTcpServer(motor, opciones, NullLogger<InversorTcpServer>.Instance);
        using var apagado = new CancellationTokenSource();
        var ejecucionServidor = servidor.EjecutarAsync(apagado.Token);
        var cliente = new InversorTcpClient(opciones, NullLogger<InversorTcpClient>.Instance);

        try
        {
            var inventario = new InventarioNodoInversorDto("nodo-real", "Nodo de prueba TCP", "Parque de prueba TCP", Guid.NewGuid(),
                Enumerable.Range(1, 4).Select(indice => new InventarioPanelInversorDto($"panel-{indice}", $"SERIE-{indice}", 400m, EstadoPanel.Activo)).ToArray());
            await cliente.SincronizarInventarioAsync([inventario]);
            Assert.Equal("nodo-real", Assert.Single(await cliente.ListarNodosAsync()));
            var lecturas = new List<decimal>();
            for (var intento = 0; intento < 20; intento++)
            {
                var estado = await cliente.ObtenerEstadoPanelesAsync("nodo-real");
                Assert.Equal(4, estado.Count);
                Assert.All(estado, panel => Assert.InRange(panel.PotenciaGeneradaW, 360m, 400m));
                lecturas.Add(estado[0].PotenciaGeneradaW);
            }
            Assert.True(lecturas.Distinct().Count() > 1);

            await cliente.DesactivarPanelAsync("nodo-real", "panel-1");
            var paneles = await cliente.ObtenerEstadoPanelesAsync("nodo-real");
            Assert.False(paneles.Single(panel => panel.PanelId == "panel-1").EstaActivo);
            Assert.Equal(0m, paneles.Single(panel => panel.PanelId == "panel-1").PotenciaGeneradaW);
            await cliente.ActivarPanelAsync("nodo-real", "panel-1");
            await cliente.ActivarVolcadoRedAsync();
            Assert.True(await cliente.ObtenerEstadoVolcadoRedAsync());
            await cliente.DesactivarVolcadoRedAsync();
            Assert.False(await cliente.ObtenerEstadoVolcadoRedAsync());

            var medicionesRegistradas = await cliente.ObtenerMedicionesRegistradasAsync(
                [new PanelMedicionDto(Guid.NewGuid(), Guid.NewGuid(), "serie-tcp", 400m, EstadoPanel.Activo)],
                new DateTime(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc));
            Assert.Single(medicionesRegistradas);
            Assert.InRange(medicionesRegistradas[0].PotenciaGeneradaW, 360m, 400m);

            await cliente.EstablecerEstadoEncendidoAsync(false);
            Assert.False(await cliente.ObtenerEstadoEncendidoAsync());
            Assert.Equal(0m, await cliente.ObtenerPotenciaGeneradaAsync("nodo-real"));
            Assert.All(await cliente.ObtenerEstadoPanelesAsync("nodo-real"), panel => Assert.Equal(0m, panel.PotenciaGeneradaW));

            await cliente.EstablecerEstadoEncendidoAsync(true);
            Assert.True(await cliente.ObtenerEstadoEncendidoAsync());
            Assert.InRange(await cliente.ObtenerPotenciaGeneradaAsync("nodo-real"), 1440m, 1600m);
        }
        finally
        {
            apagado.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ejecucionServidor);
        }
    }

    [Fact]
    public async Task ClienteTcp_SimuladorDesconectadoLanzaErrorDeComunicacion()
    {
        var cliente = new InversorTcpClient(Opciones(ObtenerPuertoLibre()), NullLogger<InversorTcpClient>.Instance);

        await Assert.ThrowsAsync<ErrorComunicacionInversorException>(() => cliente.ListarNodosAsync());
    }

    [Fact]
    public async Task ClienteTcp_RespuestaInvalidaLanzaErrorDeComunicacion()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var responder = Task.Run(async () =>
        {
            using var conexion = await listener.AcceptTcpClientAsync();
            var stream = conexion.GetStream();
            await stream.WriteAsync(Encoding.UTF8.GetBytes("esto-no-es-json\n"));
        });
        var cliente = new InversorTcpClient(Opciones(port), NullLogger<InversorTcpClient>.Instance);

        await Assert.ThrowsAsync<ErrorComunicacionInversorException>(() => cliente.ListarNodosAsync());
        await responder;
    }

    [Fact]
    public async Task ClienteTcp_DesactivacionSeConservaEnLecturasYTrasSincronizarInventario()
    {
        var opciones = Opciones(ObtenerPuertoLibre());
        var motor = new InversorSimulador(new ConfigurationBuilder().Build());
        var servidor = new InversorTcpServer(motor, opciones, NullLogger<InversorTcpServer>.Instance);
        using var apagado = new CancellationTokenSource();
        var ejecucion = servidor.EjecutarAsync(apagado.Token);
        var cliente = new InversorTcpClient(opciones, NullLogger<InversorTcpClient>.Instance);
        var nodoId = Guid.NewGuid();
        var panelId = Guid.NewGuid();
        var nodo = nodoId.ToString("D");
        var panel = panelId.ToString("D");
        var inventario = new InventarioNodoInversorDto(nodo, "Nodo", "Parque", Guid.NewGuid(),
            [new InventarioPanelInversorDto(panel, "SERIE-ORDEN", 400m, EstadoPanel.Activo)]);
        var medicion = new PanelMedicionDto(panelId, nodoId, "SERIE-ORDEN", 400m, EstadoPanel.Activo);
        var instante = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        try
        {
            await cliente.SincronizarInventarioAsync([inventario]);
            Assert.InRange((await cliente.ObtenerMedicionesRegistradasAsync([medicion], instante))[0].PotenciaGeneradaW, 360m, 400m);
            await cliente.DesactivarPanelAsync(nodo, panel);
            await cliente.SincronizarInventarioAsync([inventario]);
            Assert.Equal(0m, (await cliente.ObtenerMedicionesRegistradasAsync([medicion], instante))[0].PotenciaGeneradaW);
            Assert.Equal(EstadoSaludLectura.Sano, (await cliente.ObtenerMedicionesRegistradasAsync([medicion], instante))[0].EstadoSalud);
            Assert.False((await cliente.ObtenerEstadoPanelesAsync(nodo))[0].EstaActivo);
            await cliente.ActivarPanelAsync(nodo, panel);
            Assert.InRange((await cliente.ObtenerMedicionesRegistradasAsync([medicion], instante))[0].PotenciaGeneradaW, 360m, 400m);
            await cliente.EstablecerEstadoEncendidoAsync(false);
            await cliente.ActivarPanelAsync(nodo, panel);
            Assert.Equal(0m, (await cliente.ObtenerMedicionesRegistradasAsync([medicion], instante))[0].PotenciaGeneradaW);
            var falladoId = Guid.NewGuid();
            var fallado = new InventarioPanelInversorDto(falladoId.ToString("D"), "FALLADO", 400m, EstadoPanel.Falla);
            await cliente.SincronizarInventarioAsync([inventario with { Paneles = [.. inventario.Paneles, fallado] }]);
            await cliente.DesactivarPanelAsync(nodo, fallado.Id);
            var lecturaFallada = (await cliente.ObtenerMedicionesRegistradasAsync(
                [new PanelMedicionDto(falladoId, nodoId, "FALLADO", 400m, EstadoPanel.Falla)], instante))[0];
            Assert.Equal(0m, lecturaFallada.PotenciaGeneradaW);
            Assert.Equal(EstadoSaludLectura.Fallado, lecturaFallada.EstadoSalud);
        }
        finally
        {
            apagado.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ejecucion);
        }
    }

    private static IOptions<InversorTcpOptions> Opciones(int port)
        => Options.Create(new InversorTcpOptions { Host = "127.0.0.1", Port = port, TimeoutSeconds = 3 });

    private static int ObtenerPuertoLibre()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class RelojSimulador : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
