using SolarUY.Domain.Entities;

namespace SolarUY.Domain.Tests;

public sealed class ParqueSolarTests
{
    [Fact]
    public void QuitarNodo_RechazaEliminarElUltimo()
    {
        var parque = new ParqueSolar("Parque", "Salto");
        var nodo = new Nodo(parque, "Nodo", "Norte");
        Assert.Throws<InvalidOperationException>(() => parque.QuitarNodo(nodo));
    }

    [Fact]
    public void QuitarNodo_PermiteEliminarCuandoQuedaOtro()
    {
        var parque = new ParqueSolar("Parque", "Salto");
        var nodo = new Nodo(parque, "Nodo", "Norte");
        var otroNodo = new Nodo(parque, "Nodo dos", "Sur");
        parque.QuitarNodo(nodo);
        Assert.Equal(new[] { otroNodo }, parque.Nodos);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RechazaNombreNoInformado(string? nombre)
        => Assert.ThrowsAny<ArgumentException>(() => new ParqueSolar(nombre!, "Salto"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RechazaUbicacionNoInformada(string? ubicacion)
        => Assert.ThrowsAny<ArgumentException>(() => new ParqueSolar("Parque", ubicacion!));

    [Fact]
    public void Constructor_RechazaNombreMayorAlLimite()
        => Assert.Throws<ArgumentException>(() => new ParqueSolar(new string('a', 101), "Salto"));

    [Fact]
    public void Constructor_RechazaUbicacionMayorAlLimite()
        => Assert.Throws<ArgumentException>(() => new ParqueSolar("Parque", new string('a', 201)));

    [Fact]
    public void Constructor_AceptaLimitesYCapacidadOpcional()
    {
        var antes = DateTime.UtcNow;
        var parque = new ParqueSolar(new string('a', 100), new string('a', 200));
        Assert.NotEqual(Guid.Empty, parque.Id);
        Assert.Null(parque.CapacidadEstimadaMW);
        Assert.InRange(parque.FechaCreacion, antes, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, parque.FechaCreacion.Kind);
    }

    [Fact]
    public void Constructor_ConservaLosDatosIngresados()
    {
        var parque = new ParqueSolar("Parque Norte", "Salto", 12.5m);
        Assert.Equal("Parque Norte", parque.Nombre);
        Assert.Equal("Salto", parque.Ubicacion);
        Assert.Equal(12.5m, parque.CapacidadEstimadaMW);
    }

    [Fact]
    public void ActualizarDatos_ActualizaConfiguracionYConservaIdentidadYFechaCreacion()
    {
        var parque = new ParqueSolar("Parque Norte", "Salto", 12.5m);
        var id = parque.Id;
        var fechaCreacion = parque.FechaCreacion;

        parque.ActualizarDatos("Parque Sur", "Maldonado", null);

        Assert.Equal("Parque Sur", parque.Nombre);
        Assert.Equal("Maldonado", parque.Ubicacion);
        Assert.Null(parque.CapacidadEstimadaMW);
        Assert.Equal(id, parque.Id);
        Assert.Equal(fechaCreacion, parque.FechaCreacion);
    }

    [Theory]
    [InlineData(null, "Salto")]
    [InlineData("", "Salto")]
    [InlineData("Parque", null)]
    [InlineData("Parque", "")]
    public void ActualizarDatos_RechazaDatosObligatorios(string? nombre, string? ubicacion)
    {
        var parque = new ParqueSolar("Parque Norte", "Salto");

        Assert.ThrowsAny<ArgumentException>(() => parque.ActualizarDatos(nombre!, ubicacion!, null));
    }

    [Fact]
    public void ActualizarDatos_RechazaLongitudesExcesivas()
    {
        var parque = new ParqueSolar("Parque Norte", "Salto");

        Assert.Throws<ArgumentException>(() => parque.ActualizarDatos(new string('a', 101), new string('a', 201), null));
    }
}
