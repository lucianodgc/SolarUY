using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace CodeQuality.Tests;

public static class ArchitectureSetup
{
    public static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(SolarUY.Domain.AssemblyReference).Assembly,
            typeof(SolarUY.Application.AssemblyReference).Assembly,
            typeof(SolarUY.Infrastructure.AssemblyReference).Assembly,
            typeof(SolarUY.Presentation.AssemblyReference).Assembly
            )
        .Build();

    public const string DomainNamespace = ".*Domain.*";
    public const string ApplicationNamespace = ".*Application.*";
    public const string InfrastructureNamespace = ".*Infrastructure.*";
    public const string PresentationNamespace = ".*Presentation.*";
}
