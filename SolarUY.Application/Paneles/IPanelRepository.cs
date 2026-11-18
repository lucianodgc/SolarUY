using SolarUY.Domain.Entities;

namespace SolarUY.Application.Paneles;

public interface IPanelRepository
{
    Task<Panel?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Nodo?> ObtenerNodoAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NodoParaPanelDto>> ListarNodosParaAltaAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PanelDto>> ListarDetallesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PanelDto>> ListarDetallesPorNodoAsync(Guid nodoId, CancellationToken cancellationToken = default);
    Task<bool> ExisteNumeroSerieAsync(string numeroSerie, Guid? panelIdExcluido = null,
        CancellationToken cancellationToken = default);
    Task AgregarAsync(Panel panel, CancellationToken cancellationToken = default);
    Task ActualizarAsync(Panel panel, Guid nodoOriginalId, CancellationToken cancellationToken = default);
    Task DarDeBajaAsync(Panel panel, CancellationToken cancellationToken = default);
}
