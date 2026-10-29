namespace SolarUY.Domain.Entities;

public sealed class ParqueSolar
{
    private readonly List<Nodo> _nodos = [];

    private ParqueSolar() { }

    public ParqueSolar(string nombre, string ubicacion, decimal? capacidadEstimadaMW = null)
    {
        ValidarDatos(nombre, ubicacion);

        Id = Guid.NewGuid();
        Nombre = nombre;
        Ubicacion = ubicacion;
        CapacidadEstimadaMW = capacidadEstimadaMW;
        FechaCreacion = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Ubicacion { get; private set; } = string.Empty;
    public decimal? CapacidadEstimadaMW { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public IReadOnlyCollection<Nodo> Nodos => _nodos.AsReadOnly();

    public void ActualizarDatos(string nombre, string ubicacion, decimal? capacidadEstimadaMW)
    {
        ValidarDatos(nombre, ubicacion);

        Nombre = nombre;
        Ubicacion = ubicacion;
        CapacidadEstimadaMW = capacidadEstimadaMW;
    }

    internal void AgregarNodo(Nodo nodo)
    {
        ArgumentNullException.ThrowIfNull(nodo);
        if (!_nodos.Contains(nodo)) _nodos.Add(nodo);
    }

    public void QuitarNodo(Nodo nodo)
    {
        ArgumentNullException.ThrowIfNull(nodo);
        if (!_nodos.Contains(nodo)) throw new InvalidOperationException("El nodo no pertenece al parque solar.");
        if (_nodos.Count <= 1) throw new InvalidOperationException("Un parque solar debe conservar al menos un nodo.");
        _nodos.Remove(nodo);
    }

    private static void ValidarDatos(string nombre, string ubicacion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(ubicacion);
        if (nombre.Length > 100)
            throw new ArgumentException("El nombre admite hasta 100 caracteres.", nameof(nombre));
        if (ubicacion.Length > 200)
            throw new ArgumentException("La ubicación admite hasta 200 caracteres.", nameof(ubicacion));
    }
}
