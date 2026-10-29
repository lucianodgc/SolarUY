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
        parque.AgregarNodo(this);
    }

    public Guid Id { get; private set; }
    public Guid ParqueId { get; private set; }
    public ParqueSolar Parque { get; private set; } = null!;
    public string Nombre { get; private set; } = string.Empty;
    public string UbicacionEnParque { get; private set; } = string.Empty;
    public int? CantidadPanelesPrevista { get; private set; }
    public Inversor Inversor { get; private set; } = null!;

    public void ActualizarDatos(string nombre, string ubicacionEnParque, int? cantidadPanelesPrevista)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(ubicacionEnParque);
        Nombre = nombre;
        UbicacionEnParque = ubicacionEnParque;
        CantidadPanelesPrevista = cantidadPanelesPrevista;
    }

    internal void AsignarInversor(Inversor inversor)
    {
        ArgumentNullException.ThrowIfNull(inversor);
        if (Inversor is not null) throw new InvalidOperationException("El nodo ya tiene un inversor asociado.");
        Inversor = inversor;
    }
}
