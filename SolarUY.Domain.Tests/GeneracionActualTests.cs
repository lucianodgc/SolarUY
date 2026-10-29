using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Domain.Monitoreo;

namespace SolarUY.Domain.Tests;

public sealed class GeneracionActualTests
{
    private static readonly DateTime Instante = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private readonly Nodo _nodo = new(new ParqueSolar("Monitoreo", "Salto"), "Nodo", "Norte");
    private Panel Panel(string serie) => new(_nodo, serie, "Marca", "Modelo", 450, Instante.AddYears(-1), EstadoPanel.Activo);
    private static LecturaPanel Lectura(Panel panel, DateTime fecha, decimal potencia)
        => new(panel, fecha, potencia, EstadoSaludLectura.Sano);

    [Fact]
    public void NodoHistoricoDesconocido_NoSeAtribuyeAlNodoActual()
    {
        var panel = Panel("Legado");
        var lectura = Lectura(panel, Instante, 100);
        // Simular exclusivamente la materialización de una fila legada por el ORM.
        typeof(LecturaPanel).GetProperty(nameof(LecturaPanel.NodoId))!.SetValue(lectura, null);
        typeof(LecturaPanel).GetProperty(nameof(LecturaPanel.Nodo))!.SetValue(lectura, null);
        var resultado = new GeneracionActualService().Calcular([_nodo], [panel], [lectura], Instante, new(60, 2));
        Assert.Null(resultado.Parque.PotenciaW);
        Assert.Null(resultado.Nodos[_nodo.Id].PotenciaW);
        Assert.Equal(DisponibilidadMonitoreo.NodoHistoricoDesconocido, Assert.Single(resultado.Paneles).Disponibilidad);
        Assert.Equal(100m, lectura.PotenciaGeneradaW);
    }

    [Fact]
    public void SumaUnaVezCadaPanelYSoloLaUltimaMuestraSinRetroceder()
    {
        var a = Panel("A"); var b = Panel("B");
        var lectura = Lectura(a, Instante, 100);
        var calculo = new GeneracionActualService().Calcular([_nodo], [a, b, a],
            [lectura, Lectura(a, Instante.AddMinutes(-1), 900), Lectura(b, Instante, 200), lectura],
            Instante.AddSeconds(1), new(60, 2));
        Assert.Equal(300m, calculo.Parque.PotenciaW);
        Assert.Equal(300m, calculo.Nodos[_nodo.Id].PotenciaW);
        Assert.True(calculo.Parque.Completo);
        Assert.Equal(2, calculo.Paneles.Count);
        Assert.Equal(Instante, calculo.InstanteUtc);
    }

    [Fact]
    public void DistingueCeroSinDatosEIntervalosIncompatibles()
    {
        var cero = Panel("Cero"); var ausente = Panel("Ausente"); var anterior = Panel("Anterior");
        var calculo = new GeneracionActualService().Calcular([_nodo], [cero, ausente, anterior],
            [Lectura(cero, Instante, 0), Lectura(anterior, Instante.AddMinutes(-1), 50)],
            Instante.AddSeconds(30), new(60, 2));
        Assert.Equal(0m, calculo.Parque.PotenciaW);
        Assert.False(calculo.Parque.Completo);
        Assert.Equal(1, calculo.Parque.PanelesConDatos);
        Assert.Equal(DisponibilidadMonitoreo.SinDatos, calculo.Paneles.Single(p => p.PanelId == ausente.Id).Disponibilidad);
        Assert.Equal(DisponibilidadMonitoreo.IntervaloIncompatible, calculo.Paneles.Single(p => p.PanelId == anterior.Id).Disponibilidad);
        Assert.Null(calculo.Paneles.Single(p => p.PanelId == anterior.Id).PotenciaW);
    }

    [Fact]
    public void ExpiraEnElLimiteSinNecesitarUnaNuevaLectura()
    {
        var panel = Panel("A"); var lectura = Lectura(panel, Instante, 100);
        var servicio = new GeneracionActualService();
        Assert.Equal(100m, servicio.Calcular([_nodo], [panel], [lectura], Instante.AddSeconds(119), new(60, 2)).Parque.PotenciaW);
        var expirado = servicio.Calcular([_nodo], [panel], [lectura], Instante.AddSeconds(120), new(60, 2));
        Assert.Null(expirado.Parque.PotenciaW);
        Assert.Equal(DisponibilidadMonitoreo.FueraDeVigencia, Assert.Single(expirado.Paneles).Disponibilidad);
        Assert.Equal(Instante, Assert.Single(expirado.Paneles).FechaHoraUtc);
    }

    [Fact]
    public void NoUsaMuestrasFuturasYRechazaReferenciaNoUtc()
    {
        var panel = Panel("A");
        var servicio = new GeneracionActualService();
        var resultado = servicio.Calcular([_nodo], [panel], [Lectura(panel, Instante.AddMinutes(1), 100)], Instante, new(60, 2));
        Assert.Null(resultado.Parque.PotenciaW);
        Assert.Throws<ArgumentException>(() => servicio.Calcular([_nodo], [panel], [], DateTime.SpecifyKind(Instante, DateTimeKind.Unspecified), new(60, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoliticaMonitoreo(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoliticaMonitoreo(60, 0));
    }
}
