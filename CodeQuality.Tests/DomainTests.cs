using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static CodeQuality.Tests.ArchitectureSetup;

namespace CodeQuality.Tests;

public class DomainTests
{
    private readonly Architecture _architecture = ArchitectureSetup.Architecture;

    [Fact]
    public void DomainLayer_ShouldNotUseEntityFramework()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespace(DomainNamespace)
            .Should().NotDependOnAnyTypesThat().ResideInNamespace("Microsoft.EntityFrameworkCore.*")
            .Because("El dominio no debe estar acoplado al ORM.");

        rule.Check(_architecture);
    }
}
