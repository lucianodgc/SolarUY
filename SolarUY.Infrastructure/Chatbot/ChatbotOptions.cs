namespace SolarUY.Infrastructure.Chatbot;

public sealed class ChatbotOptions
{
    public string Proveedor { get; set; } = "Ollama";
    public string Modelo { get; set; } = "qwen3:8b";
    public string EndpointOllama { get; set; } = "http://127.0.0.1:11434/";
    public int ContextoOllama { get; set; } = 16384;
    public int TiempoMaximoSegundos { get; set; } = 300;
    public string TransporteMcp { get; set; } = "Interno";
    public bool TieneOpcionesValidas => !string.IsNullOrWhiteSpace(Modelo)
        && TiempoMaximoSegundos is > 0 and <= 300 && TransporteMcp == "Interno"
        && (Proveedor == "OpenAI" || (Proveedor == "Ollama" && ContextoOllama is >= 4096 and <= 32768
            && Uri.TryCreate(EndpointOllama, UriKind.Absolute, out var destino) && destino.IsLoopback
            && destino.Scheme == "http" && destino.AbsolutePath == "/" && destino.UserInfo.Length == 0
            && destino.Query.Length == 0 && destino.Fragment.Length == 0));
}
