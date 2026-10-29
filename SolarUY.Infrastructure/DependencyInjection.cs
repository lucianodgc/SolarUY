using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Mantenimiento;
using SolarUY.Application.Inversores;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Application.RecoleccionLecturas;
using SolarUY.Infrastructure.Identity;
using SolarUY.Infrastructure.Inversores;
using SolarUY.Infrastructure.Inversores.Tcp;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSolarInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextFactory<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("SolarUY");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Configure ConnectionStrings:SolarUY o ConnectionStrings__SolarUY para acceder a MariaDB.");

            var version = Version.Parse(configuration["MariaDB:ServerVersion"] ?? "11.8.0");
            options.UseMySql(connectionString, new MariaDbServerVersion(version));
        });
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IPanelRepository, PanelRepository>();
        services.AddScoped<IInversorInventarioRepository, InversorInventarioRepository>();
        services.AddScoped<IHistoricoGeneracionRepository, HistoricoGeneracionRepository>();
        services.AddScoped<IRecoleccionLecturasRepository, RecoleccionLecturasRepository>();
        services.AddScoped<ITareaMantenimientoRepository, TareaMantenimientoRepository>();
        services.AddSingleton(_ => new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30)
        });
        services.AddScoped<ITecnicoDirectory, KeycloakTecnicoDirectory>();

        services.AddSingleton(TimeProvider.System);
        services.AddOptions<InversorTcpOptions>()
            .Configure(options => InversorTcpOptions.Cargar(configuration, options))
            .Validate(options => IPAddress.TryParse(options.Host, out _), "InversorTcp:Host debe ser una dirección IP válida.")
            .Validate(options => options.Port is > 0 and <= 65535, "InversorTcp:Port debe estar entre 1 y 65535.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 30, "InversorTcp:TimeoutSeconds debe estar entre 1 y 30.");
        services.AddSingleton<IInversorClient, InversorTcpClient>();
        return services;
    }
}
