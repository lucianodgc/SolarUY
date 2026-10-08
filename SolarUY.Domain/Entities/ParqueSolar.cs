namespace SolarUY.Domain.Entities;

public sealed class ParqueSolar
{
    private ParqueSolar() { }

    public ParqueSolar(string nombre, string ubicacion, decimal? capacidadEstimadaMW = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(ubicacion);
        if (nombre.Length > 100)
            throw new ArgumentException("El nombre admite hasta 100 caracteres.", nameof(nombre));
        if (ubicacion.Length > 200)
            throw new ArgumentException("La ubicación admite hasta 200 caracteres.", nameof(ubicacion));

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
}
