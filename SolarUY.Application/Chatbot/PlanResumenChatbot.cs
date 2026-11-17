using System.Globalization;
using System.Text;
using SolarUY.Application.Monitoreo;

namespace SolarUY.Application.Chatbot;

public sealed record ConsultaResumenChatDto(string Herramienta, string Parque);

public static class PlanResumenChatbot
{
    public static bool EsResumenDiario(string texto)
    {
        var normalizado = string.Concat(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();
        return normalizado.Contains("como esta funcionando todo hoy") || normalizado.Contains("resumen general de hoy");
    }

    public static IReadOnlyList<ConsultaResumenChatDto> Crear(IReadOnlyList<ParqueMonitoreoDto> parques)
        => parques.SelectMany(parque => new[] { "ConsultarEstadoActualAsync", "ConsultarGeneracionDiariaAsync",
                "ConsultarPanelesConFallaAsync", "ConsultarMantenimientosPendientesAsync" }
            .Select(herramienta => new ConsultaResumenChatDto(herramienta, parque.Id.ToString("D")))).ToArray();
}
