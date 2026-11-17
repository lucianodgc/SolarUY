using SolarUY.Application.Inversores;
using SolarUY.Domain.Entities;

namespace SolarUY.Presentation.Monitoreo;

public static class MensajeResultadoOrden
{
    public static string Formatear(ResultadoOrdenInversorDto resultado)
        => $"Intento {resultado.IntentoId?.ToString() ?? "no registrado"}: {resultado.Estado}. " + (resultado.Codigo switch
            {
                "sin-permiso" => "Se requiere Administrador y una sesión con identidad válida. ",
                "inversor-ocupado" => "Hay otra orden en curso. No se envió esta solicitud. ",
                "destino-no-vigente" => "El panel ya no pertenece al nodo confirmado. ",
                "correlacion-incompatible" => "La solicitud repetida tiene un destino o acción diferente. ",
                "sin-envio" => "La preparación falló antes de enviar la orden. ",
                _ => ""
            })
            + (resultado.Estado == EstadoOrdenInversor.Indeterminada ? "No se reenvió. Consultá el estado antes de confirmar un nuevo intento. " : "")
            + (resultado.AuditoriaCompleta ? "Auditoría guardada." : "Auditoría incompleta; el resultado remoto indicado no implica guardado completo.");
}
