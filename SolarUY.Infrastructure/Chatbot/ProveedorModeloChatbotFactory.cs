using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace SolarUY.Infrastructure.Chatbot;

public sealed class ProveedorModeloChatbotFactory : IModeloChatbotFactory
{
    private readonly IOptions<ChatbotOptions> _opciones;
    private readonly OpenAiModeloChatbotFactory _openAi;
    public ProveedorModeloChatbotFactory(IOptions<ChatbotOptions> opciones, OpenAiModeloChatbotFactory openAi)
    { _opciones = opciones; _openAi = openAi; }
    public IChatClient? Crear()
    {
        var opciones = _opciones.Value;
        if (!opciones.TieneOpcionesValidas) return null;
        return opciones.Proveedor == "OpenAI" ? _openAi.Crear() : new OllamaChatClient(opciones);
    }
}
