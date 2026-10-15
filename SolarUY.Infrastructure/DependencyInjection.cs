using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Mantenimiento;
using SolarUY.Infrastructure.Identity;
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
        services.AddScoped<ITareaMantenimientoRepository, TareaMantenimientoRepository>();
        services.AddSingleton(_ => new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30)
        });
        services.AddScoped<ITecnicoDirectory, KeycloakTecnicoDirectory>();
        return services;
    }
}
