using SolarUY.Domain.Entities;

namespace SolarUY.Domain.Tests;

public sealed class ParqueSolarTests
{
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
}
