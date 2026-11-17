using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SolarUY.Application.Abstractions;

namespace SolarUY.Application.Chatbot;

public sealed class ChatbotService : IChatbotService
{
    private readonly IChatbotClient _cliente;
    private readonly IUsuarioActual _usuarioActual;
    private readonly SemaphoreSlim _turno = new(1, 1);
    private readonly List<MensajeChatDto> _historial = [];
    private readonly List<MensajeChatDto> _conversacion = [];
    private string? _identidad;

    public ChatbotService(IChatbotClient cliente, IUsuarioActual usuarioActual)
    { _cliente = cliente; _usuarioActual = usuarioActual; }

    public async Task<RespuestaChatDto> EnviarPreguntaAsync(PreguntaChatDto pregunta, CancellationToken cancellationToken = default)
    {
        if (!await _turno.WaitAsync(0, cancellationToken))
            return Respuesta("Espera a que termine la consulta actual.", EstadoConsultaChat.EntradaInvalida);
        try
        {
            var usuario = await AutorizarAsync(cancellationToken);
            var identidad = Identidad(usuario);
            if (_identidad != identidad) { LimpiarConversacion(); _identidad = identidad; }
            if (string.IsNullOrWhiteSpace(pregunta?.Texto))
                return Respuesta("Escribe una pregunta para consultar SolarUY.", EstadoConsultaChat.EntradaInvalida);
            if (SolicitaAccion(pregunta.Texto))
            {
                var rechazo = Respuesta("El chatbot únicamente puede consultar información. No puedo modificar datos ni ejecutar órdenes sobre equipos.",
                    EstadoConsultaChat.EntradaInvalida);
                GuardarIntercambio(pregunta.Texto, rechazo);
                return rechazo;
            }
            var mensajes = _historial.Append(new MensajeChatDto(true, pregunta.Texto.Trim(),
                PlanResumenChatbot.EsResumenDiario(pregunta.Texto))).ToArray();
            var cortesia = ResponderCortesia(pregunta.Texto);
            var resultado = cortesia is null
                ? await _cliente.ConsultarAsync(mensajes, cancellationToken)
                : Respuesta(cortesia, EstadoConsultaChat.Correcto);
            cancellationToken.ThrowIfCancellationRequested();
            if (Identidad(await AutorizarAsync(cancellationToken)) != identidad)
            { LimpiarConversacion(); throw new UnauthorizedAccessException(); }
            if (resultado.Estado == EstadoConsultaChat.NoAutorizado)
            { LimpiarConversacion(); return resultado; }
            if (cortesia is null && resultado.Estado == EstadoConsultaChat.Correcto && resultado.Consultas.Count == 0)
                resultado = Respuesta("No se obtuvieron consultas que respalden una respuesta sobre SolarUY.", EstadoConsultaChat.SinDatos);
            else if (resultado.Estado is EstadoConsultaChat.Correcto or EstadoConsultaChat.SinDatos or EstadoConsultaChat.RequiereAclaracion)
            { _historial.Add(mensajes[^1]); _historial.Add(new(false, resultado.Texto)); }
            GuardarIntercambio(pregunta.Texto, resultado);
            return resultado;
        }
        catch (UnauthorizedAccessException)
        { LimpiarConversacion(); return Respuesta("Tu sesión no permite realizar esta consulta.", EstadoConsultaChat.NoAutorizado); }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        { return Respuesta("El asistente no está disponible en este momento. Puedes volver a intentar.", EstadoConsultaChat.NoDisponible); }
        finally { _turno.Release(); }
    }

    public async Task<IReadOnlyList<MensajeChatDto>> ObtenerConversacionAsync(CancellationToken cancellationToken = default)
    {
        await _turno.WaitAsync(cancellationToken);
        try
        {
            var identidad = Identidad(await AutorizarAsync(cancellationToken));
            if (_identidad != identidad) { LimpiarConversacion(); _identidad = identidad; }
            return _conversacion.ToArray();
        }
        catch (UnauthorizedAccessException) { LimpiarConversacion(); return []; }
        finally { _turno.Release(); }
    }

