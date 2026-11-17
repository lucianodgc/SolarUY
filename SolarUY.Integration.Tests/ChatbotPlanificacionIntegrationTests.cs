using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SolarUY.Application.Chatbot;

namespace SolarUY.Integration.Tests;

public sealed class ChatbotPlanificacionIntegrationTests
{
    [Fact]
    public async Task Chat_PlanRetomaListadoYCompletaApartadosPorCadaParqueMedianteMcp()
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Modelo.Planificar = mensajes => mensajes.Last(m => m.Role.Value == "user").Text.Contains("agregale")
            ? "{\"accion\":\"detallar_parques\",\"parques\":[]}" : "{\"accion\":\"listar_parques\",\"parques\":[]}";
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatbotService>();
        Assert.Equal(EstadoConsultaChat.Correcto, (await chat.EnviarPreguntaAsync(new("¿Qué parques hay?"))).Estado);
        var detalle = await chat.EnviarPreguntaAsync(new("agregale mas informacion"));
        Assert.Equal(EstadoConsultaChat.Correcto, detalle.Estado);
        Assert.Contains("Cantidad de parques autorizados: 2.", detalle.Texto);
        Assert.Equal(9, detalle.Consultas.Count);
        foreach (var herramienta in new[] { "ConsultarEstadoActualAsync", "ConsultarGeneracionDiariaAsync",
                     "ConsultarPanelesConFallaAsync", "ConsultarMantenimientosPendientesAsync" })
            Assert.Equal(2, detalle.Consultas.Count(r => r.Herramienta == herramienta));
        Assert.Contains(escenario.Modelo.MensajesRecibidos, m => m.Text == "¿Qué parques hay?");
        Assert.Equal(0, escenario.Inversor.Escrituras);
        Assert.Equal(0, escenario.Escrituras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chat_PlanLimitaDetalleAlParqueSeleccionadoPorNombreOId(bool porId)
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Modelo.Planificar = _ => JsonSerializer.Serialize(new { accion = "detallar_parques",
            parques = new[] { porId ? escenario.Parque.Id.ToString() : escenario.Parque.Nombre } });
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("Detallá el Parque norte"));
        Assert.Equal(EstadoConsultaChat.Correcto, respuesta.Estado);
        Assert.Equal(5, respuesta.Consultas.Count);
        var estado = Assert.Single(respuesta.Consultas, r => r.Herramienta == "ConsultarEstadoActualAsync");
        Assert.Equal(escenario.Parque.Id, ((JsonElement)estado.Datos!).GetProperty("parqueId").GetGuid());
        Assert.Equal(0, escenario.Inversor.Escrituras);
    }

    [Fact]
    public async Task Chat_PlanDeEquiposNoConsultaFuentesNoSolicitadas()
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Historico.Falla = true;
        escenario.FallaMantenimiento = true;
        escenario.Modelo.Planificar = _ => "{\"accion\":\"estado_parques\",\"parques\":[]}";
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("¿Cuántos nodos hay en total?"));
        Assert.Equal(3, respuesta.Consultas.Count);
        Assert.All(respuesta.Consultas, r => Assert.True(r.Herramienta is "ListarParquesAsync" or "ConsultarEstadoActualAsync"));
        Assert.Null(escenario.Historico.UltimaConsulta);
        Assert.Equal(0, escenario.Inversor.Escrituras);
    }

    [Theory]
    [InlineData("Consulta")]
    [InlineData("Tecnico")]
    public async Task Chat_PlanDetalladoConservaPermisosDeMantenimiento(string rol)
    {
        await using var escenario = new ChatbotEscenario(rol);
        escenario.Modelo.Planificar = _ => JsonSerializer.Serialize(new { accion = "detallar_parques", parques = new[] { escenario.Parque.Nombre } });
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("Detallá el Parque norte"));
        var mantenimiento = Assert.Single(respuesta.Consultas, r => r.Herramienta == "ConsultarMantenimientosPendientesAsync");
        if (rol == "Consulta")
        { Assert.Equal(EstadoConsultaChat.NoAutorizado, mantenimiento.Estado); Assert.Null(mantenimiento.Datos); }
        else
        { Assert.Equal(1, ((JsonElement)mantenimiento.Datos!).GetArrayLength()); Assert.DoesNotContain("Tarea ajena", respuesta.Texto); }
        Assert.Equal(0, escenario.Escrituras);
    }

    [Fact]
    public async Task Chat_AclaracionConservaLaPreguntaParaResolverElSiguienteTurno()
    {
        await using var escenario = new ChatbotEscenario();
        var planificaciones = 0;
        escenario.Modelo.Planificar = mensajes =>
        {
            if (++planificaciones == 1) return "{\"accion\":\"aclarar\",\"parques\":[]}";
            Assert.Contains(mensajes, m => m.Text == "Ampliá ese parque");
            Assert.Contains(mensajes, m => m.Text.Contains("¿Qué parque y qué información"));
            return JsonSerializer.Serialize(new { accion = "detallar_parques", parques = new[] { escenario.Parque.Nombre } });
        };
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var chat = scope.ServiceProvider.GetRequiredService<IChatbotService>();
        var pregunta = await chat.EnviarPreguntaAsync(new("Ampliá ese parque"));
        Assert.Equal(EstadoConsultaChat.RequiereAclaracion, pregunta.Estado);
        Assert.Empty(pregunta.Consultas);
        Assert.Equal(EstadoConsultaChat.Correcto, (await chat.EnviarPreguntaAsync(new(escenario.Parque.Nombre))).Estado);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chat_PlanNoInventaParqueInexistenteNiResuelveNombreAmbiguo(bool ambiguo)
    {
        await using var escenario = new ChatbotEscenario();
        if (ambiguo) escenario.OtroParque.ActualizarDatos(escenario.Parque.Nombre, "Otra ubicación", null);
        escenario.Modelo.Planificar = _ => JsonSerializer.Serialize(new { accion = "detallar_parques",
            parques = new[] { ambiguo ? escenario.Parque.Nombre : "No existe" } });
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("Ampliá la información"));
        Assert.Equal(EstadoConsultaChat.RequiereAclaracion, respuesta.Estado);
        Assert.Equal("ListarParquesAsync", Assert.Single(respuesta.Consultas).Herramienta);
        Assert.Equal(0, escenario.Inversor.Escrituras);
    }

    [Theory]
    [InlineData("{\"accion\":\"borrar_parques\",\"parques\":[]}")]
    [InlineData("{\"accion\":\"detallar_parques\",\"parques\":[],\"usuarioId\":\"admin\"}")]
    [InlineData("{\"accion\":\"detallar_parques\",\"parques\":[{\"roles\":[\"Administrador\"]}]}")]
    [InlineData("no es JSON")]
    public async Task Chat_PlanInvalidoNoEjecutaConsultasNiExponeContenido(string plan)
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Modelo.Planificar = _ => plan;
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("Detallá los parques"));
        Assert.Equal(EstadoConsultaChat.NoDisponible, respuesta.Estado);
        Assert.Empty(respuesta.Consultas);
        Assert.DoesNotContain(plan, respuesta.Texto);
        Assert.Equal(0, escenario.Inversor.Escrituras);
    }

    [Fact]
    public async Task Chat_ReintentoSinConsultasEsAcotadoYNoAceptaCifrasInventadas()
    {
        await using var escenario = new ChatbotEscenario();
        escenario.Modelo.Responder = _ => "Generó 9999 kWh.";
        await using var scope = escenario.Servicios.CreateAsyncScope();
        var respuesta = await scope.ServiceProvider.GetRequiredService<IChatbotService>().EnviarPreguntaAsync(new("¿Cuánto generó el Parque norte?"));
        Assert.Equal(EstadoConsultaChat.SinDatos, respuesta.Estado);
        Assert.Equal(3, escenario.Modelo.CantidadSolicitudes); // Plan y dos intentos, sin más llamadas.
        Assert.DoesNotContain("9999", respuesta.Texto);
        Assert.Empty(respuesta.Consultas);
    }
}
