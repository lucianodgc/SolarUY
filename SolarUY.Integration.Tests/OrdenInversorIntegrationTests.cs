using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Application.Inversores;
using SolarUY.Domain.Entities;
using SolarUY.Infrastructure.Inversores;
using SolarUY.Infrastructure.Inversores.Tcp;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class OrdenInversorIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    private sealed class Factory(HistoricoDatabase database) : IDbContextFactory<ApplicationDbContext>
    { public ApplicationDbContext CreateDbContext() => database.CrearContexto(); }
    private sealed class Actor(bool autenticado, string? sub, params string[] roles) : IUsuarioActual
    {
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UsuarioActualDto(autenticado, "fallback", roles, sub));
    }
    private static Actor Admin() => new(true, "sub-keycloak", "Administrador");
    private IntentoOrdenInversorRepository Repository(IOptions<InversorTcpOptions> opciones) => new(new Factory(database), opciones);
    private OrdenInversorService Servicio(IInversorClient cliente, IOptions<InversorTcpOptions> opciones, IUsuarioActual? actor = null,
        IIntentoOrdenInversorRepository? repository = null)
        => new(cliente, new InversorInventarioRepository(new Factory(database)), actor ?? Admin(), repository ?? Repository(opciones),
            TimeProvider.System, new OrdenInversorOptions());

    [MariaDbHistoricoFact]
    public async Task MigracionIncrementalConservaInventarioYLecturasSinInventarAuditoria()
    {
        await database.ReiniciarAsync(); await using var db = database.CrearContexto();
        var lecturas = await db.LecturasPanel.AsNoTracking().OrderBy(l => l.Id).Select(l => new { l.Id, l.PanelId, l.PotenciaGeneradaW, l.FechaHora }).ToArrayAsync();
        var paneles = await db.Paneles.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261112035214_AddEstadoSaludAlertas");
        try { await db.Database.MigrateAsync(); }
        finally { await db.Database.MigrateAsync(); }
        Assert.Equal(lecturas, await db.LecturasPanel.AsNoTracking().OrderBy(l => l.Id).Select(l => new { l.Id, l.PanelId, l.PotenciaGeneradaW, l.FechaHora }).ToArrayAsync());
        Assert.Equal(paneles, await db.Paneles.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync());
        Assert.Empty(await db.Set<IntentoOrdenInversor>().ToArrayAsync());
    }

    [MariaDbHistoricoFact]
    public async Task CuatroOrdenesAuditanYCorrelacionNoReenviaNiCambiaDestino()
    {
        await database.ReiniciarAsync(); await using var tcp = new Servidor();
        var servicio = Servicio(tcp.Cliente, tcp.Opciones);
        var nodo = database.Panel.NodoId.ToString("D"); var panel = database.Panel.Id.ToString("D");
        var solicitud = new SolicitudOrdenInversorDto(Guid.NewGuid(), AccionOrdenInversor.DesactivarPanel, nodo, panel);
        var resultado = await servicio.EjecutarAsync(solicitud);
        Assert.Equal(EstadoOrdenInversor.Confirmada, resultado.Estado); Assert.True(resultado.AuditoriaCompleta);
        Assert.False((await tcp.Cliente.ObtenerEstadoPanelesAsync(nodo)).Single(p => p.PanelId == panel).EstaActivo);
        Assert.Equal(resultado, await Servicio(tcp.Cliente, tcp.Opciones).EjecutarAsync(solicitud));
        Assert.Equal("correlacion-incompatible", (await servicio.EjecutarAsync(solicitud with { Accion = AccionOrdenInversor.ActivarPanel })).Codigo);
        foreach (var accion in new[] { AccionOrdenInversor.ActivarPanel, AccionOrdenInversor.ActivarVolcadoRed, AccionOrdenInversor.DesactivarVolcadoRed })
        {
            var esPanel = accion == AccionOrdenInversor.ActivarPanel;
            Assert.Equal(EstadoOrdenInversor.Confirmada, (await servicio.EjecutarAsync(new(Guid.NewGuid(), accion,
                esPanel ? nodo : null, esPanel ? panel : null))).Estado);
        }
        Assert.False(await tcp.Cliente.ObtenerEstadoVolcadoRedAsync());
        Assert.Equal(EstadoOrdenInversor.Confirmada, (await servicio.EjecutarAsync(new(Guid.NewGuid(), AccionOrdenInversor.DesactivarVolcadoRed))).Estado);
        await using var db = database.CrearContexto();
        var intentos = await db.Set<IntentoOrdenInversor>().AsNoTracking().ToArrayAsync();
        Assert.Equal(5, intentos.Length);
        Assert.All(intentos, i => { Assert.Equal("sub-keycloak", i.ActorSub); Assert.Equal(DateTimeKind.Utc, i.SolicitadaUtc.Kind); Assert.Equal(EstadoOrdenInversor.Confirmada, i.Estado); });
        Assert.Equal(database.Panel.Id, intentos.Single(i => i.Id == resultado.IntentoId).PanelValidado);
    }

    [MariaDbHistoricoTheory]
    [InlineData(false, null, "Administrador", false)]
    [InlineData(true, null, "Administrador", false)]
    [InlineData(true, "sub", "Tecnico", false)]
    [InlineData(true, "sub", "Consulta", false)]
    [InlineData(true, "sub", "Administrador", true)]
    public async Task AutorizacionEfectivaYSubDelServidor(bool autenticado, string? sub, string rol, bool permite)
    {
        await database.ReiniciarAsync(); await using var tcp = new Servidor();
        var resultado = await Servicio(tcp.Cliente, tcp.Opciones, new Actor(autenticado, sub, rol, "Consulta"))
            .EjecutarAsync(new(Guid.NewGuid(), AccionOrdenInversor.ActivarVolcadoRed));
        Assert.Equal(permite ? EstadoOrdenInversor.Confirmada : EstadoOrdenInversor.Rechazada, resultado.Estado);
        Assert.Equal(permite, await tcp.Cliente.ObtenerEstadoVolcadoRedAsync());
        await using var db = database.CrearContexto(); var intento = await db.Set<IntentoOrdenInversor>().SingleAsync();
        Assert.Equal(sub, intento.ActorSub); Assert.NotEqual("fallback", intento.ActorSub);
    }

    [MariaDbHistoricoFact]
    public async Task BloqueoEntreRepositoriosRechazaOpuestaYRecuperaPendienteSinEnviar()
    {
        await database.ReiniciarAsync(); await using var tcp = new Servidor(); var repo = Repository(tcp.Opciones);
        await using (var bloqueo = await repo.IntentarBloqueoAsync(default))
        {
            Assert.NotNull(bloqueo);
            var resultado = await Servicio(tcp.Cliente, tcp.Opciones).EjecutarAsync(new(Guid.NewGuid(), AccionOrdenInversor.ActivarVolcadoRed));
            Assert.Equal("inversor-ocupado", resultado.Codigo);
            Assert.False(await tcp.Cliente.ObtenerEstadoVolcadoRedAsync());
        }
        var pendiente = new IntentoOrdenInversor(Guid.NewGuid(), "sub-keycloak", true, true, AccionOrdenInversor.ActivarVolcadoRed, null, null, DateTime.UtcNow);
        await repo.GuardarAsync(pendiente, default);
        var recuperado = await Servicio(tcp.Cliente, tcp.Opciones).EjecutarAsync(new(pendiente.Correlacion, pendiente.Accion));
        Assert.Equal(EstadoOrdenInversor.Indeterminada, recuperado.Estado);
        Assert.False(await tcp.Cliente.ObtenerEstadoVolcadoRedAsync());
    }

    [MariaDbHistoricoFact]
    public async Task FallosAuditoriaInicialYFinalNuncaReenvian()
    {
        await database.ReiniciarAsync(); await using var tcp = new Servidor(); var repo = Repository(tcp.Opciones);
        var fallaInicial = new FalloRepository(repo, true);
        var resultado = await Servicio(tcp.Cliente, tcp.Opciones, repository: fallaInicial).EjecutarAsync(new(Guid.NewGuid(), AccionOrdenInversor.ActivarVolcadoRed));
        Assert.False(resultado.AuditoriaCompleta); Assert.False(await tcp.Cliente.ObtenerEstadoVolcadoRedAsync());
        var solicitud = new SolicitudOrdenInversorDto(Guid.NewGuid(), AccionOrdenInversor.ActivarVolcadoRed);
        resultado = await Servicio(tcp.Cliente, tcp.Opciones, repository: new FalloRepository(repo, false)).EjecutarAsync(solicitud);
        Assert.Equal(EstadoOrdenInversor.Confirmada, resultado.Estado); Assert.False(resultado.AuditoriaCompleta);
        Assert.True(await tcp.Cliente.ObtenerEstadoVolcadoRedAsync());
        await tcp.Cliente.DesactivarVolcadoRedAsync();
        var repetida = await Servicio(tcp.Cliente, tcp.Opciones).EjecutarAsync(solicitud);
        Assert.Equal(EstadoOrdenInversor.Indeterminada, repetida.Estado);
        Assert.False(await tcp.Cliente.ObtenerEstadoVolcadoRedAsync());
    }

    [MariaDbHistoricoTheory]
    [InlineData("{\"exito\":true,\"resultado\":false}\n", false, EstadoOrdenInversor.Rechazada)]
    [InlineData("{\"exito\":false,\"codigoError\":\"entrada-invalida\"}\n", false, EstadoOrdenInversor.Rechazada)]
    [InlineData("{\"exito\":true}\n", false, EstadoOrdenInversor.Indeterminada)]
    [InlineData("", false, EstadoOrdenInversor.Indeterminada)]
    [InlineData("timeout", false, EstadoOrdenInversor.Indeterminada)]
    [InlineData("", true, EstadoOrdenInversor.Indeterminada)]
    public async Task AuditoriaDistingueRechazoPerdidaDeConfirmacionYCancelacion(string respuesta, bool cancelar, EstadoOrdenInversor esperado)
    {
        await database.ReiniciarAsync();
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var opciones = Options.Create(new InversorTcpOptions { Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port, TimeoutSeconds = 1 });
        using var cancelacion = new CancellationTokenSource();
        var solicitudes = new List<string>();
        var atender = Task.Run(async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                using var conexion = await listener.AcceptTcpClientAsync();
                using var lector = new StreamReader(conexion.GetStream(), leaveOpen: true);
                solicitudes.Add((await lector.ReadLineAsync())!);
                if (i == 0) await conexion.GetStream().WriteAsync(Encoding.UTF8.GetBytes("{\"exito\":true,\"resultado\":true}\n"));
                else if (cancelar) { cancelacion.Cancel(); await Task.Delay(100); }
                else if (respuesta == "timeout") await Task.Delay(1500);
                else await conexion.GetStream().WriteAsync(Encoding.UTF8.GetBytes(respuesta));
            }
        });
        var cliente = new InversorTcpClient(opciones, NullLogger<InversorTcpClient>.Instance);
        var solicitud = new SolicitudOrdenInversorDto(Guid.NewGuid(), AccionOrdenInversor.ActivarVolcadoRed);
        var resultado = await Servicio(cliente, opciones).EjecutarAsync(solicitud, cancelacion.Token);
        await atender;
        Assert.Equal(esperado, resultado.Estado); Assert.True(resultado.AuditoriaCompleta);
        Assert.Equal(2, solicitudes.Count);
        Assert.Equal(resultado, await Servicio(cliente, opciones).EjecutarAsync(solicitud));
        await using var db = database.CrearContexto();
        Assert.Equal(esperado, (await db.Set<IntentoOrdenInversor>().SingleAsync()).Estado);
    }

    [MariaDbHistoricoFact]
    public async Task DestinoInexistenteOTrasladadoNoSeEnviaYConservaSolicitud()
    {
        await database.ReiniciarAsync(); await using var tcp = new Servidor();
        var solicitud = new SolicitudOrdenInversorDto(Guid.NewGuid(), AccionOrdenInversor.DesactivarPanel,
            database.PanelAjeno.NodoId.ToString("D"), database.Panel.Id.ToString("D"));
        var resultado = await Servicio(tcp.Cliente, tcp.Opciones).EjecutarAsync(solicitud);
        Assert.Equal("destino-no-vigente", resultado.Codigo);
        Assert.Empty(await tcp.Cliente.ListarNodosAsync());
        await using var db = database.CrearContexto(); var intento = await db.Set<IntentoOrdenInversor>().SingleAsync();
        Assert.Equal(solicitud.NodoId, intento.NodoSolicitado); Assert.Equal(solicitud.PanelId, intento.PanelSolicitado);
        Assert.Null(intento.PanelValidado);
    }

    private sealed class FalloRepository(IIntentoOrdenInversorRepository real, bool inicial) : IIntentoOrdenInversorRepository
    {
        public Task<IAsyncDisposable?> IntentarBloqueoAsync(CancellationToken ct) => real.IntentarBloqueoAsync(ct);
        public Task<IntentoOrdenInversor?> BuscarAsync(string sub, Guid correlacion, CancellationToken ct) => real.BuscarAsync(sub, correlacion, ct);
        public Task RecuperarPendientesAsync(DateTime ahora, CancellationToken ct) => real.RecuperarPendientesAsync(ahora, ct);
        public Task GuardarAsync(IntentoOrdenInversor intento, CancellationToken ct)
            => inicial || intento.Estado != EstadoOrdenInversor.Pendiente ? throw new IOException("Fallo de prueba") : real.GuardarAsync(intento, ct);
    }

    [Theory]
    [InlineData("{\"exito\":true,\"resultado\":false}\n", true)]
    [InlineData("{\"exito\":true}\n", false)]
    [InlineData("{\"resultado\":true}\n", false)]
    [InlineData("{\"exito\":true,\"resultado\":\"true\"}\n", false)]
    [InlineData("invalido\n", false)]
    [InlineData("", false)]
    public async Task TcpNoConfirmaRespuestasFalsasInvalidasOConexionPerdida(string respuesta, bool rechazo)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var opciones = Options.Create(new InversorTcpOptions { Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port, TimeoutSeconds = 2 });
        var atender = Task.Run(async () =>
        {
            using var conexion = await listener.AcceptTcpClientAsync();
            using var lector = new StreamReader(conexion.GetStream(), leaveOpen: true); await lector.ReadLineAsync();
            await conexion.GetStream().WriteAsync(Encoding.UTF8.GetBytes(respuesta));
        });
        var cliente = new InversorTcpClient(opciones, NullLogger<InversorTcpClient>.Instance);
        if (rechazo) await Assert.ThrowsAsync<OrdenInversorRechazadaException>(() => cliente.ActivarVolcadoRedAsync());
        else await Assert.ThrowsAsync<ErrorComunicacionInversorException>(() => cliente.ActivarVolcadoRedAsync());
        await atender;
    }

    private sealed class Servidor : IAsyncDisposable
    {
        private readonly CancellationTokenSource _fin = new(); private readonly Task _ejecucion;
        public IOptions<InversorTcpOptions> Opciones { get; }
        public InversorTcpClient Cliente { get; }
        public Servidor()
        {
            using var puerto = new TcpListener(IPAddress.Loopback, 0); puerto.Start();
            var numero = ((IPEndPoint)puerto.LocalEndpoint).Port; puerto.Stop();
            Opciones = Options.Create(new InversorTcpOptions { Host = "127.0.0.1", Port = numero, TimeoutSeconds = 2 });
            var motor = new InversorSimulador(new ConfigurationBuilder().Build());
            _ejecucion = new InversorTcpServer(motor, Opciones, NullLogger<InversorTcpServer>.Instance).EjecutarAsync(_fin.Token);
            Cliente = new InversorTcpClient(Opciones, NullLogger<InversorTcpClient>.Instance);
        }
        public async ValueTask DisposeAsync()
        { _fin.Cancel(); try { await _ejecucion; } catch (OperationCanceledException) { } _fin.Dispose(); }
    }
}
