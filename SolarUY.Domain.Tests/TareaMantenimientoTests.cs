using SolarUY.Domain.Entities;
using SolarUY.Domain.Enums;

namespace SolarUY.Domain.Tests;

public sealed class TareaMantenimientoTests
{
    private static readonly DateTime Fecha = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static TareaMantenimiento Crear()
    {
        var nodo = new Nodo(new ParqueSolar("Parque", "Salto"), "Nodo", "Norte");
        var panel = new Panel(nodo, "SERIE", "Marca", "Modelo", 450, Fecha, EstadoPanel.Activo);
        return new TareaMantenimiento(panel, "Limpieza", "Limpiar superficie", Fecha, "tecnico-sub");
    }

    [Fact]
    public void Crear_ConservaDatosYComienzaPendiente()
    {
        var tarea = Crear();
        Assert.NotEqual(Guid.Empty, tarea.Id);
        Assert.Equal(tarea.Panel.Id, tarea.PanelId);
        Assert.Equal("Limpieza", tarea.Tipo);
        Assert.Equal("Limpiar superficie", tarea.Descripcion);
        Assert.Equal("tecnico-sub", tarea.ResponsableId);
        Assert.Equal(Fecha, tarea.FechaProgramada);
        Assert.Equal(EstadoTareaMantenimiento.Pendiente, tarea.Estado);
        Assert.Null(tarea.FechaRealizacion);
    }

    [Theory]
    [InlineData("", "Descripción", "sub")]
    [InlineData(" ", "Descripción", "sub")]
    [InlineData("Tipo", "", "sub")]
    [InlineData("Tipo", "Descripción", "")]
    public void Crear_RechazaDatosObligatoriosVacios(string tipo, string descripcion, string responsable)
        => Assert.ThrowsAny<ArgumentException>(() =>
            new TareaMantenimiento(Crear().Panel, tipo, descripcion, Fecha, responsable));

    [Fact]
    public void Crear_RechazaPanelAusente()
        => Assert.Throws<ArgumentNullException>(() => new TareaMantenimiento(null!, "Tipo", "Descripción", Fecha, "sub"));

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void Crear_RechazaFechaNoUtc(DateTimeKind kind)
        => Assert.Throws<ArgumentException>(() =>
            new TareaMantenimiento(Crear().Panel, "Tipo", "Descripción", DateTime.SpecifyKind(Fecha, kind), "sub"));

    [Fact]
    public void Crear_RechazaFechaAusente()
        => Assert.Throws<ArgumentException>(() =>
            new TareaMantenimiento(Crear().Panel, "Tipo", "Descripción", default, "sub"));

    [Fact]
    public void Completar_ExigeIniciarYRegistraRealizacion()
    {
        var tarea = Crear();
        Assert.Throws<InvalidOperationException>(() => tarea.Completar(Fecha));
        tarea.Iniciar();
        Assert.Equal(EstadoTareaMantenimiento.EnCurso, tarea.Estado);
        tarea.Completar(Fecha.AddHours(1));
        Assert.Equal(EstadoTareaMantenimiento.Completada, tarea.Estado);
        Assert.Equal(Fecha.AddHours(1), tarea.FechaRealizacion);
    }

    [Fact]
    public void Completar_RechazaFechaInvalidaSinCambiarEstado()
    {
        var tarea = Crear();
        tarea.Iniciar();
        Assert.Throws<ArgumentException>(() => tarea.Completar(default));
        Assert.Equal(EstadoTareaMantenimiento.EnCurso, tarea.Estado);
        Assert.Null(tarea.FechaRealizacion);
    }

