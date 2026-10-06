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
            .Should().ResideInNamespace(InfrastructureNamespace)
            .Because("Las implementaciones concretas de los repositorios deben estar en la capa de Infraestructura.");

        rule.Check(_architecture);
    }

    [Fact]
    public void RepositoryInterfaces_ShouldStartWithIAndEndWithRepository()
    {
        IArchRule rule = Interfaces()
            .That().HaveNameEndingWith("Repository")
            .Should().HaveNameStartingWith("I")
            .Because("Las interfaces de repositorios deben seguir la convención 'IRepository'.");

        rule.Check(_architecture);
    }
}
