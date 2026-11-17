using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Chatbot;
using SolarUY.Application.HistoricoGeneracion;
using SolarUY.Domain.Enums;
using SolarUY.Infrastructure.Chatbot;

namespace SolarUY.Integration.Tests;

public sealed class ChatbotIntegrationTests
{
    [Fact]
    public async Task Mcp_DescubreSeisHerramientasYConsultaTelemetriaSinEscribir()
    {
        await using var escenario = new ChatbotEscenario();
        await using var scope = escenario.Servicios.CreateAsyncScope();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var sesion = await scope.ServiceProvider.GetRequiredService<ConexionMcpChatbot>().AbrirAsync(limite.Token);
        var herramientas = await sesion.Cliente.ListToolsAsync(cancellationToken: limite.Token);
        Assert.Equal(6, herramientas.Count);
        Assert.Equal(CatalogoMcpChatbot.Crear().Select(t => t.Name).Order(), herramientas.Select(t => t.Name).Order());
        var respuesta = await sesion.Cliente.CallToolAsync("ConsultarEstadoActualAsync",
            new Dictionary<string, object?> { ["parque"] = escenario.Parque.Id.ToString() }, cancellationToken: limite.Token);
        var datos = respuesta.StructuredContent!.Value;
        Assert.Equal("Correcto", datos.GetProperty("estado").GetString());
        Assert.Equal(120m, datos.GetProperty("datos").GetProperty("nodos")[0].GetProperty("potenciaGeneradaW").GetDecimal());
        Assert.Equal(0, escenario.Inversor.Escrituras);
        Assert.Equal(0, escenario.Escrituras);
    }

    [Theory]
    [InlineData("Administrador", 2)]
    [InlineData("Tecnico", 1)]
    [InlineData("Consulta", -1)]
    public async Task Mcp_MantenimientoConservaPermisosDelServicioExistente(string rol, int cantidad)
    {
        await using var escenario = new ChatbotEscenario(rol);
        await using var scope = escenario.Servicios.CreateAsyncScope();
        await using var sesion = await scope.ServiceProvider.GetRequiredService<ConexionMcpChatbot>().AbrirAsync();
        var respuesta = await sesion.Cliente.CallToolAsync("ConsultarMantenimientosPendientesAsync",
            new Dictionary<string, object?> { ["parque"] = escenario.Parque.Nombre });
        var datos = respuesta.StructuredContent!.Value;
        if (cantidad < 0)
        { Assert.Equal("NoAutorizado", datos.GetProperty("estado").GetString()); Assert.Equal(JsonValueKind.Null, datos.GetProperty("datos").ValueKind); }
        else
        { Assert.Equal(cantidad, datos.GetProperty("datos").GetArrayLength()); Assert.DoesNotContain("puedeEditar", datos.GetRawText()); }
        Assert.Equal(0, escenario.Escrituras);
    }

    [Theory]
    [InlineData("Diario")]
    [InlineData("Mensual")]
    [InlineData("Anual")]
    public async Task Mcp_HistoricoCoincideConServicioYConservaCobertura(string periodo)
    {
        await using var escenario = new ChatbotEscenario();
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IHistoricoGeneracionService>();
        var esperado = await servicio.ConsultarAsync(new(TipoObjetivoHistorico.Parque, escenario.Parque.Id,
            Enum.Parse<PeriodoHistorico>(periodo), new DateTime(2026, 11, 16)));
        await using var sesion = await scope.ServiceProvider.GetRequiredService<ConexionMcpChatbot>().AbrirAsync();
        var respuesta = await sesion.Cliente.CallToolAsync("ConsultarGeneracionHistoricaAsync", new Dictionary<string, object?>
        { ["objetivo"] = escenario.Parque.Nombre, ["tipo"] = "Parque", ["periodo"] = periodo, ["fechaLocal"] = "2026-11-16" });
        var datos = respuesta.StructuredContent!.Value.GetProperty("datos");
        Assert.Equal(esperado.EnergiaKWh, datos.GetProperty("energiaKWh").GetDecimal());
        Assert.Equal(esperado.Consulta.InicioUtc, datos.GetProperty("consulta").GetProperty("inicioUtc").GetDateTime());
        Assert.Equal(esperado.Consulta.FinUtc, datos.GetProperty("consulta").GetProperty("finUtc").GetDateTime());
        Assert.Equal("kWh", datos.GetProperty("unidad").GetString());
        Assert.Equal("Parcial", datos.GetProperty("cobertura").GetString());
        Assert.Equal(2, datos.GetProperty("lecturasSinNodoHistorico").GetInt64());
    }

