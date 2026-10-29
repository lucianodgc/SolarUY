using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Application.Inversores;

namespace SolarUY.Infrastructure.Inversores.Tcp;

internal static class InversorTcpProtocol
{
    public const int MaximoBytesPorLinea = 1024 * 1024;
    public static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public static async Task<string> LeerLineaAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var linea = new MemoryStream();
        var buffer = new byte[1024];

        while (true)
        {
            var leidos = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (leidos == 0)
                throw new EndOfStreamException("La conexión terminó antes de completar la respuesta JSON Lines.");

            var finLinea = Array.IndexOf(buffer, (byte)'\n', 0, leidos);
            var cantidad = finLinea >= 0 ? finLinea : leidos;
            if (linea.Length + cantidad > MaximoBytesPorLinea)
                throw new InvalidDataException("El mensaje TCP excede el tamaño permitido.");

            linea.Write(buffer, 0, cantidad);
            if (finLinea >= 0)
                break;
        }

        var texto = Encoding.UTF8.GetString(linea.ToArray());
        return texto.EndsWith('\r') ? texto[..^1] : texto;
    }

    public static async Task EscribirLineaAsync<T>(NetworkStream stream, T valor, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(valor, OpcionesJson);
        if (json.Length > MaximoBytesPorLinea)
            throw new InvalidDataException("El mensaje TCP excede el tamaño permitido.");

        var linea = new byte[json.Length + 1];
        json.CopyTo(linea, 0);
        linea[^1] = (byte)'\n';
        await stream.WriteAsync(linea.AsMemory(), cancellationToken);
    }
}

internal sealed record SolicitudInversorTcp(
    string Operacion,
    string? NodoId = null,
    string? PanelId = null,
    bool? Activo = null,
    IReadOnlyList<PanelMedicionDto>? Paneles = null,
    DateTime? InstanteUtc = null,
    IReadOnlyList<InventarioNodoInversorDto>? Nodos = null);

internal sealed record RespuestaInversorTcp(
    bool Exito,
    JsonElement? Resultado = null,
    string? CodigoError = null,
    string? Mensaje = null);

internal static class OperacionesInversorTcp
{
    public const string MedicionesRegistradas = "mediciones-registradas";
    public const string ListarNodos = "listar-nodos";
    public const string ObtenerEstadoPaneles = "obtener-estado-paneles";
    public const string ObtenerPotencia = "obtener-potencia";
    public const string ActivarPanel = "activar-panel";
    public const string DesactivarPanel = "desactivar-panel";
    public const string ActivarVolcado = "activar-volcado";
    public const string DesactivarVolcado = "desactivar-volcado";
    public const string ObtenerEstadoVolcado = "obtener-estado-volcado";
    public const string ObtenerEstadoEncendido = "obtener-estado-encendido";
    public const string EstablecerEstadoEncendido = "establecer-estado-encendido";
    public const string SincronizarInventario = "sincronizar-inventario";
}
