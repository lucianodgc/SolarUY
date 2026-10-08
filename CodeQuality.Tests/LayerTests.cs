using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static CodeQuality.Tests.ArchitectureSetup;

namespace CodeQuality.Tests;

public class LayerTests
{
    private readonly Architecture _architecture = ArchitectureSetup.Architecture;

    [Fact]
    public void DomainLayer_ShouldNotHaveDependenciesOnOtherLayers()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespaceMatching(DomainNamespace)
            .Should().NotDependOnAny(
                Classes().That().ResideInNamespaceMatching(ApplicationNamespace)
                .Or().ResideInNamespaceMatching(InfrastructureNamespace)
                .Or().ResideInNamespaceMatching(PresentationNamespace)
            )
            .Because("El Dominio es el núcleo y no debe depender de ninguna otra capa (Clean Architecture).");

        rule.Check(_architecture);
    }

    [Fact]
    public void ApplicationLayer_ShouldNotDependOnInfrastructureOrPresentation()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespaceMatching(ApplicationNamespace)
            .Should().NotDependOnAny(
                Classes().That().ResideInNamespaceMatching(InfrastructureNamespace)
                .Or().ResideInNamespaceMatching(PresentationNamespace)
            )
            .Because("La capa de Aplicación solo debe depender del Dominio.");

        rule.Check(_architecture);
    }

    [Fact]
    public void PresentationLayer_ShouldNotDependOnInfrastructureDirectly()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespaceMatching(PresentationNamespace)
            .Should().NotDependOnAny(
                Classes().That().ResideInNamespaceMatching(InfrastructureNamespace)
            )
            .Because("Blazor (UI) no debe acceder directamente a la base de datos o repositorios reales. Debe usar la capa de Aplicación.");

        rule.Check(_architecture);
    }
}
