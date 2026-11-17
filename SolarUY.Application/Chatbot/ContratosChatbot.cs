using SolarUY.Application.Inversores;

namespace SolarUY.Application.Chatbot;

public enum EstadoConsultaChat { Correcto, SinDatos, EntradaInvalida, NoEncontrado, NoAutorizado, NoDisponible, RequiereAclaracion }
public sealed record PreguntaChatDto(string Texto);
public sealed record MensajeChatDto(bool EsUsuario, string Texto, bool ResumenDiario = false);
public sealed record ResultadoConsultaChatDto(string Herramienta, EstadoConsultaChat Estado, string Mensaje, object? Datos = null);
public sealed record RespuestaChatDto(string Texto, EstadoConsultaChat Estado,
    IReadOnlyList<ResultadoConsultaChatDto> Consultas);
public sealed record EstadoEquiposChatDto(Guid ParqueId, string Parque, DateTime ConsultadoUtc,
    IReadOnlyList<TelemetriaNodoDto> Nodos);
public sealed record FallaPanelChatDto(string PanelId, string NumeroSerie, string Nodo,
    string Origen, DateTime ConsultadoUtc);
public sealed record PendienteChatDto(Guid Id, Guid PanelId, string NumeroSerie, string Tipo,
    string Descripcion, DateTime FechaProgramadaUtc, bool Vencida);

public interface IChatbotService
{
    Task<RespuestaChatDto> EnviarPreguntaAsync(PreguntaChatDto pregunta, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MensajeChatDto>> ObtenerConversacionAsync(CancellationToken cancellationToken = default);
    Task ReiniciarAsync(CancellationToken cancellationToken = default);
}

public interface IChatbotClient
{
    Task<RespuestaChatDto> ConsultarAsync(IReadOnlyList<MensajeChatDto> mensajes, CancellationToken cancellationToken = default);
}

public interface IEstadoEquiposConsultaClient
{
    Task<IReadOnlyList<TelemetriaNodoDto>> ConsultarAsync(IReadOnlyList<InventarioNodoInversorDto> nodos,
        CancellationToken cancellationToken = default);
}

public interface IConsultaChatbotService
{
    Task<ResultadoConsultaChatDto> ListarParquesAsync(CancellationToken cancellationToken = default);
    Task<ResultadoConsultaChatDto> ConsultarEstadoActualAsync(string parque, CancellationToken cancellationToken = default);
    Task<ResultadoConsultaChatDto> ConsultarPanelesConFallaAsync(string parque, CancellationToken cancellationToken = default);
    Task<ResultadoConsultaChatDto> ConsultarGeneracionDiariaAsync(string parque, DateTime? fechaLocal = null,
        CancellationToken cancellationToken = default);
    Task<ResultadoConsultaChatDto> ConsultarGeneracionHistoricaAsync(string objetivo, string tipo, string periodo,
        DateTime fechaLocal, CancellationToken cancellationToken = default);
    Task<ResultadoConsultaChatDto> ConsultarMantenimientosPendientesAsync(string parque, CancellationToken cancellationToken = default);
}
