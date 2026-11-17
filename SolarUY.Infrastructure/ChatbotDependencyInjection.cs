using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Chatbot;
using SolarUY.Infrastructure.Chatbot;

namespace SolarUY.Infrastructure;

public static class ChatbotDependencyInjection
{
    public static IServiceCollection AddChatbotInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ChatbotOptions>().Configure(options =>
        {
            options.Proveedor = configuration["Chatbot:Proveedor"] ?? options.Proveedor;
            options.Modelo = configuration["Chatbot:Modelo"] ?? (options.Proveedor == "OpenAI" ? "gpt-6-luna" : "qwen3:8b");
            options.EndpointOllama = configuration["Chatbot:Ollama:Endpoint"] ?? options.EndpointOllama;
            options.PermitirHostDocker = bool.TryParse(configuration["Chatbot:Ollama:PermitirHostDocker"], out var permitirDocker)
                && permitirDocker;
            if (configuration["Chatbot:Ollama:Contexto"] is { } contexto)
                options.ContextoOllama = int.TryParse(contexto, out var tokens) ? tokens : 0;
            options.TransporteMcp = configuration["Chatbot:Mcp:Transporte"] ?? options.TransporteMcp;
            if (configuration["Chatbot:TiempoMaximoSegundos"] is { } tiempo)
                options.TiempoMaximoSegundos = int.TryParse(tiempo, out var segundos) ? segundos : 0;
        });
        services.AddScoped<IEstadoEquiposConsultaClient, EstadoEquiposConsultaClient>();
        services.AddScoped<ServidorMcpChatbot>();
        services.AddScoped<ConexionMcpChatbot>();
        services.AddScoped<IConexionMcpChatbot>(provider => provider.GetRequiredService<ConexionMcpChatbot>());
        services.AddScoped<OpenAiModeloChatbotFactory>();
        services.AddScoped<IModeloChatbotFactory, ProveedorModeloChatbotFactory>();
        services.AddScoped<IChatbotClient, ChatbotClient>();
        return services;
    }
}
