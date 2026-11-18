namespace SolarUY.Application.Paneles;

public sealed record NodoParaPanelDto(Guid Id, string Parque, string Nombre, int? CantidadPanelesPrevista,
    int CantidadPanelesAsignados)
{
    public int? CuposDisponibles => CantidadPanelesPrevista is int capacidad
        ? Math.Max(0, capacidad - CantidadPanelesAsignados)
        : null;

    public bool SinCupo => CuposDisponibles == 0;

    public string Etiqueta => $"{Parque} / {Nombre} · {DescripcionCupos}";

    private string DescripcionCupos => CuposDisponibles switch
    {
        null => "Sin límite",
        0 => "Sin cupos",
        1 => "1 cupo disponible",
        int cupos => $"{cupos} cupos disponibles"
    };
}
