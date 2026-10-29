using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;

namespace SolarUY.Domain.Tests;

public sealed class InversorTests
{
    [Fact]
    public void Constructor_AsociaInversorAlNodoYConservaDatos()
    {
        var nodo = new Nodo(new ParqueSolar("Parque", "Salto"), "Nodo", "Norte");
        var inversor = new Inversor(nodo, "Marca", "Modelo", 25.5m, EstadoInversor.Encendido, true);
        Assert.Same(inversor, nodo.Inversor);
        Assert.Equal(nodo.Id, inversor.NodoId);
        Assert.Equal(EstadoInversor.Encendido, inversor.Estado);
        Assert.True(inversor.VolcadoRedActivo);
    }

    [Fact]
    public void Constructor_RechazaDatosInvalidosYSegundoInversor()
    {
        var nodo = new Nodo(new ParqueSolar("Parque", "Salto"), "Nodo", "Norte");
        Assert.Throws<ArgumentException>(() => new Inversor(nodo, "", "Modelo", 1, EstadoInversor.Encendido, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Inversor(nodo, "Marca", "Modelo", 0, EstadoInversor.Encendido, false));
        _ = new Inversor(nodo, "Marca", "Modelo", 1, EstadoInversor.Encendido, false);
        Assert.Throws<InvalidOperationException>(() => new Inversor(nodo, "Marca", "Modelo", 1, EstadoInversor.Apagado, false));
    }
}
