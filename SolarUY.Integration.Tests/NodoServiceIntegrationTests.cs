using FluentValidation;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Nodos;
using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;

namespace SolarUY.Integration.Tests;

public sealed class NodoServiceIntegrationTests
{
    [Fact]
    public async Task Administrador_CreaConsultaEditaYEliminaNodoConInversor()
    {
        var repository = new RepositorioNodosPrueba();
        var parque = repository.AgregarParque();
        var service = CrearServicio(repository, "Administrador");
        var id = await service.CrearAsync(parque.Id, new()
        {
            Nombre = "Nodo uno", UbicacionEnParque = "Norte", CantidadPanelesPrevista = 12,
            MarcaInversor = "Marca", ModeloInversor = "Modelo", PotenciaNominalKW = 10,
            EstadoInversor = EstadoInversor.Encendido, VolcadoRedActivo = true
        });

        var nodo = await service.ObtenerPorIdAsync(id);
        Assert.NotNull(nodo);
        Assert.Equal("Marca", nodo.Inversor.Marca);
        Assert.Equal(EstadoInversor.Encendido, nodo.Inversor.Estado);
        await service.ActualizarAsync(id, new() { Nombre = "Nodo actualizado", UbicacionEnParque = "Sur" });
        Assert.Equal("Nodo actualizado", (await service.ObtenerPorIdAsync(id))!.Nombre);

        await service.CrearAsync(parque.Id, new()
        {
            Nombre = "Nodo dos", UbicacionEnParque = "Este", MarcaInversor = "Marca", ModeloInversor = "Modelo",
            PotenciaNominalKW = 5, EstadoInversor = EstadoInversor.Apagado
        });
        await service.EliminarAsync(id);
        Assert.Null(await service.ObtenerPorIdAsync(id));
    }

    [Theory]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task RolesDeConsulta_NoPuedenModificarPeroPuedenConsultar(string rol)
    {
        var repository = new RepositorioNodosPrueba();
        var parque = repository.AgregarParque();
        var administrador = CrearServicio(repository, "Administrador");
        await administrador.CrearAsync(parque.Id, new()
        {
            Nombre = "Nodo", UbicacionEnParque = "Norte", MarcaInversor = "Marca", ModeloInversor = "Modelo",
            PotenciaNominalKW = 5, EstadoInversor = EstadoInversor.Apagado
        });
        var service = CrearServicio(repository, rol);
        Assert.Single(await service.ListarPorParqueAsync(parque.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CrearAsync(parque.Id, new()));
    }

    [Fact]
    public async Task EliminarUltimoNodo_NoLoPersiste()
    {
        var repository = new RepositorioNodosPrueba();
        var parque = repository.AgregarParque();
        var service = CrearServicio(repository, "Administrador");
        var id = await service.CrearAsync(parque.Id, new()
        {
            Nombre = "Nodo", UbicacionEnParque = "Norte", MarcaInversor = "Marca", ModeloInversor = "Modelo",
            PotenciaNominalKW = 5, EstadoInversor = EstadoInversor.Apagado
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EliminarAsync(id));
        Assert.NotNull(await service.ObtenerPorIdAsync(id));
    }

    [Fact]
    public async Task UsuarioNoAutenticado_NoConsultaNiModifica()
    {
        var repository = new RepositorioNodosPrueba();
        var parque = repository.AgregarParque();
        var service = new NodoService(repository, new UsuarioPrueba("Administrador", false),
            new CrearNodoDtoValidator(), new ActualizarNodoDtoValidator());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListarPorParqueAsync(parque.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CrearAsync(parque.Id, new()));
        Assert.Empty(await repository.ListarPorParqueAsync(parque.Id));
    }

    private static INodoService CrearServicio(RepositorioNodosPrueba repository, string rol) => new NodoService(
        repository, new UsuarioPrueba(rol), new CrearNodoDtoValidator(), new ActualizarNodoDtoValidator());

    private sealed class UsuarioPrueba(string rol, bool autenticado = true) : IUsuarioActual
    {
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UsuarioActualDto(autenticado, "usuario", [rol]));
    }

    private sealed class RepositorioNodosPrueba : INodoRepository
    {
        private readonly Dictionary<Guid, ParqueSolar> _parques = [];
        private readonly Dictionary<Guid, Nodo> _nodos = [];
        public ParqueSolar AgregarParque()
        {
            var parque = new ParqueSolar("Parque", "Salto");
            _parques.Add(parque.Id, parque);
            return parque;
        }
        public Task<ParqueSolar?> ObtenerParqueAsync(Guid parqueId, CancellationToken cancellationToken = default)
            => Task.FromResult(_parques.GetValueOrDefault(parqueId));
        public Task<Nodo?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_nodos.GetValueOrDefault(id));
        public Task<Nodo?> ObtenerParaEliminarAsync(Guid id, CancellationToken cancellationToken = default)
            => ObtenerPorIdAsync(id, cancellationToken);
        public Task<IReadOnlyList<Nodo>> ListarPorParqueAsync(Guid parqueId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Nodo>>(_nodos.Values.Where(nodo => nodo.ParqueId == parqueId).ToArray());
        public Task AgregarAsync(Nodo nodo, CancellationToken cancellationToken = default)
        {
            _nodos.Add(nodo.Id, nodo);
            return Task.CompletedTask;
        }
        public Task ActualizarAsync(Nodo nodo, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EliminarEnCascadaAsync(Nodo nodo, CancellationToken cancellationToken = default)
        {
            _nodos.Remove(nodo.Id);
            return Task.CompletedTask;
        }
    }
}