    [Fact]
    public void Completada_RechazaTodasLasModificacionesSinAlterarDatos()
    {
        var tarea = Crear();
        tarea.Iniciar();
        tarea.Completar(Fecha);
        Assert.Throws<InvalidOperationException>(() => tarea.Iniciar());
        Assert.Throws<InvalidOperationException>(() => tarea.Completar(Fecha.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() => tarea.Cancelar());
        Assert.Throws<InvalidOperationException>(() => tarea.AsignarResponsable("otro"));
        Assert.Throws<InvalidOperationException>(() => tarea.Editar("Otro", "Otra", Fecha.AddDays(1), "otro"));
        Assert.Equal("tecnico-sub", tarea.ResponsableId);
        Assert.Equal("Limpieza", tarea.Tipo);
        Assert.Equal("Limpiar superficie", tarea.Descripcion);
        Assert.Equal(Fecha, tarea.FechaProgramada);
        Assert.Equal(Fecha, tarea.FechaRealizacion);
        Assert.Equal(EstadoTareaMantenimiento.Completada, tarea.Estado);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancelar_AdmitePendienteYEnCursoSinReactivacion(bool iniciar)
    {
        var tarea = Crear();
        if (iniciar) tarea.Iniciar();
        tarea.Cancelar();
        Assert.Equal(EstadoTareaMantenimiento.Cancelada, tarea.Estado);
        Assert.Null(tarea.FechaRealizacion);
        Assert.Throws<InvalidOperationException>(() => tarea.Iniciar());
        Assert.Throws<InvalidOperationException>(() => tarea.Completar(Fecha));
        Assert.Throws<InvalidOperationException>(() => tarea.Cancelar());
        Assert.Throws<InvalidOperationException>(() => tarea.AsignarResponsable("otro"));
        Assert.Throws<InvalidOperationException>(() => tarea.Editar("Tipo", "Descripción", Fecha, "otro"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditarYReasignar_ConservaPanelYEstado(bool iniciar)
    {
        var tarea = Crear();
        if (iniciar) tarea.Iniciar();
        var panel = tarea.Panel;
        var estado = tarea.Estado;
        tarea.Editar("Inspección", "Revisar", Fecha.AddDays(1), "otro");
        tarea.AsignarResponsable("tercero");
        Assert.Same(panel, tarea.Panel);
        Assert.Equal(estado, tarea.Estado);
        Assert.Equal("Inspección", tarea.Tipo);
        Assert.Equal("Revisar", tarea.Descripcion);
        Assert.Equal(Fecha.AddDays(1), tarea.FechaProgramada);
        Assert.Equal("tercero", tarea.ResponsableId);
    }

    [Fact]
    public void Editar_RechazaDatosInvalidosSinCambiosParciales()
    {
        var tarea = Crear();
        Assert.ThrowsAny<ArgumentException>(() => tarea.Editar("Otro", "", Fecha.AddDays(1), "otro"));
        Assert.ThrowsAny<ArgumentException>(() => tarea.AsignarResponsable(" "));
        Assert.Equal("Limpieza", tarea.Tipo);
        Assert.Equal("tecnico-sub", tarea.ResponsableId);
        Assert.Equal(Fecha, tarea.FechaProgramada);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EstaVencida_ExigeFechaEstrictamenteAnterior(bool iniciar)
    {
        var tarea = Crear();
        if (iniciar) tarea.Iniciar();
        Assert.False(tarea.EstaVencida(Fecha.AddTicks(-1)));
        Assert.False(tarea.EstaVencida(Fecha));
        Assert.True(tarea.EstaVencida(Fecha.AddTicks(1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EstaVencida_ExcluyeCompletadasYCanceladas(bool completar)
    {
        var tarea = Crear();
        if (completar) { tarea.Iniciar(); tarea.Completar(Fecha); }
        else tarea.Cancelar();
        Assert.False(tarea.EstaVencida(Fecha.AddDays(1)));
    }

    [Fact]
    public void EstaVencida_RechazaReferenciaNoUtc()
        => Assert.Throws<ArgumentException>(() => Crear().EstaVencida(DateTime.SpecifyKind(Fecha, DateTimeKind.Unspecified)));
}
