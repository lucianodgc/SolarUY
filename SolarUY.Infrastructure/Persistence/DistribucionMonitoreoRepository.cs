using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SolarUY.Application.Monitoreo;

namespace SolarUY.Infrastructure.Persistence;

public sealed class DistribucionMonitoreoRepository : IDistribucionMonitoreoRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    public DistribucionMonitoreoRepository(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<AvisoMonitoreoDto>> LeerPendientesAsync(string instanciaId, int limite, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanciaId);
        if (instanciaId.Length > 100 || limite <= 0) throw new ArgumentOutOfRangeException(nameof(limite));
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT IGNORE INTO CursoresMonitoreo (InstanciaId, Secuencia) VALUES ({instanciaId}, 0)", cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var comando = context.Database.GetDbConnection().CreateCommand();
        comando.CommandText = "SELECT a.Secuencia, a.Contenido FROM AvisosMonitoreo a JOIN CursoresMonitoreo c ON c.InstanciaId = @instancia WHERE a.Secuencia > c.Secuencia ORDER BY a.Secuencia LIMIT @limite";
        var id = comando.CreateParameter(); id.ParameterName = "@instancia"; id.Value = instanciaId; comando.Parameters.Add(id);
        var maximo = comando.CreateParameter(); maximo.ParameterName = "@limite"; maximo.Value = limite; comando.Parameters.Add(maximo);
        await using var reader = await comando.ExecuteReaderAsync(cancellationToken);
        var avisos = new List<AvisoMonitoreoDto>();
        while (await reader.ReadAsync(cancellationToken))
            avisos.Add(new(reader.GetInt64(0), JsonSerializer.Deserialize<LecturaConfirmadaDto[]>(reader.GetString(1))
                ?? throw new InvalidOperationException("El aviso persistido no tiene contenido válido.")));
        return avisos;
    }

    public async Task ConfirmarAsync(string instanciaId, long secuencia, CancellationToken cancellationToken)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE CursoresMonitoreo SET Secuencia = GREATEST(Secuencia, {secuencia}) WHERE InstanciaId = {instanciaId}", cancellationToken);
    }
}
