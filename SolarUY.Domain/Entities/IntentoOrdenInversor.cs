namespace SolarUY.Domain.Entities;

public enum AccionOrdenInversor { ActivarPanel, DesactivarPanel, ActivarVolcadoRed, DesactivarVolcadoRed }
public enum EstadoOrdenInversor { Pendiente, Confirmada, Rechazada, Fallida, Indeterminada }

public sealed class IntentoOrdenInversor
{
    private IntentoOrdenInversor() { }
    public Guid Id { get; private set; }
    public Guid Correlacion { get; private set; }
    public string? ActorSub { get; private set; }
    public bool Autenticado { get; private set; }
    public bool Administrador { get; private set; }
    public AccionOrdenInversor Accion { get; private set; }
    public string? NodoSolicitado { get; private set; }
    public string? PanelSolicitado { get; private set; }
    public Guid? NodoValidado { get; private set; }
    public Guid? PanelValidado { get; private set; }
    public string DestinoNombre { get; private set; } = "Inversor configurado — alcance global";
    public DateTime SolicitadaUtc { get; private set; }
    public DateTime? ResultadoUtc { get; private set; }
    public EstadoOrdenInversor Estado { get; private set; }
    public string Codigo { get; private set; } = "pendiente";

    public IntentoOrdenInversor(Guid correlacion, string? actorSub, bool autenticado, bool administrador,
        AccionOrdenInversor accion, string? nodoSolicitado, string? panelSolicitado, DateTime solicitadaUtc)
    {
        if (correlacion == Guid.Empty) throw new ArgumentException("La correlación es obligatoria.", nameof(correlacion));
        if (!Enum.IsDefined(accion)) throw new ArgumentOutOfRangeException(nameof(accion));
        ValidarUtc(solicitadaUtc);
        if (actorSub?.Length > 255 || nodoSolicitado?.Length > 128 || panelSolicitado?.Length > 128)
            throw new ArgumentException("Identificador demasiado largo.");
        Id = Guid.NewGuid(); Correlacion = correlacion; ActorSub = string.IsNullOrWhiteSpace(actorSub) ? null : actorSub;
        Autenticado = autenticado; Administrador = administrador; Accion = accion;
        NodoSolicitado = nodoSolicitado; PanelSolicitado = panelSolicitado; SolicitadaUtc = solicitadaUtc;
        if (accion is AccionOrdenInversor.ActivarPanel or AccionOrdenInversor.DesactivarPanel)
            DestinoNombre = "Panel solicitado";
    }

    public void ValidarDestino(Guid nodoId, Guid panelId, string nombre = "Panel validado")
    {
        if (Estado != EstadoOrdenInversor.Pendiente || NodoValidado.HasValue)
            throw new InvalidOperationException("El destino ya fue fijado.");
        if (Accion is not (AccionOrdenInversor.ActivarPanel or AccionOrdenInversor.DesactivarPanel)
            || nodoId == Guid.Empty || panelId == Guid.Empty) throw new ArgumentException("Destino inválido.");
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 300) throw new ArgumentException("Nombre de destino inválido.");
        NodoValidado = nodoId; PanelValidado = panelId; DestinoNombre = nombre;
    }

    public void Finalizar(EstadoOrdenInversor estado, string codigo, DateTime instanteUtc, bool ackVerdadero = false)
    {
        ValidarUtc(instanteUtc);
        if (Estado != EstadoOrdenInversor.Pendiente || estado == EstadoOrdenInversor.Pendiente || !Enum.IsDefined(estado))
            throw new InvalidOperationException("Transición de resultado inválida.");
        if (instanteUtc < SolicitadaUtc) throw new ArgumentException("Resultado anterior a la solicitud.");
        if (estado == EstadoOrdenInversor.Confirmada && (!ackVerdadero || !Autenticado || !Administrador || ActorSub is null))
            throw new InvalidOperationException("La confirmación requiere autorización y ACK verdadero.");
        if (estado == EstadoOrdenInversor.Confirmada && Accion is (AccionOrdenInversor.ActivarPanel or AccionOrdenInversor.DesactivarPanel)
            && !PanelValidado.HasValue) throw new InvalidOperationException("La confirmación requiere destino validado.");
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 64 || codigo.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
            throw new ArgumentException("Código de resultado inválido.");
        Estado = estado; Codigo = codigo; ResultadoUtc = instanteUtc;
    }

    private static void ValidarUtc(DateTime instante)
    {
        if (instante.Kind != DateTimeKind.Utc) throw new ArgumentException("Se requiere UTC.");
    }
}