    [Fact]
    public async Task Mcp_HoySeResuelveEnUruguayYFallasNoSeInfierenDeCero()
    {
        await using var escenario = new ChatbotEscenario();
        await using var scope = escenario.Servicios.CreateAsyncScope();
        await using var sesion = await scope.ServiceProvider.GetRequiredService<ConexionMcpChatbot>().AbrirAsync();
        var args = new Dictionary<string, object?> { ["parque"] = escenario.Parque.Nombre };
        await sesion.Cliente.CallToolAsync("ConsultarGeneracionDiariaAsync", args);
        Assert.Equal(new DateTime(2026, 11, 16, 3, 0, 0, DateTimeKind.Utc), escenario.Historico.UltimaConsulta!.InicioUtc);
        var fallas = await sesion.Cliente.CallToolAsync("ConsultarPanelesConFallaAsync", args);
        var json = fallas.StructuredContent!.Value.GetProperty("datos");
        Assert.Equal(2, json.GetArrayLength());
        Assert.All(json.EnumerateArray(), falla => Assert.Equal(escenario.SegundoPanel.Id.ToString("D"), falla.GetProperty("panelId").GetString()));
        Assert.Contains("Salud actual", json.GetRawText());
        Assert.Contains("Estado registrado", json.GetRawText());
    }

    [Fact]
    public async Task AgentFramework_InvocaHerramientasPorMcpYRespondeConEvidenciaDelTurno()
    {
        await using var escenario = new ChatbotEscenario();
        foreach (var nombre in new[] { "ListarParquesAsync", "ConsultarEstadoActualAsync", "ConsultarGeneracionDiariaAsync",
                     "ConsultarPanelesConFallaAsync", "ConsultarMantenimientosPendientesAsync" })
            escenario.Modelo.Llamadas.Enqueue((nombre, nombre == "ListarParquesAsync" ? [] : new() { ["parque"] = escenario.Parque.Nombre }));
        escenario.Modelo.Responder = mensajes => string.Join("\n", mensajes.SelectMany(m => m.Contents)
            .OfType<Microsoft.Extensions.AI.FunctionResultContent>().Select(r => JsonSerializer.Serialize(r.Result)));
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>()
            .EnviarPreguntaAsync(new("¿Cómo está funcionando el Parque norte hoy?"));
        Assert.Equal(EstadoConsultaChat.Correcto, respuesta.Estado);
        Assert.Equal(5, respuesta.Consultas.Count);
        Assert.Contains("0.7", respuesta.Texto);
        Assert.Contains("120", respuesta.Texto);
        Assert.Contains("Tarea propia", respuesta.Texto);
        Assert.Equal(0, escenario.Inversor.Escrituras);
        Assert.Equal(0, escenario.Escrituras);
    }

    [Fact]
    public async Task AgentFramework_ResumenGeneralConsultaCadaApartadoAunqueElModeloNoLoSolicite()
    {
        await using var escenario = new ChatbotEscenario();
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>()
            .EnviarPreguntaAsync(new("¿Cómo está funcionando todo hoy?"));
        Assert.Equal(EstadoConsultaChat.Correcto, respuesta.Estado);
        Assert.Equal(9, respuesta.Consultas.Count);
        foreach (var nombre in new[] { "ConsultarEstadoActualAsync", "ConsultarGeneracionDiariaAsync",
                     "ConsultarPanelesConFallaAsync", "ConsultarMantenimientosPendientesAsync" })
            Assert.Equal(2, respuesta.Consultas.Count(c => c.Herramienta == nombre));
        Assert.Equal(0, escenario.Inversor.Escrituras);
    }

