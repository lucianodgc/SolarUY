using SolarUY.Application.Abstractions;
using SolarUY.Application.Chatbot;
using SolarUY.Application.Inversores;

namespace SolarUY.Infrastructure.Chatbot;

public sealed class EstadoEquiposConsultaClient : IEstadoEquiposConsultaClient
{
    private readonly IInversorClient _inversor;
    private readonly TimeProvider _reloj;

    public EstadoEquiposConsultaClient(IInversorClient inversor, TimeProvider reloj)
    { _inversor = inversor; _reloj = reloj; }

    public async Task<IReadOnlyList<TelemetriaNodoDto>> ConsultarAsync(IReadOnlyList<InventarioNodoInversorDto> nodos,
        CancellationToken cancellationToken = default)
    {
        if (nodos.Count == 0) return [];
        var volcado = await _inversor.ObtenerEstadoVolcadoRedAsync(cancellationToken);
        var encendido = await _inversor.ObtenerEstadoEncendidoAsync(cancellationToken);
        var resultado = new List<TelemetriaNodoDto>();
        foreach (var nodo in nodos)
        {
            var paneles = await _inversor.ObtenerEstadoPanelesAsync(nodo.Id, cancellationToken);
            var permitidos = nodo.Paneles.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (paneles.Any(p => p.NodoId != nodo.Id || !permitidos.Contains(p.PanelId))
                || permitidos.Count != paneles.Select(p => p.PanelId).Distinct().Count())
                throw new InvalidOperationException("La telemetría no coincide con el inventario consultado.");
            resultado.Add(new(nodo.ParqueId, nodo.Parque, nodo.Id, nodo.Nombre, paneles,
                paneles.Sum(p => p.PotenciaGeneradaW), volcado, encendido, _reloj.GetUtcNow().UtcDateTime));
        }
        return resultado.ToArray();
    }
}
