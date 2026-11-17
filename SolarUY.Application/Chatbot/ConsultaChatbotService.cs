using SolarUY.Application.Abstractions;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.Inversores;
using SolarUY.Application.Mantenimiento;
using SolarUY.Application.Monitoreo;
using SolarUY.Domain.Enums;

namespace SolarUY.Application.Chatbot;

public sealed class ConsultaChatbotService : IConsultaChatbotService
{
    private readonly IUsuarioActual _usuarioActual;
    private readonly IMonitoreoService _monitoreo;
    private readonly IInversorInventarioRepository _inventario;
    private readonly IEstadoEquiposConsultaClient _equipos;
    private readonly IHistoricoGeneracionService _historico;
    private readonly ITareaMantenimientoService _mantenimiento;
    private readonly TimeProvider _reloj;

    public ConsultaChatbotService(IUsuarioActual usuarioActual, IMonitoreoService monitoreo,
        IInversorInventarioRepository inventario, IEstadoEquiposConsultaClient equipos,
        IHistoricoGeneracionService historico, ITareaMantenimientoService mantenimiento, TimeProvider reloj)
    {
        _usuarioActual = usuarioActual; _monitoreo = monitoreo; _inventario = inventario;
        _equipos = equipos; _historico = historico; _mantenimiento = mantenimiento; _reloj = reloj;
    }

    public Task<ResultadoConsultaChatDto> ListarParquesAsync(CancellationToken cancellationToken = default)
        => EjecutarAsync(nameof(ListarParquesAsync), async () =>
        {
            var parques = await _monitoreo.ListarParquesAsync(cancellationToken);
            return Resultado(nameof(ListarParquesAsync), parques, parques.Count == 0,
                "No hay parques disponibles para tu usuario.");
        }, cancellationToken);

    public Task<ResultadoConsultaChatDto> ConsultarEstadoActualAsync(string parque, CancellationToken cancellationToken = default)
        => EjecutarAsync(nameof(ConsultarEstadoActualAsync), async () =>
        {
            var seleccionado = await ResolverParqueAsync(parque, cancellationToken);
            var nodos = await NodosAsync(seleccionado.Id, cancellationToken);
            var estados = await _equipos.ConsultarAsync(nodos, cancellationToken);
            return Resultado(nameof(ConsultarEstadoActualAsync),
                new EstadoEquiposChatDto(seleccionado.Id, seleccionado.Nombre, _reloj.GetUtcNow().UtcDateTime, estados),
                estados.Count == 0, $"No hay datos de estado actual para el parque «{seleccionado.Nombre}».");
        }, cancellationToken);

    public Task<ResultadoConsultaChatDto> ConsultarPanelesConFallaAsync(string parque, CancellationToken cancellationToken = default)
        => EjecutarAsync(nameof(ConsultarPanelesConFallaAsync), async () =>
        {
            var seleccionado = await ResolverParqueAsync(parque, cancellationToken);
            var nodos = await NodosAsync(seleccionado.Id, cancellationToken);
            var actuales = await _equipos.ConsultarAsync(nodos, cancellationToken);
            var fallas = actuales.SelectMany(n => n.Paneles.Where(p => p.EstadoSalud == EstadoSaludPanel.Fallado)
                .Select(p => new FallaPanelChatDto(p.PanelId, p.NumeroSerie, n.NodoNombre,
                    "Salud actual del inversor", n.FechaConsultaUtc))).ToList();
            fallas.AddRange(nodos.SelectMany(n => n.Paneles.Where(p => p.Estado == EstadoPanel.Falla)
                .Select(p => new FallaPanelChatDto(p.Id, p.NumeroSerie, n.Nombre,
                    "Estado registrado en inventario", _reloj.GetUtcNow().UtcDateTime))));
            if (fallas.Count == 0 && !actuales.Any(n => n.Paneles.Count > 0))
                return Resultado(nameof(ConsultarPanelesConFallaAsync), fallas.ToArray(), true,
                    $"No hay datos de paneles para evaluar fallas en el parque «{seleccionado.Nombre}».");
            return new(nameof(ConsultarPanelesConFallaAsync), EstadoConsultaChat.Correcto,
                fallas.Count == 0 ? $"No se encontraron paneles con fallas en el parque «{seleccionado.Nombre}»."
                    : "Datos consultados en SolarUY.", fallas.ToArray());
        }, cancellationToken);

