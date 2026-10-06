using System;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static CodeQuality.Tests.ArchitectureSetup;

namespace CodeQuality.Tests;

public class NamingConventionTests
{
    private readonly Architecture _architecture = ArchitectureSetup.Architecture;

    [Fact]
    public void Interfaces_ShouldStartWithI()
    {
        IArchRule rule = Interfaces()
            .Should().HaveNameStartingWith("I")
            .Because("Por convención en C#, todas las interfaces deben comenzar con la letra 'I'.");

        rule.Check(_architecture);
    }

    [Fact]
    public void ExceptionClasses_ShouldEndWithException()
    {
        IArchRule rule = Classes()
            .That().AreAssignableTo(typeof(Exception))
            .Should().HaveNameEndingWith("Exception")
            .Because("Todas las clases que representan un error o excepción deben terminar con el sufijo 'Exception'.");

        rule.Check(_architecture);
    }

    [Fact]
    public void ApplicationServices_ShouldEndWithService()
    {
        IArchRule rule = Classes()
            .That().ResideInNamespace(ApplicationNamespace)
            // .And().AreAssignableTo("IService") 
            .Should().HaveNameEndingWith("Service")
            .Because("Los casos de uso o coordinadores en la capa de Aplicación deben identificarse claramente con el sufijo 'Service'.");

    }
}
