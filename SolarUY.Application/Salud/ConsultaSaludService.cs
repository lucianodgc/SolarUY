using SolarUY.Application.Abstractions;
using SolarUY.Domain.Salud;

namespace SolarUY.Application.Salud;

public sealed record ConfiguracionConsultaSalud(TimeSpan VigenciaEvaluacion);

public sealed class ConsultaSaludService(ISaludPanelRepository repository, IUsuarioActual usuario,
    SaludNodoService nodos, TimeProvider reloj, ConfiguracionConsultaSalud configuracion) : IConsultaSaludService
{
    public SaludParqueDto Reevaluar(SaludParqueDto estado)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var paneles = estado.Paneles.Select(p => p with { Vigente = p.Vigente && p.EvaluadoUtc.HasValue
            && ahora >= p.EvaluadoUtc && ahora - p.EvaluadoUtc.Value < configuracion.VigenciaEvaluacion }).ToArray();
        var vigentes = paneles.Where(p => p.Vigente).Select(p => p.PanelId).ToHashSet();
        var resumen = estado.Nodos.Select(n =>
        {
            var r = nodos.Agregar(paneles.Where(p => p.NodoId == n.NodoId).Select(p =>
                new ResultadoSalud(p.Vigente ? p.Estado : null, p.Vigente, p.Motivo, p.EvidenciaUtc)).ToArray());
            return n with { Estado = r.Estado, Evaluados = r.PanelesEvaluados, Total = r.TotalPaneles };
        }).ToArray();
        return estado with { Paneles = paneles, Nodos = resumen,
            Alertas = estado.Alertas.Select(a => a with { Vigente = a.Vigente && vigentes.Contains(a.PanelId) }).ToArray() };
    }
    public async Task<SaludParqueDto> ConsultarAsync(Guid parqueId, CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken); ValidarId(parqueId);
        var datos = await repository.ConsultarAsync(parqueId, reloj.GetUtcNow().UtcDateTime,
            configuracion.VigenciaEvaluacion, cancellationToken) ?? throw new KeyNotFoundException("El parque no existe.");
        var resumen = datos.Nodos.Select(n =>
        {
            var paneles = datos.Paneles.Where(p => p.NodoId == n.Id).Select(p =>
                new ResultadoSalud(p.Vigente ? p.Estado : null, p.Vigente, p.Motivo, p.EvidenciaUtc)).ToArray();
            var resultado = nodos.Agregar(paneles);
            return new SaludNodoDto(n.Id, n.Nombre, resultado.Estado, resultado.PanelesEvaluados, resultado.TotalPaneles);
        }).ToArray();
        return new(parqueId, datos.Paneles, resumen, datos.Alertas);
    }

    public async Task<PaginaCambiosSaludDto> HistorialAsync(Guid parqueId, Guid panelId, int pagina = 0,
        int tamano = 20, CancellationToken cancellationToken = default)
    {
        await AutorizarAsync(cancellationToken); ValidarId(parqueId); ValidarId(panelId);
        if (pagina < 0 || tamano is < 1 or > 100 || pagina > int.MaxValue / tamano)
            throw new ArgumentOutOfRangeException(nameof(pagina));
        return await repository.HistorialAsync(parqueId, panelId, pagina, tamano, cancellationToken);
    }

    private async Task AutorizarAsync(CancellationToken cancellationToken)
    {
        var actual = await usuario.ObtenerAsync(cancellationToken);
        if (!actual.EstaAutenticado || string.IsNullOrWhiteSpace(actual.Id)
            || !actual.Roles.Any(r => r is "Administrador" or "Tecnico" or "Consulta"))
            throw new UnauthorizedAccessException("No tienes permisos para consultar salud y alertas.");
    }
    private static void ValidarId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Selecciona un identificador válido.");
    }
}
