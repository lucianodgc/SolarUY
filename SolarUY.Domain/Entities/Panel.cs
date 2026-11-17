using SolarUY.Domain.Enums;

namespace SolarUY.Domain.Entities;

public sealed class Panel
{
    private Panel() { }

    public Panel(Nodo nodo, string numeroSerie, string marca, string modelo,
        decimal potenciaNominalW, DateTime fechaInstalacion, EstadoPanel estado)
    {
        ArgumentNullException.ThrowIfNull(nodo);
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroSerie);
        ArgumentException.ThrowIfNullOrWhiteSpace(marca);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        if (potenciaNominalW <= 0) throw new ArgumentOutOfRangeException(nameof(potenciaNominalW));
        if (fechaInstalacion == default || fechaInstalacion.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha de instalación debe ser un instante UTC.", nameof(fechaInstalacion));
        if (!Enum.IsDefined(estado))
            throw new ArgumentOutOfRangeException(nameof(estado));
        Id = Guid.NewGuid();
        Nodo = nodo;
        NodoId = nodo.Id;
        NumeroSerie = numeroSerie;
        Marca = marca;
        Modelo = modelo;
        PotenciaNominalW = potenciaNominalW;
        FechaInstalacion = fechaInstalacion;
        Estado = estado;
    }

    public Guid Id { get; private set; }
    public Guid NodoId { get; private set; }
    public Nodo Nodo { get; private set; } = null!;
    public string NumeroSerie { get; private set; } = string.Empty;
    public string Marca { get; private set; } = string.Empty;
    public string Modelo { get; private set; } = string.Empty;
    public decimal PotenciaNominalW { get; private set; }
    public DateTime FechaInstalacion { get; private set; }
    public EstadoPanel Estado { get; private set; }
    public bool EstaDadoDeBaja { get; private set; }

    public void Actualizar(string numeroSerie, string marca, string modelo, decimal potenciaNominalW,
        DateTime fechaInstalacion, EstadoPanel estado)
    {
        ValidarDatos(numeroSerie, marca, modelo, potenciaNominalW, fechaInstalacion, estado);
        NumeroSerie = numeroSerie;
        Marca = marca;
        Modelo = modelo;
        PotenciaNominalW = potenciaNominalW;
        FechaInstalacion = fechaInstalacion;
        Estado = estado;
    }

    public void TrasladarANodo(Nodo nodo)
    {
        ArgumentNullException.ThrowIfNull(nodo);
        if (EstaDadoDeBaja) throw new InvalidOperationException("No se puede trasladar un panel dado de baja.");
        Nodo = nodo;
        NodoId = nodo.Id;
    }

    public void DarDeBaja()
    {
        if (EstaDadoDeBaja) throw new InvalidOperationException("El panel ya está dado de baja.");
        EstaDadoDeBaja = true;
    }

    private static void ValidarDatos(string numeroSerie, string marca, string modelo, decimal potenciaNominalW,
        DateTime fechaInstalacion, EstadoPanel estado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroSerie);
        ArgumentException.ThrowIfNullOrWhiteSpace(marca);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        if (potenciaNominalW <= 0) throw new ArgumentOutOfRangeException(nameof(potenciaNominalW));
        if (fechaInstalacion == default || fechaInstalacion.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha de instalación debe ser un instante UTC.", nameof(fechaInstalacion));
        if (!Enum.IsDefined(estado)) throw new ArgumentOutOfRangeException(nameof(estado));
    }
}
