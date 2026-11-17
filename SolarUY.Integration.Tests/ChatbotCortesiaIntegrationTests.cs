using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Abstractions;
using SolarUY.Application.Chatbot;

namespace SolarUY.Integration.Tests;

public sealed class ChatbotCortesiaIntegrationTests
{
    [Theory]
    [InlineData("hola", "¡Hola!")]
    [InlineData("¡HÓLA!", "¡Hola!")]
    [InlineData("  buenos   días  ", "¡Hola!")]
    [InlineData("buenas tardes", "¡Hola!")]
    [InlineData("buenas noches", "¡Hola!")]
    [InlineData("Hola, ¿cómo estás?", "gracias por preguntar")]
    [InlineData("como estas?", "gracias por preguntar")]
    [InlineData("muchas gracias", "¡De nada!")]
    [InlineData("hasta luego", "¡Hasta luego!")]
    [InlineData("¿Qué podés hacer?", "generación")]
    [InlineData("hola que podemos hacer hoy?", "Podemos revisar")]
    [InlineData("¡Buenas! ¿Qué podemos hacer hoy en SolarUY?", "Podemos revisar")]
    [InlineData("Buenas tardes, ¿qué podemos hacer hoy?", "Podemos revisar")]
    [InlineData("¿Qué hacemos hoy?", "Podemos revisar")]
    [InlineData("Hola, ¿qué puedo consultar?", "Podemos revisar")]
    [InlineData("¿En qué me podés ayudar hoy?", "Podemos revisar")]
    [InlineData("¿Cómo puedes ayudarme?", "Podemos revisar")]
    public async Task Chat_CortesiaRespondeInclusoConProveedorCaidoSinConsultarDatos(string texto, string esperado)
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Modelo.Caido = true;
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new(texto));
        Assert.Equal(EstadoConsultaChat.Correcto, respuesta.Estado);
        Assert.Contains(esperado, respuesta.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(respuesta.Consultas);
        Assert.Equal(0, escenario.Modelo.CantidadSolicitudes);
        Assert.Equal(0, escenario.Inversor.Escrituras);
        Assert.Equal(0, escenario.Escrituras);
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Tecnico")]
    [InlineData("Consulta")]
    public async Task Chat_CortesiaConservaContextoYConsultaPosteriorPorMcp(string rol)
    {
        await using var escenario = new ChatbotEscenario(rol);
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IChatbotService>();
        escenario.Modelo.Llamadas.Enqueue(("ListarParquesAsync", []));
        Assert.Equal(EstadoConsultaChat.Correcto, (await servicio.EnviarPreguntaAsync(new("¿Qué parques hay?"))).Estado);
        var solicitudes = escenario.Modelo.CantidadSolicitudes;
        Assert.Equal(EstadoConsultaChat.Correcto, (await servicio.EnviarPreguntaAsync(new("hola"))).Estado);
        Assert.Equal(solicitudes, escenario.Modelo.CantidadSolicitudes);
        escenario.Modelo.Llamadas.Enqueue(("ConsultarEstadoActualAsync", new() { ["parque"] = escenario.Parque.Nombre }));
        var respuesta = await servicio.EnviarPreguntaAsync(new("¿Cuál es el estado del Parque norte?"));
        Assert.Equal(EstadoConsultaChat.Correcto, respuesta.Estado);
        Assert.Equal("ConsultarEstadoActualAsync", Assert.Single(respuesta.Consultas).Herramienta);
        Assert.Contains(escenario.Modelo.MensajesRecibidos, m => m.Text == "¿Qué parques hay?");
        Assert.Contains(escenario.Modelo.MensajesRecibidos, m => m.Text == "hola");
        Assert.Contains(escenario.Modelo.MensajesRecibidos, m => m.Text.Contains("¡Hola!"));
        Assert.Equal(0, escenario.Inversor.Escrituras);
        Assert.Equal(0, escenario.Escrituras);
    }

    [Theory]
    [InlineData(false, "persona", "Administrador")]
    [InlineData(true, "", "Administrador")]
    [InlineData(true, "persona", "Ajeno")]
    public async Task Chat_CortesiaNoOmiteAutenticacion(bool autenticado, string id, string rol)
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Usuario.Actual = new(autenticado, id, [rol]);
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("hola"));
        Assert.Equal(EstadoConsultaChat.NoAutorizado, respuesta.Estado);
        Assert.DoesNotContain("¡Hola!", respuesta.Texto);
        Assert.Empty(respuesta.Consultas);
        Assert.Equal(0, escenario.Modelo.CantidadSolicitudes);
    }

    [Fact]
    public async Task Chat_CortesiaRevalidaIdentidadAntesDeResponder()
    {
        await using var escenario = new ChatbotEscenario(configurar: s => s.AddSingleton<IUsuarioActual>(new IdentidadCambiante()));
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("hola"));
        Assert.Equal(EstadoConsultaChat.NoAutorizado, respuesta.Estado);
        Assert.DoesNotContain("¡Hola!", respuesta.Texto);
        Assert.Equal(0, escenario.Modelo.CantidadSolicitudes);
    }

    [Theory]
    [InlineData("Hola, ¿cuánta energía generó el Parque norte?")]
    [InlineData("Gracias, ¿cuánta energía generó el Parque norte?")]
    [InlineData("Hola, ¿qué podemos hacer hoy con la generación del Parque norte?")]
    [InlineData("Hola, ¿qué podés hacer? Consultá la generación del Parque norte.")]
    public async Task Chat_CortesiaConPreguntaOperativaSiguePorMcp(string texto)
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Modelo.Llamadas.Enqueue(("ConsultarGeneracionDiariaAsync", new() { ["parque"] = escenario.Parque.Nombre }));
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new(texto));
        Assert.Equal(EstadoConsultaChat.Correcto, respuesta.Estado);
        Assert.Equal("ConsultarGeneracionDiariaAsync", Assert.Single(respuesta.Consultas).Herramienta);
        Assert.True(escenario.Modelo.CantidadSolicitudes > 0);
        Assert.Equal(0, escenario.Inversor.Escrituras);
    }

    [Fact]
    public async Task Chat_SecuenciaDeLaCapturaDistingueSaludoBienestarYAyuda()
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Modelo.Caido = true;
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IChatbotService>();
        var saludo = await servicio.EnviarPreguntaAsync(new("hola"));
        var bienestar = await servicio.EnviarPreguntaAsync(new("como estas?"));
        var ayuda = await servicio.EnviarPreguntaAsync(new("hola que podemos hacer hoy?"));
        Assert.All(new[] { saludo, bienestar, ayuda }, r => Assert.Equal(EstadoConsultaChat.Correcto, r.Estado));
        Assert.NotEqual(saludo.Texto, bienestar.Texto);
        Assert.Contains("Podemos revisar", ayuda.Texto);
        Assert.Equal(0, escenario.Modelo.CantidadSolicitudes);
        Assert.Equal(0, escenario.Inversor.Escrituras);
        Assert.Equal(0, escenario.Escrituras);
    }

    [Fact]
    public async Task Chat_PreguntaDeAyudaConDatosOperativosNoAceptaRespuestaSinEvidencia()
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Modelo.Responder = _ => "El Parque norte generó 9999 kWh.";
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>()
            .EnviarPreguntaAsync(new("Hola, ¿qué podemos hacer hoy con la generación del Parque norte?"));
        Assert.Equal(EstadoConsultaChat.SinDatos, respuesta.Estado);
        Assert.DoesNotContain("9999", respuesta.Texto);
        Assert.True(escenario.Modelo.CantidadSolicitudes > 0);
    }

    private sealed class IdentidadCambiante : IUsuarioActual
    {
        private int _consultas;
        public Task<UsuarioActualDto> ObtenerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UsuarioActualDto(true, ++_consultas == 1 ? "persona-uno" : "persona-dos", ["Administrador"]));
    }
}
