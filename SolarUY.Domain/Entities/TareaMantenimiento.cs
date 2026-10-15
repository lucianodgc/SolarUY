using SolarUY.Domain.Enums;

namespace SolarUY.Domain.Entities;

public sealed class TareaMantenimiento
{
    private TareaMantenimiento() { }

    public TareaMantenimiento(Panel panel, string tipo, string descripcion,
        DateTime fechaProgramada, string responsableId)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ValidarDatos(tipo, descripcion, fechaProgramada, responsableId);
        Id = Guid.NewGuid();
        Panel = panel;
        PanelId = panel.Id;
        Tipo = tipo;
        Descripcion = descripcion;
        FechaProgramada = fechaProgramada;
        ResponsableId = responsableId;
        Estado = EstadoTareaMantenimiento.Pendiente;
    }

    public Guid Id { get; private set; }
    public Guid PanelId { get; private set; }
    public Panel Panel { get; private set; } = null!;
    public string Tipo { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public DateTime FechaProgramada { get; private set; }
    public DateTime? FechaRealizacion { get; private set; }
    public EstadoTareaMantenimiento Estado { get; private set; }
    public string ResponsableId { get; private set; } = string.Empty;

    public void AsignarResponsable(string responsableId)
    {
        AsegurarEditable();
        ArgumentException.ThrowIfNullOrWhiteSpace(responsableId);
        ResponsableId = responsableId;
    }

    public void Editar(string tipo, string descripcion, DateTime fechaProgramada, string responsableId)
    {
        AsegurarEditable();
        ValidarDatos(tipo, descripcion, fechaProgramada, responsableId);
        Tipo = tipo;
        Descripcion = descripcion;
        FechaProgramada = fechaProgramada;
        ResponsableId = responsableId;
    }

    public void Iniciar()
    {
        if (Estado != EstadoTareaMantenimiento.Pendiente)
            throw new InvalidOperationException("Solo puede iniciarse una tarea pendiente.");
        Estado = EstadoTareaMantenimiento.EnCurso;
    }

    public void Completar(DateTime fechaRealizacionUtc)
    {
        if (Estado != EstadoTareaMantenimiento.EnCurso)
            throw new InvalidOperationException("Solo puede completarse una tarea en curso.");
        ValidarInstanteUtc(fechaRealizacionUtc);
        FechaRealizacion = fechaRealizacionUtc;
        Estado = EstadoTareaMantenimiento.Completada;
    }

    public void Cancelar()
    {
        AsegurarEditable();
        Estado = EstadoTareaMantenimiento.Cancelada;
    }

    public bool EstaVencida(DateTime referenciaUtc)
    {
        ValidarInstanteUtc(referenciaUtc);
        return FechaProgramada < referenciaUtc
            && Estado is EstadoTareaMantenimiento.Pendiente or EstadoTareaMantenimiento.EnCurso;
    }

    private void AsegurarEditable()
    {
        if (Estado is not (EstadoTareaMantenimiento.Pendiente or EstadoTareaMantenimiento.EnCurso))
            throw new InvalidOperationException("Una tarea completada o cancelada no puede modificarse.");
    }

    private static void ValidarDatos(string tipo, string descripcion, DateTime fechaProgramada, string responsableId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);
        ArgumentException.ThrowIfNullOrWhiteSpace(descripcion);
        ArgumentException.ThrowIfNullOrWhiteSpace(responsableId);
        ValidarInstanteUtc(fechaProgramada);
    }

    private static void ValidarInstanteUtc(DateTime instante)
    {
        if (instante == default || instante.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser un instante UTC válido.", nameof(instante));
    }
}
