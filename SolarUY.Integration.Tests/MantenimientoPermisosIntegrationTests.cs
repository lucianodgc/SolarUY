using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Application.Mantenimiento;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Integration.Tests;

[Collection("MariaDB histórico")]
public sealed class MantenimientoPermisosIntegrationTests(HistoricoDatabase database) : IClassFixture<HistoricoDatabase>
{
    [MariaDbHistoricoTheory]
    [InlineData("Administrador", "admin", false)]
    [InlineData("Administrador", "tecnico", false)]
    [InlineData("Tecnico", "otro", false)]
    [InlineData("Tecnico", "tecnico", true)]
    [InlineData("Administrador,Tecnico", "otro", false)]
    [InlineData("Administrador,Tecnico", "tecnico", true)]
    [InlineData("Consulta", "tecnico", false)]
    public async Task InicioYFinalizacionExigenTecnicoAsignado(string roles, string actor, bool permitido)
    {
        await database.ReiniciarAsync();
        var id = await CrearTareaAsync();
        var servicio = CrearServicio(roles, actor);
        if (permitido)
        {
            Assert.True((await servicio.ObtenerAsync(id)).PuedeIniciar);
            await servicio.IniciarAsync(id);
            Assert.True((await servicio.ObtenerAsync(id)).PuedeCompletar);
            await servicio.CompletarAsync(id);
        }
        else
        {
            if (roles.Contains("Administrador"))
                Assert.False((await servicio.ObtenerAsync(id)).PuedeIniciar);
            await Assert.ThrowsAsync<MantenimientoException>(() => servicio.IniciarAsync(id));
            await using (var db = database.CrearContexto())
            {
                var tarea = await db.TareasMantenimiento.SingleAsync(t => t.Id == id);
                Assert.Equal(EstadoTareaMantenimiento.Pendiente, tarea.Estado);
                tarea.Iniciar();
                await db.SaveChangesAsync();
            }
            if (roles.Contains("Administrador"))
                Assert.False((await servicio.ObtenerAsync(id)).PuedeCompletar);
            await Assert.ThrowsAsync<MantenimientoException>(() => servicio.CompletarAsync(id));
        }
        await using var verificar = database.CrearContexto();
        var resultado = await verificar.TareasMantenimiento.SingleAsync(t => t.Id == id);
        Assert.Equal(permitido ? EstadoTareaMantenimiento.Completada : EstadoTareaMantenimiento.EnCurso, resultado.Estado);
        Assert.Equal(permitido, resultado.FechaRealizacion.HasValue);
    }

    [MariaDbHistoricoTheory]
    [InlineData("Administrador", "admin")]
    [InlineData("Tecnico", "tecnico")]
    public async Task AdministradorYResponsableConservanCancelacion(string rol, string actor)
    {
        await database.ReiniciarAsync();
        var id = await CrearTareaAsync();
        await CrearServicio(rol, actor).CancelarAsync(id);
        await using var db = database.CrearContexto();
        Assert.Equal(EstadoTareaMantenimiento.Cancelada, (await db.TareasMantenimiento.SingleAsync(t => t.Id == id)).Estado);
    }

    private async Task<Guid> CrearTareaAsync()
    {
        await using var db = database.CrearContexto();
        var panel = await db.Paneles.SingleAsync(p => p.Id == database.Panel.Id);
        var tarea = new TareaMantenimiento(panel, "Limpieza", "Verificar permisos", DateTime.UtcNow, "tecnico");
        db.TareasMantenimiento.Add(tarea);
        await db.SaveChangesAsync();
        return tarea.Id;
    }

    private TareaMantenimientoService CrearServicio(string roles, string actor)
    {
        var fabrica = new Fabrica(database);
        return new(new TareaMantenimientoRepository(fabrica), new PanelRepository(fabrica), new DirectorioNoUtilizado(),
            new Identidad(roles, actor));
    }

    private sealed class Fabrica(HistoricoDatabase database) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => database.CrearContexto();
    }
    private sealed class Identidad(string roles, string actor) : IUsuarioActual
    {
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UsuarioActualDto(true, actor, roles.Split(',')));
    }
    private sealed class DirectorioNoUtilizado : ITecnicoDirectory
    {
        public Task<IReadOnlyList<TecnicoDto>> ListarAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Cambiar estado no debe consultar el directorio.");
        public Task<bool> EsTecnicoHabilitadoAsync(string responsableId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Cambiar estado no debe consultar el directorio.");
    }
}
