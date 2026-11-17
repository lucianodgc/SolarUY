using SolarUY.Domain.Salud;

namespace SolarUY.Application.Salud;

public sealed class SaludPanelService(ISaludPanelRepository repository, PoliticaSalud politica,
    TimeProvider reloj) : ISaludPanelService
{
    public async Task<IReadOnlyList<AvisoSaludConfirmada>> EvaluarAsync(CancellationToken cancellationToken = default)
    {
        var ids = await repository.ListarPanelesAsync(cancellationToken);
        var avisos = new List<AvisoSaludConfirmada>();
        foreach (var lote in ids.Chunk(100))
            avisos.AddRange(await repository.EvaluarLoteAsync(lote, reloj.GetUtcNow().UtcDateTime, politica, cancellationToken));
        return avisos;
    }
}
