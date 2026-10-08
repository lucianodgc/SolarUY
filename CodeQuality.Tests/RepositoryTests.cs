using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static CodeQuality.Tests.ArchitectureSetup;

namespace CodeQuality.Tests;

public class RepositoryTests
{
    private readonly Architecture _architecture = ArchitectureSetup.Architecture;

    [Fact]
    public void RepositoryImplementations_ShouldResideInInfrastructure()
    {
        IArchRule rule = Classes()
            .That().HaveNameEndingWith("Repository")
            .Should().ResideInNamespaceMatching(InfrastructureNamespace)
            .Because("Las implementaciones concretas de los repositorios deben estar en la capa de Infraestructura.")
            .WithoutRequiringPositiveResults();

        // Este caso de uso usa IApplicationDbContext y no exige crear repositorios.
        // Si existen repositorios, deben satisfacer la misma regla de ubicación.
        rule.Check(_architecture);
    }

    [Fact]
    public void RepositoryInterfaces_ShouldStartWithIAndEndWithRepository()
    {
        IArchRule rule = Interfaces()
            .That().HaveNameEndingWith("Repository")
            .Should().HaveNameStartingWith("I")
            .Because("Las interfaces de repositorios deben seguir la convención 'IRepository'.")
            .WithoutRequiringPositiveResults();

        // La ausencia de interfaces Repository es válida; sus nombres siguen controlados cuando existen.
        rule.Check(_architecture);
    }
}
