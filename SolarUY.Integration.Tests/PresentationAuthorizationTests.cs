using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using SolarUY.Presentation;

namespace SolarUY.Integration.Tests;

public sealed class PresentationAuthorizationTests
{
    [Theory]
    [InlineData("ParquesSolares", "Administrador,Tecnico,Consulta")]
    [InlineData("AltaParqueSolar", "Administrador")]
    [InlineData("EditarParqueSolar", "Administrador")]
    [InlineData("MantenimientoPaneles", "Administrador,Tecnico")]
    [InlineData("CrearTareaMantenimiento", "Administrador")]
    [InlineData("SimuladorInversor", "Administrador,Tecnico,Consulta")]
    public void Paginas_DeclaranLosRolesAutorizados(string nombrePagina, string rolesEsperados)
    {
        var pagina = typeof(AssemblyReference).Assembly.GetTypes()
            .Single(tipo => tipo.Name == nombrePagina);
        var autorizacion = pagina.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(autorizacion);
        Assert.Equal(rolesEsperados, autorizacion.Roles);
    }
}