    public Task<ResultadoConsultaChatDto> ConsultarGeneracionDiariaAsync(string parque, DateTime? fechaLocal = null,
        CancellationToken cancellationToken = default)
        => EjecutarAsync(nameof(ConsultarGeneracionDiariaAsync), async () =>
        {
            var seleccionado = await ResolverParqueAsync(parque, cancellationToken);
            var hoy = TimeZoneInfo.ConvertTime(_reloj.GetUtcNow(),
                TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo")).Date;
            var filtro = new FiltroHistoricoDto(TipoObjetivoHistorico.Parque, seleccionado.Id,
                PeriodoHistorico.Diario, FechaLocal(fechaLocal ?? hoy));
            return await GeneracionAsync(nameof(ConsultarGeneracionDiariaAsync), filtro,
                $"Generación diaria de «{seleccionado.Nombre}»: no hay lecturas para el día {filtro.InicioLocal:yyyy-MM-dd}.", cancellationToken);
        }, cancellationToken);

    public Task<ResultadoConsultaChatDto> ConsultarGeneracionHistoricaAsync(string objetivo, string tipo,
        string periodo, DateTime fechaLocal, CancellationToken cancellationToken = default)
        => EjecutarAsync(nameof(ConsultarGeneracionHistoricaAsync), async () =>
        {
            if (!Enum.TryParse<TipoObjetivoHistorico>(tipo, true, out var tipoObjetivo) || !Enum.IsDefined(tipoObjetivo)
                || !Enum.TryParse<PeriodoHistorico>(periodo, true, out var periodoHistorico)
                || periodoHistorico is not (PeriodoHistorico.Diario or PeriodoHistorico.Mensual or PeriodoHistorico.Anual))
                throw new ArgumentException("Indica Parque, Nodo o Panel y un período Diario, Mensual o Anual.");
            var opciones = await _historico.ListarOpcionesAsync(cancellationToken);
            var coincidencias = opciones.Where(o => o.Tipo == tipoObjetivo && Coincide(objetivo, o.Id, o.Etiqueta)).ToArray();
            var opcion = ResolverUnico(coincidencias);
            return await GeneracionAsync(nameof(ConsultarGeneracionHistoricaAsync),
                new FiltroHistoricoDto(tipoObjetivo, opcion.Id, periodoHistorico, FechaLocal(fechaLocal)),
                $"Generación histórica de «{opcion.Etiqueta}»: no hay lecturas para el período {periodoHistorico} consultado ({fechaLocal:yyyy-MM-dd}).", cancellationToken);
        }, cancellationToken);

    public Task<ResultadoConsultaChatDto> ConsultarMantenimientosPendientesAsync(string parque,
        CancellationToken cancellationToken = default)
        => EjecutarAsync(nameof(ConsultarMantenimientosPendientesAsync), async () =>
        {
            var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
            if (!usuario.Roles.Any(r => r is "Administrador" or "Tecnico"))
                throw new UnauthorizedAccessException();
            var seleccionado = await ResolverParqueAsync(parque, cancellationToken);
            var paneles = (await NodosAsync(seleccionado.Id, cancellationToken))
                .SelectMany(n => n.Paneles).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var tareas = await _mantenimiento.ListarAsync(new FiltroTareasDto
                { Estado = EstadoTareaMantenimiento.Pendiente }, cancellationToken);
            var pendientes = tareas.Where(t => t.Estado == EstadoTareaMantenimiento.Pendiente
                    && paneles.Contains(t.PanelId.ToString("D")))
                .Select(t => new PendienteChatDto(t.Id, t.PanelId, t.NumeroSerie, t.Tipo,
                    t.Descripcion, t.FechaProgramada, t.Vencida)).ToArray();
            return new(nameof(ConsultarMantenimientosPendientesAsync), EstadoConsultaChat.Correcto,
                pendientes.Length == 0 ? $"No hay mantenimientos pendientes en el parque «{seleccionado.Nombre}»."
                    : "Datos consultados en SolarUY.", pendientes);
        }, cancellationToken);

    private async Task<ResultadoConsultaChatDto> GeneracionAsync(string herramienta, FiltroHistoricoDto filtro,
        string mensajeSinDatos, CancellationToken cancellationToken)
    {
        var datos = await _historico.ConsultarAsync(filtro, cancellationToken);
        return Resultado(herramienta, datos, datos.Cobertura == CoberturaHistorico.SinDatos, mensajeSinDatos);
    }

    private async Task<ParqueMonitoreoDto> ResolverParqueAsync(string referencia, CancellationToken cancellationToken)
        => ResolverUnico((await _monitoreo.ListarParquesAsync(cancellationToken))
            .Where(p => Coincide(referencia, p.Id, p.Nombre)).ToArray());

    private async Task<IReadOnlyList<InventarioNodoInversorDto>> NodosAsync(Guid parqueId, CancellationToken cancellationToken)
        => (await _inventario.ListarAsync(cancellationToken)).Where(n => n.ParqueId == parqueId).ToArray();

    private static bool Coincide(string referencia, Guid id, string nombre)
        => !string.IsNullOrWhiteSpace(referencia) && (Guid.TryParse(referencia, out var buscado) ? buscado == id
            : string.Equals(referencia.Trim(), nombre, StringComparison.OrdinalIgnoreCase));

    private static T ResolverUnico<T>(T[] coincidencias) => coincidencias.Length switch
    {
        0 => throw new KeyNotFoundException(),
        1 => coincidencias[0],
        _ => throw new ArgumentException("La referencia es ambigua; indica el identificador del objetivo.")
    };

    private static DateTime FechaLocal(DateTime fecha)
    {
        if (fecha == default || fecha.Kind != DateTimeKind.Unspecified)
            throw new ArgumentException("Indica una fecha local de Uruguay sin sufijo de zona horaria.");
        return fecha;
    }

    private static ResultadoConsultaChatDto Resultado(string herramienta, object datos, bool vacio, string mensajeSinDatos)
        => new(herramienta, vacio ? EstadoConsultaChat.SinDatos : EstadoConsultaChat.Correcto,
            vacio ? mensajeSinDatos : "Datos consultados en SolarUY.", datos);

    private async Task<ResultadoConsultaChatDto> EjecutarAsync(string herramienta,
        Func<Task<ResultadoConsultaChatDto>> consulta, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await AutorizarAsync(cancellationToken);
            var resultado = await consulta();
            var actual = await AutorizarAsync(cancellationToken);
            if (usuario.Id != actual.Id || !usuario.Roles.Order().SequenceEqual(actual.Roles.Order()))
                throw new UnauthorizedAccessException();
            return resultado;
        }
        catch (OperationCanceledException) { throw; }
        catch (UnauthorizedAccessException)
        { return new(herramienta, EstadoConsultaChat.NoAutorizado, "No tienes permiso para esta consulta."); }
        catch (KeyNotFoundException)
        { return new(herramienta, EstadoConsultaChat.NoEncontrado, "El objetivo solicitado no existe o no está disponible para tu usuario."); }
        catch (ArgumentException)
        { return new(herramienta, EstadoConsultaChat.EntradaInvalida, "Selecciona un objetivo inequívoco y un período/fecha válidos."); }
        catch (Exception)
        { return new(herramienta, EstadoConsultaChat.NoDisponible, "La fuente de datos no está disponible; no se pudo completar esta consulta."); }
    }

    private async Task<UsuarioActualDto> AutorizarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(r => r is "Administrador" or "Tecnico" or "Consulta"))
            throw new UnauthorizedAccessException();
        return usuario;
    }
}
