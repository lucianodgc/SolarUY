namespace SolarUY.Domain.Salud;

public sealed class PoliticaSalud
{
    public PoliticaSalud(TimeSpan plazoSinDatos, decimal umbralFalla = 0.5m,
        decimal umbralDegradado = 0.8m, decimal factorEsperado = 1m, bool evaluarPotenciaBaja = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(plazoSinDatos, TimeSpan.Zero);
        if (umbralFalla <= 0 || umbralFalla >= umbralDegradado || umbralDegradado > 1)
            throw new ArgumentOutOfRangeException(nameof(umbralFalla));
        if (factorEsperado <= 0 || factorEsperado > 1) throw new ArgumentOutOfRangeException(nameof(factorEsperado));
        PlazoSinDatos = plazoSinDatos; UmbralFalla = umbralFalla; UmbralDegradado = umbralDegradado;
        FactorEsperado = factorEsperado; EvaluarPotenciaBaja = evaluarPotenciaBaja;
    }

    public TimeSpan PlazoSinDatos { get; }
    public decimal UmbralFalla { get; }
    public decimal UmbralDegradado { get; }
    public decimal FactorEsperado { get; }
    public bool EvaluarPotenciaBaja { get; }
}
