using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace SolarUY.Infrastructure.Chatbot;

public static class CatalogoMcpChatbot
{
    internal const string ArgumentoParque = "parque";
    internal const string ArgumentoFechaLocal = "fechaLocal";
    internal static readonly JsonSerializerOptions Json = CrearJson();

    public static IReadOnlyList<Tool> Crear() =>
    [
        Crear("ListarParquesAsync", "Lista todos los parques autorizados con nombre y Guid. Permite responder cuántos parques hay. Para ampliar o detallar cada parque, consultar también las herramientas de estado, generación, fallas y pendientes.", [], []),
        Crear("ConsultarEstadoActualAsync", "Consulta nodos, paneles, potencia en W, actividad y salud actuales de un parque. Las listas permiten contar nodos y paneles. Invocar por cada parque si piden detalles de todos. Los indicadores de encendido y volcado pertenecen al simulador global.", [ArgumentoParque], [ArgumentoParque]),
        Crear("ConsultarPanelesConFallaAsync", "Consulta fallas actuales y estados registrados, diferenciando el origen. Cero potencia no significa falla.", [ArgumentoParque], [ArgumentoParque]),
        Crear("ConsultarGeneracionDiariaAsync", "Consulta energía diaria en kWh según el histórico. Sin fechaLocal usa hoy en Uruguay. Conserva cobertura y datos faltantes.", [ArgumentoParque, ArgumentoFechaLocal], [ArgumentoParque]),
        Crear("ConsultarGeneracionHistoricaAsync", "Consulta generación histórica por objetivo (nombre exacto o Guid), tipo Parque/Nodo/Panel y periodo Diario/Mensual/Anual. fechaLocal es yyyy-MM-dd.", ["objetivo", "tipo", "periodo", ArgumentoFechaLocal], ["objetivo", "tipo", "periodo", ArgumentoFechaLocal]),
        Crear("ConsultarMantenimientosPendientesAsync", "Consulta tareas Pendiente: administrador todas las autorizadas, técnico solo propias; Consulta sin acceso.", [ArgumentoParque], [ArgumentoParque])
    ];

    private static Tool Crear(string nombre, string descripcion, string[] propiedades, string[] obligatorias)
    {
        var campos = propiedades.ToDictionary(p => p, p => new { type = "string", description = DescribirArgumento(p) });
        return new Tool
        {
            Name = nombre, Description = descripcion,
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = campos,
                required = obligatorias, additionalProperties = false }),
            Annotations = new ToolAnnotations { ReadOnlyHint = true, DestructiveHint = false, OpenWorldHint = false }
        };
    }

    private static string DescribirArgumento(string propiedad) => propiedad switch
    {
        ArgumentoFechaLocal => "Fecha local de Uruguay yyyy-MM-dd; omitir para hoy cuando sea opcional.",
        ArgumentoParque => "Nombre exacto o Guid del parque autorizado.",
        _ => propiedad
    };

    private static JsonSerializerOptions CrearJson()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opciones.Converters.Add(new JsonStringEnumConverter());
        return opciones;
    }
}
