using FluentValidation;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Application.ParquesSolares;
using SolarUY.Domain.Entities;

namespace SolarUY.Application.Tests;

public sealed class ParqueSolarServiceTests
{
    [Fact]
    public async Task CrearParqueSolarAsync_RechazaNombreExistenteSinGuardar()
    {
        var context = new TestDbContext { NombreExiste = true };
        var service = CrearService(context);

        await Assert.ThrowsAsync<NombreParqueDuplicadoException>(() => service.CrearParqueSolarAsync(Dto()));

        Assert.Equal("Parque Norte", context.NombreConsultado);
        Assert.Null(context.ParqueAgregado);
        Assert.False(context.Guardado);
    }

    [Fact]
    public async Task CrearParqueSolarAsync_GuardaAntesDeDevolverElId()
    {
        var context = new TestDbContext();
        var id = await CrearService(context).CrearParqueSolarAsync(Dto());

        Assert.True(context.Guardado);
        Assert.Equal(context.ParqueAgregado!.Id, id);
        Assert.Equal("Parque Norte", context.ParqueAgregado.Nombre);
        Assert.Equal("Salto", context.ParqueAgregado.Ubicacion);
        Assert.Null(context.ParqueAgregado.CapacidadEstimadaMW);
    }

    [Theory]
    [InlineData("", "Salto")]
    [InlineData("   ", "Salto")]
    [InlineData("Parque", "")]
    [InlineData("Parque", "   ")]
    public async Task CrearParqueSolarAsync_RechazaDatosObligatoriosAntesDeConsultar(string nombre, string ubicacion)
    {
        var context = new TestDbContext();
        await Assert.ThrowsAsync<ValidationException>(() => CrearService(context).CrearParqueSolarAsync(
            new CrearParqueSolarDto { Nombre = nombre, Ubicacion = ubicacion }));

        Assert.Null(context.NombreConsultado);
        Assert.Null(context.ParqueAgregado);
        Assert.False(context.Guardado);
    }

    [Fact]
    public async Task CrearParqueSolarAsync_RechazaLongitudesExcesivas()
    {
        var context = new TestDbContext();
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CrearService(context).CrearParqueSolarAsync(
            new CrearParqueSolarDto { Nombre = new string('a', 101), Ubicacion = new string('a', 201) }));
        Assert.Equal(2, exception.Errors.Count());
        Assert.False(context.Guardado);
    }

    [Fact]
    public async Task CrearParqueSolarAsync_PropagaConflictoDetectadoAlGuardar()
    {
        var context = new TestDbContext { ConflictoAlGuardar = true };
        await Assert.ThrowsAsync<NombreParqueDuplicadoException>(() => CrearService(context).CrearParqueSolarAsync(Dto()));
        Assert.NotNull(context.ParqueAgregado);
        Assert.False(context.Guardado);
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task ConsultarParquesSolar_PermiteLecturaATodosLosRoles(string rol)
    {
        var context = new TestDbContext();
        var service = CrearService(context, rol);

        await service.ListarParquesSolaresAsync();
        await service.BuscarParquesSolaresAsync("Norte");
        await service.ObtenerParqueSolarPorIdAsync(Guid.NewGuid());

        Assert.Equal(3, context.ConsultasLectura);
    }

    [Theory]
    [InlineData(true, "usuario", "SinPermiso")]
    [InlineData(false, "usuario", "Administrador")]
    [InlineData(true, "", "Administrador")]
    public async Task ConsultarParquesSolar_RechazaIdentidadSinRolPermitido(bool autenticado, string id, string rol)
    {
        var context = new TestDbContext();
        var service = CrearService(context, autenticado, id, rol);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListarParquesSolaresAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.BuscarParquesSolaresAsync(null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ObtenerParqueSolarPorIdAsync(Guid.NewGuid()));

        Assert.Equal(0, context.ConsultasLectura);
    }

    [Theory]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task EscribirParquesSolar_SeDeniegaAntesDePersistirSiNoEsAdministrador(string rol)
    {
        var context = new TestDbContext();
        var service = CrearService(context, rol);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CrearParqueSolarAsync(Dto()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ActualizarParqueSolarAsync(
            Guid.NewGuid(), new ActualizarParqueSolarDto { Nombre = "Parque Norte", Ubicacion = "Salto" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EliminarParqueSolarAsync(Guid.NewGuid()));

        Assert.Null(context.ParqueAgregado);
        Assert.False(context.Guardado);
        Assert.Equal(0, context.ConsultasLectura);
    }

    private static ParqueSolarService CrearService(TestDbContext context, string rol = "Administrador")
        => CrearService(context, true, "usuario-prueba", rol);

    private static ParqueSolarService CrearService(TestDbContext context, bool autenticado, string id, string rol)
        => new(context, new CrearParqueSolarDtoValidator(), new ActualizarParqueSolarDtoValidator(),
            new TestUsuarioActual(autenticado, id, rol));

    private sealed class TestUsuarioActual(bool autenticado, string id, string rol) : IUsuarioActual
    {
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UsuarioActualDto(autenticado, id, new[] { rol }));
    }

    private static CrearParqueSolarDto Dto() => new() { Nombre = "Parque Norte", Ubicacion = "Salto" };

    private sealed class TestDbContext : IApplicationDbContext
    {
        public bool NombreExiste { get; init; }
        public bool ConflictoAlGuardar { get; init; }
        public string? NombreConsultado { get; private set; }
        public ParqueSolar? ParqueAgregado { get; private set; }
        public bool Guardado { get; private set; }
        public int ConsultasLectura { get; private set; }

        public Task<bool> ExisteNombreParqueSolarAsync(
            string nombre,
            Guid? parqueIdExcluido = null,
            CancellationToken cancellationToken = default)
        {
            NombreConsultado = nombre;
            return Task.FromResult(NombreExiste);
        }

        public Task<ParqueSolar?> ObtenerParqueSolarPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            ConsultasLectura++;
            return Task.FromResult<ParqueSolar?>(null);
        }

        public void AgregarParqueSolar(ParqueSolar parque) => ParqueAgregado = parque;

        public void ActualizarParqueSolar(ParqueSolar parque) { }

        public Task EliminarParqueSolarEnCascadaAsync(
            ParqueSolar parque,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Assert.NotNull(ParqueAgregado);
            if (ConflictoAlGuardar) throw new NombreParqueDuplicadoException();
            Guardado = true;
            return Task.FromResult(1);
        }

        public Task<IReadOnlyList<ParqueSolar>> ListarParquesSolaresAsync(CancellationToken cancellationToken = default)
        {
            ConsultasLectura++;
            return Task.FromResult<IReadOnlyList<ParqueSolar>>(Array.Empty<ParqueSolar>());
        }

        public Task<IReadOnlyList<ParqueSolar>> BuscarParquesSolaresAsync(
            string? criterio,
            CancellationToken cancellationToken = default)
        {
            ConsultasLectura++;
            return Task.FromResult<IReadOnlyList<ParqueSolar>>(Array.Empty<ParqueSolar>());
        }
    }
}
