using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SolarUY.Application.Abstractions;
using SolarUY.Infrastructure.Inversores;
using SolarUY.Infrastructure.Inversores.Tcp;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<IInversorClient, InversorSimulador>();
builder.Services.AddOptions<InversorTcpOptions>()
    .Configure(options => InversorTcpOptions.Cargar(builder.Configuration, options))
    .Validate(options => IPAddress.TryParse(options.Host, out _), "InversorTcp:Host debe ser una dirección IP válida.")
    .Validate(options => options.Port is > 0 and <= 65535, "InversorTcp:Port debe estar entre 1 y 65535.")
    .Validate(options => options.TimeoutSeconds is > 0 and <= 30, "InversorTcp:TimeoutSeconds debe estar entre 1 y 30.");
builder.Services.AddSingleton<InversorTcpServer>();

using var host = builder.Build();
using var cancelacion = new CancellationTokenSource();
Console.CancelKeyPress += (_, evento) =>
{
    evento.Cancel = true;
    cancelacion.Cancel();
};

try
{
    await host.Services.GetRequiredService<InversorTcpServer>().EjecutarAsync(cancelacion.Token);
}
catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
{
    // Ctrl+C detiene el listener de forma controlada.
}
