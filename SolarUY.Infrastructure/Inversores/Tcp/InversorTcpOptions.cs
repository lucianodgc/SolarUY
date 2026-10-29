using Microsoft.Extensions.Configuration;

namespace SolarUY.Infrastructure.Inversores.Tcp;

public sealed class InversorTcpOptions
{
    public const string SectionName = "InversorTcp";

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5055;
    public int TimeoutSeconds { get; set; } = 3;

    public static void Cargar(IConfiguration configuration, InversorTcpOptions options)
    {
        options.Host = configuration[$"{SectionName}:Host"] ?? options.Host;
        options.Port = LeerEntero(configuration, $"{SectionName}:Port", options.Port);
        options.TimeoutSeconds = LeerEntero(configuration, $"{SectionName}:TimeoutSeconds", options.TimeoutSeconds);
    }

    private static int LeerEntero(IConfiguration configuration, string clave, int valorPredeterminado)
    {
        var valor = configuration[clave];
        if (valor is null) return valorPredeterminado;
        return int.TryParse(valor, out var resultado) ? resultado : -1;
    }
}
