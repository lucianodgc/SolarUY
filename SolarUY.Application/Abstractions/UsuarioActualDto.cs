namespace SolarUY.Application.Abstractions;

public sealed record UsuarioActualDto(
    bool EstaAutenticado,
    string? Id,
    IReadOnlyCollection<string> Roles);