    [Fact]
    public async Task Chat_SinFallasNiPendientesConDatosNoAgregaAvisoDeSeleccionVacia()
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Tareas.Clear();
        escenario.Inventario[0] = escenario.Inventario[0] with
        {
            Paneles = escenario.Inventario[0].Paneles.Select(p => p with { Estado = EstadoPanel.Activo }).ToArray()
        };
        escenario.Inversor.Paneles = escenario.Inversor.Paneles
            .Select(p => p with { EstadoSalud = EstadoSaludPanel.Saludable }).ToArray();
        foreach (var nombre in new[] { "ListarParquesAsync", "ConsultarEstadoActualAsync", "ConsultarGeneracionDiariaAsync",
                     "ConsultarPanelesConFallaAsync", "ConsultarMantenimientosPendientesAsync" })
            escenario.Modelo.Llamadas.Enqueue((nombre, nombre == "ListarParquesAsync" ? [] : new() { ["parque"] = escenario.Parque.Nombre }));
        const string texto = "Parque norte: potencia 120 W y energía 0,7 kWh; sin fallas ni mantenimientos pendientes.";
        escenario.Modelo.Responder = _ => texto;
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>()
            .EnviarPreguntaAsync(new("¿Cómo está funcionando el Parque norte hoy?"));

        Assert.Equal(EstadoConsultaChat.Correcto, respuesta.Estado);
        Assert.Equal(texto, respuesta.Texto);
        Assert.Equal(5, respuesta.Consultas.Count);
        Assert.All(respuesta.Consultas, r => Assert.Equal(EstadoConsultaChat.Correcto, r.Estado));
        foreach (var nombre in new[] { "ConsultarPanelesConFallaAsync", "ConsultarMantenimientosPendientesAsync" })
        {
            var resultado = Assert.Single(respuesta.Consultas, r => r.Herramienta == nombre);
            Assert.Equal(0, ((JsonElement)resultado.Datos!).GetArrayLength());
            Assert.Contains(escenario.Parque.Nombre, resultado.Mensaje);
        }
        Assert.Equal(0, escenario.Escrituras);
        Assert.Equal(0, escenario.Inversor.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chat_ResumenConDatosConservaAvisosRealesDeGeneracion(bool fuenteCaida)
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Historico.SinDatos = !fuenteCaida;
        escenario.Historico.Falla = fuenteCaida;
        foreach (var nombre in new[] { "ConsultarEstadoActualAsync", "ConsultarGeneracionDiariaAsync" })
            escenario.Modelo.Llamadas.Enqueue((nombre, new() { ["parque"] = escenario.Parque.Nombre }));
        escenario.Modelo.Responder = _ => "Potencia actual: 120 W.";
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>()
            .EnviarPreguntaAsync(new("¿Cuál es la potencia y generación del Parque norte?"));

        Assert.Equal(EstadoConsultaChat.Correcto, respuesta.Estado);
        var generacion = Assert.Single(respuesta.Consultas, r => r.Herramienta == "ConsultarGeneracionDiariaAsync");
        Assert.Equal(fuenteCaida ? EstadoConsultaChat.NoDisponible : EstadoConsultaChat.SinDatos, generacion.Estado);
        Assert.Contains(generacion.Mensaje, respuesta.Texto);
        Assert.DoesNotContain("Consulta realizada: no hay datos para esta selección.", respuesta.Texto);
        if (!fuenteCaida)
        {
            Assert.Contains("Generación diaria", generacion.Mensaje);
            Assert.Contains(escenario.Parque.Nombre, generacion.Mensaje);
            Assert.Contains("2026-11-16", generacion.Mensaje);
        }
        Assert.Equal(0, escenario.Escrituras);
        Assert.Equal(0, escenario.Inversor.Escrituras);
    }

    [Fact]
    public async Task Mcp_SinEquiposMantieneSinDatosEnEstadoYFallas()
    {
        await using var escenario = new ChatbotEscenario();
        await using var scope = escenario.Servicios.CreateAsyncScope();
        await using var sesion = await scope.ServiceProvider.GetRequiredService<ConexionMcpChatbot>().AbrirAsync();
        foreach (var nombre in new[] { "ConsultarEstadoActualAsync", "ConsultarPanelesConFallaAsync" })
        {
            var respuesta = await sesion.Cliente.CallToolAsync(nombre,
                new Dictionary<string, object?> { ["parque"] = escenario.OtroParque.Nombre });
            var resultado = respuesta.StructuredContent!.Value;
            Assert.Equal("SinDatos", resultado.GetProperty("estado").GetString());
            Assert.Contains(escenario.OtroParque.Nombre, resultado.GetProperty("mensaje").GetString());
            Assert.DoesNotContain("No se encontraron paneles con fallas", resultado.GetProperty("mensaje").GetString());
        }
        Assert.Equal(0, escenario.Escrituras);
        Assert.Equal(0, escenario.Inversor.Escrituras);
    }
}
