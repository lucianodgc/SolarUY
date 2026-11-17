using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace SolarUY.Infrastructure.Chatbot;

public interface IModeloChatbotFactory
{
    IChatClient? Crear();
}

public sealed class OpenAiModeloChatbotFactory : IModeloChatbotFactory
{
    private readonly IConfiguration _configuracion;
    private readonly IOptions<ChatbotOptions> _opciones;

    public OpenAiModeloChatbotFactory(IConfiguration configuracion, IOptions<ChatbotOptions> opciones)
    { _configuracion = configuracion; _opciones = opciones; }

    public IChatClient? Crear()
    {
        var clave = _configuracion["Chatbot:OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(clave) || !_opciones.Value.TieneOpcionesValidas) return null;
        // El SDK marca Responses y este adaptador como experimentales. Se aíslan aquí;
        // no se deshabilitan diagnósticos para el resto del proyecto.
#pragma warning disable OPENAI001, MAAI001
        return new ResponsesClient(clave).AsIChatClientWithStoredOutputDisabled(_opciones.Value.Modelo);
#pragma warning restore OPENAI001, MAAI001
    }
}
