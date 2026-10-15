namespace SolarUY.Domain.Entities;

public sealed class Nodo
{
    private Nodo() { }

    public Nodo(ParqueSolar parque, string nombre, string ubicacionEnParque, int? cantidadPanelesPrevista = null)
    {
        ArgumentNullException.ThrowIfNull(parque);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(ubicacionEnParque);
        Id = Guid.NewGuid();
        Parque = parque;
        ParqueId = parque.Id;
        Nombre = nombre;
        UbicacionEnParque = ubicacionEnParque;
        CantidadPanelesPrevista = cantidadPanelesPrevista;
    }

    public Guid Id { get; private set; }
    public Guid ParqueId { get; private set; }
    public ParqueSolar Parque { get; private set; } = null!;
    public string Nombre { get; private set; } = string.Empty;
    public string UbicacionEnParque { get; private set; } = string.Empty;
    public int? CantidadPanelesPrevista { get; private set; }
}
