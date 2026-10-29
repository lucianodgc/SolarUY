using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;

namespace SolarUY.Domain.Tests;

public sealed class LecturaPanelTests
{
    private static Panel CrearPanel() => new(new Nodo(new ParqueSolar("Parque", "Salto"), "Nodo", "Norte"),
        "Serie", "Marca", "Modelo", 450m, DateTime.UtcNow, EstadoPanel.Activo);

    [Theory]
    [InlineData("0", EstadoSaludLectura.Sano)]
    [InlineData("450.123456", EstadoSaludLectura.Fallado)]
    [InlineData("999999999999.999999", EstadoSaludLectura.Desconocido)]
    public void Constructor_ConservaDatosYGeneraIdentidad(string valor, EstadoSaludLectura salud)
    {
        var potencia = decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture);
        var panel = CrearPanel();
        var instante = new DateTime(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);
        var lectura = new LecturaPanel(panel, instante, potencia, salud);
        Assert.NotEqual(Guid.Empty, lectura.Id);
        Assert.NotEqual(lectura.Id, new LecturaPanel(panel, instante, potencia, salud).Id);
        Assert.Same(panel, lectura.Panel);
        Assert.Equal(panel.Id, lectura.PanelId);
        Assert.Equal(panel.NodoId, lectura.NodoId);
        Assert.Same(panel.Nodo, lectura.Nodo);
        Assert.Equal(instante, lectura.FechaHora);
        Assert.Equal(DateTimeKind.Utc, lectura.FechaHora.Kind);
        Assert.Equal(potencia, lectura.PotenciaGeneradaW);
        Assert.Equal(salud, lectura.EstadoSalud);
    }

    [Fact]
    public void Constructor_NormalizaMicrosegundosYConservaSaludHistorica()
    {
        var panel = CrearPanel();
        var instante = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        var lectura = new LecturaPanel(panel, instante, 12.5m, EstadoSaludLectura.Fallado);
        Assert.Equal(instante.Ticks - instante.Ticks % 10, lectura.FechaHora.Ticks);
        Assert.Equal(EstadoPanel.Activo, panel.Estado);
        Assert.Equal(EstadoSaludLectura.Fallado, lectura.EstadoSalud);
        Assert.Equal(panel.Nodo.Id, lectura.NodoId);
    }

    [Fact]
    public void Constructor_RechazaRelacionFechaEstadoYPotenciaInvalidos()
    {
        var panel = CrearPanel();
        Assert.Throws<ArgumentNullException>(() => new LecturaPanel(null!, DateTime.UtcNow, 0, EstadoSaludLectura.Sano));
        foreach (var fecha in new[] { default(DateTime), new DateTime(1, DateTimeKind.Utc), DateTime.Now, DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) })
            Assert.Throws<ArgumentException>(() => new LecturaPanel(panel, fecha, 0, EstadoSaludLectura.Sano));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LecturaPanel(panel, DateTime.UtcNow, 0, (EstadoSaludLectura)99));
        foreach (var potencia in new[] { -1m, 0.0000001m, 1000000000000m })
            Assert.Throws<ArgumentOutOfRangeException>(() => new LecturaPanel(panel, DateTime.UtcNow, potencia, EstadoSaludLectura.Sano));
    }
}
