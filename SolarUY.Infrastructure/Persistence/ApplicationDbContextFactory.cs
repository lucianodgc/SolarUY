using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace SolarUY.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var directorio = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directorio is not null
            && !File.Exists(Path.Combine(directorio.FullName, "SolarUY.Presentation.csproj"))
            && !File.Exists(Path.Combine(directorio.FullName, "SolarUY.Presentation", "SolarUY.Presentation.csproj")))
            directorio = directorio.Parent;
        if (directorio is null)
            throw new InvalidOperationException("Ejecute dotnet ef dentro del árbol de SolarUY.");
        var ruta = File.Exists(Path.Combine(directorio.FullName, "SolarUY.Presentation.csproj"))
            ? directorio.FullName : Path.Combine(directorio.FullName, "SolarUY.Presentation");
        // Misma precedencia que WebApplication: JSON del entorno, secretos solo
        // en Development y variables al final. EF admite --connection para su destino.
        var entorno = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        for (var i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--environment") entorno = args[i + 1];
        var builder = new ConfigurationBuilder()
            .SetBasePath(ruta)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{entorno}.json", optional: true);
        if (entorno.Equals("Development", StringComparison.OrdinalIgnoreCase))
            builder.AddUserSecrets<ApplicationDbContextFactory>(optional: true);
        var configuration = builder.AddEnvironmentVariables().Build();
        var connectionString = configuration.GetConnectionString("SolarUY");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "No se configuró ConnectionStrings:SolarUY. Guárdala en los user-secrets de SolarUY.Presentation "
                + "o define ConnectionStrings__SolarUY antes de ejecutar dotnet ef.");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        var destino = new MySqlConnectionStringBuilder(connectionString);
        var fuente = configuration.Providers.Reverse().First(p => p.TryGet("ConnectionStrings:SolarUY", out _));
        Console.WriteLine($"MariaDB (EF, {entorno}): {destino.Server}:{destino.Port}/{destino.Database}; fuente: {fuente}");
        // La versión explícita permite generar migraciones sin abrir una conexión.
        options.UseMySql(new MySqlConnection(connectionString),
            new MariaDbServerVersion(Version.Parse(configuration["MariaDB:ServerVersion"] ?? "11.8.0")));
        return new ApplicationDbContext(options.Options);
    }
}
