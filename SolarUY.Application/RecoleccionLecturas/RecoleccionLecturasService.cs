using SolarUY.Application.Abstractions;
using SolarUY.Domain.Entities;

namespace SolarUY.Application.RecoleccionLecturas;

public sealed class RecoleccionLecturasService : IRecoleccionLecturasService
{
    private readonly IRecoleccionLecturasRepository _repository;
    private readonly IInversorClient _inversor;
    private readonly TimeProvider _reloj;

    public RecoleccionLecturasService(IRecoleccionLecturasRepository repository, IInversorClient inversor, TimeProvider reloj)
    {
        _repository = repository;
        _inversor = inversor;
        _reloj = reloj;
    }

    public async Task<ResultadoRecoleccionDto> RecolectarAsync(CancellationToken cancellationToken = default)
    {
        await using var sesion = await _repository.IntentarAbrirSesionAsync(cancellationToken);
        if (sesion is null) return new ResultadoRecoleccionDto(false, 0);
        var instante = _reloj.GetUtcNow().UtcDateTime;
        var paneles = await sesion.ListarPendientesAsync(instante, cancellationToken);
        if (paneles.Count == 0) return new ResultadoRecoleccionDto(true, 0);
        var solicitudes = paneles.Select(panel => new PanelMedicionDto(panel.Id, panel.NodoId,
            panel.NumeroSerie, panel.PotenciaNominalW, panel.Estado)).ToArray();
        var mediciones = await _inversor.ObtenerMedicionesRegistradasAsync(solicitudes, instante, cancellationToken);
        var lecturas = CrearLecturas(paneles, mediciones, instante);
        await sesion.GuardarAsync(lecturas, cancellationToken);
        return new ResultadoRecoleccionDto(true, lecturas.Count);
    }

    private static IReadOnlyList<LecturaPanel> CrearLecturas(IReadOnlyList<Panel> paneles,
        IReadOnlyList<MedicionPanelDto> mediciones, DateTime instante)
    {
        var porId = paneles.ToDictionary(panel => panel.Id);
        if (mediciones.Count != paneles.Count || mediciones.Select(medicion => medicion.PanelId).Distinct().Count() != paneles.Count
            || mediciones.Any(medicion => !porId.ContainsKey(medicion.PanelId)))
            throw new InvalidOperationException("El inversor no devolvió exactamente una medición por panel solicitado.");
        // Validar el lote completo antes de modificar persistencia.
        return mediciones.Select(medicion => new LecturaPanel(porId[medicion.PanelId], instante,
            medicion.PotenciaGeneradaW, medicion.EstadoSalud)).ToArray();
    }
}
