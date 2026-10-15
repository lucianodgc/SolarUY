using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace SolarUY.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "SolarUY.Presentation", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), optional: true)
            .AddUserSecrets<ApplicationDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("SolarUY");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "No se configuró ConnectionStrings:SolarUY. Guárdala en los user-secrets de SolarUY.Presentation "
                + "o define ConnectionStrings__SolarUY antes de ejecutar dotnet ef.");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        // La versión explícita permite generar migraciones sin abrir una conexión.
        options.UseMySql(new MySqlConnection(connectionString),
            new MariaDbServerVersion(Version.Parse(configuration["MariaDB:ServerVersion"] ?? "11.8.0")));
        return new ApplicationDbContext(options.Options);
    }
}
