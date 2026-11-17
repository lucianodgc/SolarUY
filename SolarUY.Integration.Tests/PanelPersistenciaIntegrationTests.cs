using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Paneles;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class PanelPersistenciaIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    private PanelService Servicio(string rol = "Administrador") => new(new PanelRepository(new Factory(database)),
        new Identidad(rol), new PanelDtoValidator(), new ActualizarPanelDtoValidator());

    [MariaDbHistoricoFact]
    public async Task AltaTrasladoYBajaConservanLecturasYMantenimiento()
    {
        await database.ReiniciarAsync();
        await using (var db = database.CrearContexto())
        {
            var panel = await db.Paneles.SingleAsync(p => p.Id == database.Panel.Id);
            db.TareasMantenimiento.Add(new TareaMantenimiento(panel, "Limpieza", "Prueba de conservación", DateTime.UtcNow, "tecnico-prueba"));
            await db.SaveChangesAsync();
        }
        var service = Servicio();
        var creado = await service.CrearAsync(Datos(database.TercerPanel.NodoId, "Nuevo"));
        Assert.NotNull(await service.ObtenerAsync(creado));
        var edicion = new ActualizarPanelDto
        {
            NodoId = database.TercerPanel.NodoId, NumeroSerie = database.Panel.NumeroSerie,
            Marca = "Marca editada", Modelo = "Modelo", PotenciaNominalW = 450,
            FechaInstalacion = database.Panel.FechaInstalacion, Estado = EstadoPanel.Activo
        };
        await service.ActualizarAsync(database.Panel.Id, edicion);
        Assert.Equal(database.TercerPanel.NodoId, (await service.ObtenerAsync(database.Panel.Id))!.NodoId);
        await service.DarDeBajaAsync(database.Panel.Id);
        Assert.Null(await service.ObtenerAsync(database.Panel.Id));
        await using var verificar = database.CrearContexto();
        Assert.True((await verificar.Paneles.SingleAsync(p => p.Id == database.Panel.Id)).EstaDadoDeBaja);
        Assert.All(await verificar.LecturasPanel.Where(l => l.PanelId == database.Panel.Id).ToArrayAsync(),
            lectura => Assert.Equal(database.Panel.NodoId, lectura.NodoId));
        Assert.True(await verificar.TareasMantenimiento.AnyAsync(t => t.PanelId == database.Panel.Id));
        var inventario = await new InversorInventarioRepository(new Factory(database)).ListarAsync();
        Assert.DoesNotContain(inventario.SelectMany(n => n.Paneles), p => p.Id == database.Panel.Id.ToString("D"));
    }

    [MariaDbHistoricoFact]
    public async Task AltasConcurrentesNoSuperanElUltimoCupoDelNodo()
    {
        await database.ReiniciarAsync();
        await using (var db = database.CrearContexto())
        {
            var nodo = await db.Nodos.SingleAsync(n => n.Id == database.Panel.NodoId);
            nodo.ActualizarDatos(nodo.Nombre, nodo.UbicacionEnParque, 3);
            await db.SaveChangesAsync();
        }
        async Task<bool> Crear(string serie)
        {
            try { await Servicio().CrearAsync(Datos(database.Panel.NodoId, serie)); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var resultados = await Task.WhenAll(Crear("Concurrente-1"), Crear("Concurrente-2"));
        Assert.Equal(1, resultados.Count(exito => exito));
        await using var verificar = database.CrearContexto();
        Assert.Equal(3, await verificar.Paneles.CountAsync(p => p.NodoId == database.Panel.NodoId && !p.EstaDadoDeBaja));
    }

    [MariaDbHistoricoTheory]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task LectoresConsultanPeroNoPersistenEscrituras(string rol)
    {
        await database.ReiniciarAsync();
        var service = Servicio(rol);
        Assert.Equal(4, (await service.ListarAsync()).Count);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CrearAsync(Datos(database.Panel.NodoId, "Denegado")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ActualizarAsync(database.Panel.Id, new()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DarDeBajaAsync(database.Panel.Id));
        await using var verificar = database.CrearContexto();
        Assert.Equal(4, await verificar.Paneles.CountAsync(p => !p.EstaDadoDeBaja));
    }

    private static CrearPanelDto Datos(Guid nodo, string serie) => new()
    {
        NodoId = nodo, NumeroSerie = serie, Marca = "Marca", Modelo = "Modelo", PotenciaNominalW = 450,
        FechaInstalacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), Estado = EstadoPanel.Activo
    };

    private sealed class Identidad(string rol) : IUsuarioActual
    {
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UsuarioActualDto(true, "usuario-prueba", [rol]));
    }

    private sealed class Factory(HistoricoDatabase database) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => database.CrearContexto();
        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
