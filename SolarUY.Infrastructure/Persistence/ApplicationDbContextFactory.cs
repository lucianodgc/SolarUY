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
            .AddEnvironmentVariables()
            .Build();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        // Generar una migración no necesita una conexión real; database update sí.
        options.UseMySql(new MySqlConnection(configuration.GetConnectionString("SolarUY") ?? string.Empty),
            new MariaDbServerVersion(Version.Parse(configuration["MariaDB:ServerVersion"] ?? "11.8.0")));
        return new ApplicationDbContext(options.Options);
    }
}
