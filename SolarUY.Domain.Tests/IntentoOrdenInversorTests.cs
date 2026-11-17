using SolarUY.Domain.Entities;

namespace SolarUY.Domain.Tests;

public sealed class IntentoOrdenInversorTests
{
    private static readonly DateTime Ahora = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private static IntentoOrdenInversor Crear() => new(Guid.NewGuid(), "sub-real", true, true,
        AccionOrdenInversor.DesactivarPanel, "nodo", "panel", Ahora);

    [Fact]
    public void ConstruccionConservaSolicitudYDestino()
    {
        var intento = Crear(); var id = intento.Id; var nodo = Guid.NewGuid(); var panel = Guid.NewGuid();
        intento.ValidarDestino(nodo, panel, "Nodo / SERIE");
        intento.Finalizar(EstadoOrdenInversor.Confirmada, "ack", Ahora.AddSeconds(1), true);
        Assert.Equal(id, intento.Id); Assert.Equal("sub-real", intento.ActorSub);
        Assert.Equal("nodo", intento.NodoSolicitado); Assert.Equal("panel", intento.PanelSolicitado);
        Assert.Equal(nodo, intento.NodoValidado); Assert.Equal(panel, intento.PanelValidado);
        Assert.Equal("Nodo / SERIE", intento.DestinoNombre);
        Assert.Equal(Ahora, intento.SolicitadaUtc); Assert.Equal(EstadoOrdenInversor.Confirmada, intento.Estado);
        Assert.Throws<InvalidOperationException>(() => intento.ValidarDestino(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => intento.Finalizar(EstadoOrdenInversor.Fallida, "error", Ahora));
    }

    [Fact]
    public void ConfirmacionExigeAckYAutorizacion()
    {
        Assert.Throws<InvalidOperationException>(() => Crear().Finalizar(EstadoOrdenInversor.Confirmada, "ack", Ahora));
        Assert.Throws<InvalidOperationException>(() => Crear().Finalizar(EstadoOrdenInversor.Confirmada, "ack", Ahora, true));
        var anonimo = new IntentoOrdenInversor(Guid.NewGuid(), null, false, false, AccionOrdenInversor.ActivarVolcadoRed, null, null, Ahora);
        Assert.Throws<InvalidOperationException>(() => anonimo.Finalizar(EstadoOrdenInversor.Confirmada, "ack", Ahora, true));
        anonimo.Finalizar(EstadoOrdenInversor.Rechazada, "sin-permiso", Ahora);
        Assert.Null(anonimo.ActorSub);
    }

    [Theory]
    [InlineData(EstadoOrdenInversor.Rechazada)]
    [InlineData(EstadoOrdenInversor.Fallida)]
    [InlineData(EstadoOrdenInversor.Indeterminada)]
    public void ResultadosSinConfirmacionSonTerminales(EstadoOrdenInversor estado)
    {
        var intento = Crear(); intento.Finalizar(estado, "resultado", Ahora);
        Assert.Equal(estado, intento.Estado);
        Assert.Throws<InvalidOperationException>(() => intento.Finalizar(EstadoOrdenInversor.Confirmada, "ack", Ahora, true));
    }

    [Fact]
    public void RechazaFechasNoUtcYCodigosInseguros()
    {
        Assert.Throws<ArgumentException>(() => new IntentoOrdenInversor(Guid.NewGuid(), "sub", true, true,
            AccionOrdenInversor.ActivarVolcadoRed, null, null, DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => Crear().Finalizar(EstadoOrdenInversor.Fallida, "mensaje con secretos", Ahora));
        Assert.Throws<ArgumentException>(() => Crear().Finalizar(EstadoOrdenInversor.Fallida, "error", Ahora.AddSeconds(-1)));
    }
}