    public async Task ReiniciarAsync(CancellationToken cancellationToken = default)
    {
        await _turno.WaitAsync(cancellationToken);
        try { LimpiarConversacion(); }
        finally { _turno.Release(); }
    }

    private void GuardarIntercambio(string pregunta, RespuestaChatDto respuesta)
    {
        _conversacion.Add(new(true, pregunta.Trim()));
        _conversacion.Add(new(false, respuesta.Texto));
    }

    private void LimpiarConversacion()
    { _historial.Clear(); _conversacion.Clear(); _identidad = null; }

    private async Task<UsuarioActualDto> AutorizarAsync(CancellationToken cancellationToken)
    {
        var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);
        if (!usuario.EstaAutenticado || string.IsNullOrWhiteSpace(usuario.Id)
            || !usuario.Roles.Any(r => r is "Administrador" or "Tecnico" or "Consulta"))
            throw new UnauthorizedAccessException();
        return usuario;
    }

    private static string Identidad(UsuarioActualDto usuario) => usuario.Id + ":" + string.Join(",", usuario.Roles.Order());
    private static RespuestaChatDto Respuesta(string texto, EstadoConsultaChat estado) => new(texto, estado, []);

    private static string? ResponderCortesia(string texto)
    {
        var normalizado = Regex.Replace(Normalizar(texto), @"[^\p{L}\p{N}]+", " ", RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100)).Trim();
        if (normalizado is "hola" or "buenas" or "buen dia" or "buenos dias" or "buenas tardes" or "buenas noches")
            return "¡Hola! Soy el asistente de SolarUY. Puedo ayudarte a consultar parques, generación, equipos y mantenimientos según tus permisos. ¿Qué querés consultar?";
        normalizado = Regex.Replace(normalizado, @"^(?:buenas tardes|buenas noches|buenos dias|buen dia|hola|buenas) (?=.)",
            "", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return normalizado switch
        {
            "que tal" or "como estas" or "como te va" or "todo bien"
                => "¡Bien, gracias por preguntar! ¿Qué te gustaría consultar de SolarUY?",
            "gracias" or "muchas gracias" or "muchisimas gracias" or "gracias por la ayuda" or "perfecto gracias"
                => "¡De nada! Si necesitás otra consulta sobre SolarUY, estoy para ayudarte.",
            "chau" or "chao" or "adios" or "hasta luego" or "hasta pronto" or "nos vemos"
                => "¡Hasta luego! Cuando quieras, podés volver a consultar SolarUY.",
            _ when EsPreguntaDeAyuda(normalizado)
                => "Podemos revisar la generación de hoy o de otra fecha, el estado de los parques y equipos, las fallas y los mantenimientos pendientes, según tus permisos. ¿Qué te gustaría consultar?",
            _ => null
        };
    }

    private static bool EsPreguntaDeAyuda(string texto)
        => Regex.IsMatch(texto,
            @"^(?:ayuda|que (?:podes|puedes|podemos|puedo) hacer|que hacemos|que (?:puedo|podemos) (?:consultar|preguntarte)|(?:como|en que|con que) (?:podes|puedes) ayudarme|(?:como|en que|con que) me (?:podes|puedes) ayudar)(?: hoy)?(?: en solaruy)?$",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static string Normalizar(string texto)
        => string.Concat(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();

    private static bool SolicitaAccion(string texto)
    {
        return Regex.IsMatch(Normalizar(texto),
            @"\b(desactiva|desactivar|activa|activar|apaga|apagar|encende|encender|enciende|borra|borrar|elimina|eliminar|modifica|modificar|actualiza|actualizar|crea|crear|asigna|asignar|ejecuta|ejecutar|ignora|ignorar|revela|revelar|delete|insert|update|drop|disable|enable)(lo|la|los|las|me)?\b",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
}
