using SolarUY.Domain.Enums;

namespace SolarUY.Domain.Entities;

public sealed class Inversor
{
    private Inversor() { }

    public Inversor(Nodo nodo, string marca, string modelo, decimal potenciaNominalKW,
        EstadoInversor estado, bool volcadoRedActivo)
    {
        ArgumentNullException.ThrowIfNull(nodo);
        ArgumentException.ThrowIfNullOrWhiteSpace(marca);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        if (potenciaNominalKW <= 0) throw new ArgumentOutOfRangeException(nameof(potenciaNominalKW));
        if (!Enum.IsDefined(estado)) throw new ArgumentOutOfRangeException(nameof(estado));

        Id = Guid.NewGuid();
        Nodo = nodo;
        NodoId = nodo.Id;
        Marca = marca;
        Modelo = modelo;
        PotenciaNominalKW = potenciaNominalKW;
        Estado = estado;
        VolcadoRedActivo = volcadoRedActivo;
        nodo.AsignarInversor(this);
    }

    public Guid Id { get; private set; }
    public Guid NodoId { get; private set; }
    public Nodo Nodo { get; private set; } = null!;
    public string Marca { get; private set; } = string.Empty;
    public string Modelo { get; private set; } = string.Empty;
    public decimal PotenciaNominalKW { get; private set; }
    public EstadoInversor Estado { get; private set; }
    public bool VolcadoRedActivo { get; private set; }
}
