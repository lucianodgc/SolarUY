using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;
using SolarUY.Domain.Salud;
using EstadoSaludPanel = SolarUY.Domain.Salud.EstadoSaludPanel;

namespace SolarUY.Domain.Tests;

public sealed class SaludPanelTests
{
    private static readonly DateTime Ahora = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
    private static Panel Panel() => new(new Nodo(new ParqueSolar("Parque", "Salto"), "Nodo", "Norte"), "S", "M", "X", 100, Ahora.AddDays(-1), EstadoPanel.Activo);
    private static ResultadoSalud Resultado(EstadoSaludPanel estado, DateTime? evidencia = null) => new(estado, true, "Prueba", evidencia ?? Ahora);

    [Theory]
    [InlineData(49, EstadoSaludPanel.Falla)]
    [InlineData(50, EstadoSaludPanel.Degradado)]
    [InlineData(79, EstadoSaludPanel.Degradado)]
    [InlineData(80, EstadoSaludPanel.Normal)]
    public void Potencia_RespetaLimitesSoloConGeneracionEsperada(int potencia, EstadoSaludPanel esperado)
    {
        var servicio = new EvaluacionSaludService(); var politica = new PoliticaSalud(TimeSpan.FromSeconds(90), evaluarPotenciaBaja: true);
        Assert.Equal(esperado, servicio.Evaluar(new(Ahora, potencia, EstadoSaludLectura.Sano, GeneracionEsperada: true), 100, Ahora, Ahora, politica).Estado);
        Assert.Equal(EstadoSaludPanel.Normal, servicio.Evaluar(new(Ahora, 0, EstadoSaludLectura.Sano, GeneracionEsperada: false), 100, Ahora, Ahora, politica).Estado);
    }
    [Fact]
    public void FallaExplicitaPrevalecePeroLaEvidenciaVencidaIndicaDesconexion()
    {
        var s = new EvaluacionSaludService(); var p = new PoliticaSalud(TimeSpan.FromSeconds(90));
        Assert.Equal(EstadoSaludPanel.Falla, s.Evaluar(new(Ahora, 0, EstadoSaludLectura.Fallado), 100, Ahora, Ahora, p).Estado);
        Assert.Equal(EstadoSaludPanel.Desconectado, s.Evaluar(new(Ahora, 0, EstadoSaludLectura.Fallado), 100, Ahora, Ahora.AddSeconds(90), p).Estado);
        Assert.Equal(EstadoSaludPanel.Normal, s.Evaluar(new(Ahora, 0, EstadoSaludLectura.Sano), 100, Ahora, Ahora, p).Estado);
    }
    [Fact]
    public void AusenciaEsperaDesdePrimeraObservacionYDesconocidoNoSeInventaNormal()
    {
        var s = new EvaluacionSaludService(); var p = new PoliticaSalud(TimeSpan.FromSeconds(90));
        Assert.False(s.Evaluar(null, 100, Ahora, Ahora.AddSeconds(89), p).Suficiente);
        Assert.Equal(EstadoSaludPanel.Desconectado, s.Evaluar(null, 100, Ahora, Ahora.AddSeconds(90), p).Estado);
        Assert.False(s.Evaluar(new(Ahora, 0, EstadoSaludLectura.Desconocido), 100, Ahora, Ahora, p).Suficiente);
        Assert.False(s.Evaluar(new(Ahora.AddSeconds(1), 0, EstadoSaludLectura.Sano), 100, Ahora, Ahora, p).Suficiente);
        Assert.False(s.Evaluar(new(Ahora, 100, EstadoSaludLectura.Sano, false), 100, Ahora, Ahora, p).Suficiente);
    }
    [Fact]
    public void TransicionesNoSeDuplicanNiRetrocedenYPrimeraTieneAnteriorNulo()
    {
        var salud = new SaludPanel(Panel(), Ahora);
        var primero = salud.Aplicar(Resultado(EstadoSaludPanel.Falla), Ahora, salud.Panel.NodoId);
        Assert.Null(primero!.EstadoAnterior); Assert.Equal(1, primero.Version);
        Assert.Null(salud.Aplicar(Resultado(EstadoSaludPanel.Falla), Ahora.AddSeconds(1), salud.Panel.NodoId));
        Assert.Null(salud.Aplicar(Resultado(EstadoSaludPanel.Normal), Ahora, salud.Panel.NodoId));
        Assert.Equal(EstadoSaludPanel.Falla, salud.Estado);
        salud.Aplicar(Resultado(EstadoSaludPanel.Desconectado), Ahora.AddSeconds(90), salud.Panel.NodoId);
        Assert.NotNull(salud.Aplicar(Resultado(EstadoSaludPanel.Normal, Ahora.AddSeconds(89)), Ahora.AddSeconds(91), salud.Panel.NodoId));
        Assert.Equal(3, salud.Version);
    }
    [Fact]
    public void InsuficienciaConservaUltimoConocidoSinAfirmarVigencia()
    {
        var salud = new SaludPanel(Panel(), Ahora);
        salud.Aplicar(Resultado(EstadoSaludPanel.Falla), Ahora, salud.Panel.NodoId);
        Assert.Null(salud.Aplicar(new(null, false, "Insuficiente", Ahora), Ahora.AddSeconds(1), salud.Panel.NodoId));
        Assert.Equal(EstadoSaludPanel.Falla, salud.Estado); Assert.False(salud.Suficiente); Assert.Equal(1, salud.Version);
    }
    [Fact]
    public void EvidenciaInsuficienteNoBorraElOrdenYUnTrasladoIniciaOtraReferencia()
    {
        var salud = new SaludPanel(Panel(), Ahora);
        salud.Aplicar(Resultado(EstadoSaludPanel.Falla), Ahora, salud.Panel.NodoId);
        salud.Aplicar(new(null, false, "Sin evidencia nueva", null), Ahora.AddSeconds(1), salud.Panel.NodoId);
        Assert.Equal(Ahora, salud.EvidenciaUtc);
        Assert.Null(salud.Aplicar(Resultado(EstadoSaludPanel.Normal, Ahora.AddSeconds(-1)), Ahora.AddSeconds(2), salud.Panel.NodoId));
        Assert.Equal(EstadoSaludPanel.Falla, salud.Estado);
        var otroNodo = Guid.NewGuid();
        salud.Aplicar(new(null, false, "Esperando nodo nuevo", null), Ahora.AddSeconds(3), otroNodo);
        Assert.Equal(Ahora.AddSeconds(3), salud.PrimeraObservacionUtc);
        Assert.Null(salud.EvidenciaUtc); Assert.False(salud.Suficiente);
    }
    [Fact]
    public void NodoPriorizaFallaYDeclaraCoberturaParcial()
    {
        var s = new SaludNodoService();
        var resultado = s.Agregar([Resultado(EstadoSaludPanel.Desconectado), Resultado(EstadoSaludPanel.Falla), new(null, false, "Sin datos", null)]);
        Assert.Equal(EstadoSaludPanel.Falla, resultado.Estado); Assert.False(resultado.Completo); Assert.Equal(2, resultado.PanelesEvaluados);
        Assert.Null(s.Agregar([]).Estado);
        Assert.Equal(EstadoSaludPanel.Desconectado, s.Agregar([Resultado(EstadoSaludPanel.Degradado), Resultado(EstadoSaludPanel.Desconectado)]).Estado);
    }
    [Fact]
    public void EpisodiosConservanDeteccionYCierreYRecurrenciaTieneOtraIdentidad()
    {
        var panel = Panel(); var alerta = new AlertaPanel(panel, EstadoSaludPanel.Falla, Ahora);
        Assert.Throws<ArgumentException>(() => alerta.Cerrar(Ahora.AddSeconds(-1)));
        alerta.Cerrar(Ahora.AddSeconds(1)); alerta.Cerrar(Ahora.AddSeconds(2));
        Assert.Equal(Ahora.AddSeconds(1), alerta.CierreUtc);
        Assert.NotEqual(alerta.Id, new AlertaPanel(panel, EstadoSaludPanel.Falla, Ahora.AddSeconds(3)).Id);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlertaPanel(panel, EstadoSaludPanel.Normal, Ahora));
    }
    [Fact]
    public void PoliticaRechazaLimitesIncoherentes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoliticaSalud(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoliticaSalud(TimeSpan.FromSeconds(1), 0.8m, 0.5m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoliticaSalud(TimeSpan.FromSeconds(1), factorEsperado: 0));
    }
}
