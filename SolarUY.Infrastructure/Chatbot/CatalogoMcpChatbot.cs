using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace SolarUY.Infrastructure.Chatbot;

public static class CatalogoMcpChatbot
{
    internal static readonly JsonSerializerOptions Json = CrearJson();

    public static IReadOnlyList<Tool> Crear() =>
    [
        Crear("ListarParquesAsync", "Lista todos los parques autorizados con nombre y Guid. Permite responder cuántos parques hay. Para ampliar o detallar cada parque, consultar también las herramientas de estado, generación, fallas y pendientes.", [], []),
        Crear("ConsultarEstadoActualAsync", "Consulta nodos, paneles, potencia en W, actividad y salud actuales de un parque. Las listas permiten contar nodos y paneles. Invocar por cada parque si piden detalles de todos. Los indicadores de encendido y volcado pertenecen al simulador global.", ["parque"], ["parque"]),
        Crear("ConsultarPanelesConFallaAsync", "Consulta fallas actuales y estados registrados, diferenciando el origen. Cero potencia no significa falla.", ["parque"], ["parque"]),
        Crear("ConsultarGeneracionDiariaAsync", "Consulta energía diaria en kWh según el histórico. Sin fechaLocal usa hoy en Uruguay. Conserva cobertura y datos faltantes.", ["parque", "fechaLocal"], ["parque"]),
        Crear("ConsultarGeneracionHistoricaAsync", "Consulta generación histórica por objetivo (nombre exacto o Guid), tipo Parque/Nodo/Panel y periodo Diario/Mensual/Anual. fechaLocal es yyyy-MM-dd.", ["objetivo", "tipo", "periodo", "fechaLocal"], ["objetivo", "tipo", "periodo", "fechaLocal"]),
        Crear("ConsultarMantenimientosPendientesAsync", "Consulta tareas Pendiente: administrador todas las autorizadas, técnico solo propias; Consulta sin acceso.", ["parque"], ["parque"])
    ];

    private static Tool Crear(string nombre, string descripcion, string[] propiedades, string[] obligatorias)
    {
        var campos = propiedades.ToDictionary(p => p, p => (object)(p == "fechaLocal"
            ? new { type = "string", description = "Fecha local de Uruguay yyyy-MM-dd; omitir para hoy cuando sea opcional." }
            : new { type = "string", description = p == "parque" ? "Nombre exacto o Guid del parque autorizado." : p }));
        return new Tool
        {
            Name = nombre, Description = descripcion,
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = campos,
                required = obligatorias, additionalProperties = false }),
            Annotations = new ToolAnnotations { ReadOnlyHint = true, DestructiveHint = false, OpenWorldHint = false }
        };
    }

    private static JsonSerializerOptions CrearJson()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opciones.Converters.Add(new JsonStringEnumConverter());
        return opciones;
    }
}
