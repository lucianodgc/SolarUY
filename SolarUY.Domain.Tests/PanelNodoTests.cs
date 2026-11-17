using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;

namespace SolarUY.Domain.Tests;

public sealed class PanelNodoTests
{
    private static Nodo CrearNodo() => new(new ParqueSolar("Parque", "Salto"), "Nodo", "Norte", 4);

    [Fact]
    public void Nodo_ConservaRelacionYDatos()
    {
        var nodo = CrearNodo();
        Assert.NotEqual(Guid.Empty, nodo.Id);
        Assert.Equal(nodo.Parque.Id, nodo.ParqueId);
        Assert.Equal("Nodo", nodo.Nombre);
        Assert.Equal("Norte", nodo.UbicacionEnParque);
        Assert.Equal(4, nodo.CantidadPanelesPrevista);
    }

    [Fact]
    public void Nodo_RechazaRelacionYTextosAusentes()
    {
        Assert.Throws<ArgumentNullException>(() => new Nodo(null!, "Nodo", "Norte"));
        Assert.ThrowsAny<ArgumentException>(() => new Nodo(CrearNodo().Parque, "", "Norte"));
        Assert.ThrowsAny<ArgumentException>(() => new Nodo(CrearNodo().Parque, "Nodo", " "));
    }

    [Fact]
    public void Nodo_ActualizaSusTresDatos()
    {
        var nodo = CrearNodo();
        nodo.ActualizarDatos("Nodo dos", "Sur", null);
        Assert.Equal("Nodo dos", nodo.Nombre);
        Assert.Equal("Sur", nodo.UbicacionEnParque);
        Assert.Null(nodo.CantidadPanelesPrevista);
    }

    [Fact]
    public void Panel_ConservaRelacionDatosYEstado()
    {
        var nodo = CrearNodo();
        var fecha = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var panel = new Panel(nodo, "Serie", "Marca", "Modelo", 450, fecha, EstadoPanel.Activo);
        Assert.NotEqual(Guid.Empty, panel.Id);
        Assert.Same(nodo, panel.Nodo);
        Assert.Equal(nodo.Id, panel.NodoId);
        Assert.Equal("Serie", panel.NumeroSerie);
        Assert.Equal("Marca", panel.Marca);
        Assert.Equal("Modelo", panel.Modelo);
        Assert.Equal(450, panel.PotenciaNominalW);
        Assert.Equal(fecha, panel.FechaInstalacion);
        Assert.Equal(EstadoPanel.Activo, panel.Estado);
    }

    [Fact]
    public void Panel_RechazaRelacionTextosFechaYEstadoInvalidos()
    {
        var nodo = CrearNodo();
        var fecha = DateTime.UtcNow;
        Assert.Throws<ArgumentNullException>(() => new Panel(null!, "Serie", "Marca", "Modelo", 450, fecha, EstadoPanel.Activo));
        Assert.ThrowsAny<ArgumentException>(() => new Panel(nodo, "", "Marca", "Modelo", 450, fecha, EstadoPanel.Activo));
        Assert.ThrowsAny<ArgumentException>(() => new Panel(nodo, "Serie", "", "Modelo", 450, fecha, EstadoPanel.Activo));
        Assert.ThrowsAny<ArgumentException>(() => new Panel(nodo, "Serie", "Marca", "", 450, fecha, EstadoPanel.Activo));
        Assert.Throws<ArgumentException>(() => new Panel(nodo, "Serie", "Marca", "Modelo", 450, default, EstadoPanel.Activo));
        Assert.Throws<ArgumentException>(() => new Panel(nodo, "Serie", "Marca", "Modelo", 450, DateTime.SpecifyKind(fecha, DateTimeKind.Unspecified), EstadoPanel.Activo));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Panel(nodo, "Serie", "Marca", "Modelo", 450, fecha, (EstadoPanel)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Panel(nodo, "Serie", "Marca", "Modelo", 0, fecha, EstadoPanel.Activo));
    }

    [Fact]
    public void Panel_ActualizaTrasladaYDaDeBajaSinPerderIdentidad()
    {
        var origen = CrearNodo();
        var destino = new Nodo(origen.Parque, "Nodo destino", "Sur", 2);
        var panel = new Panel(origen, "Serie", "Marca", "Modelo", 450, DateTime.UtcNow, EstadoPanel.Activo);
        var id = panel.Id;
        panel.Actualizar("Serie 2", "Marca 2", "Modelo 2", 500, DateTime.UtcNow, EstadoPanel.Falla);
        panel.TrasladarANodo(destino);
        panel.DarDeBaja();
        Assert.Equal(id, panel.Id);
        Assert.Equal(destino.Id, panel.NodoId);
        Assert.Equal(EstadoPanel.Falla, panel.Estado);
        Assert.True(panel.EstaDadoDeBaja);
        Assert.Throws<InvalidOperationException>(() => panel.TrasladarANodo(origen));
    }

    [Fact]
    public void Nodo_RechazaCantidadSuperiorALaCapacidad()
    {
        var nodo = CrearNodo();
        nodo.ValidarCapacidadPaneles(4);
        Assert.Throws<InvalidOperationException>(() => nodo.ValidarCapacidadPaneles(5));
    }
}
