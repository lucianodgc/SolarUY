using SolarUY.Application.Abstractions;
using SolarUY.Application.Exceptions;
using SolarUY.Domain.Entities;

namespace SolarUY.Application.Inversores;

public sealed class OrdenInversorService(
    IInversorClient inversor, IInversorInventarioRepository inventario, IUsuarioActual usuarioActual,
    IIntentoOrdenInversorRepository repository, TimeProvider reloj, OrdenInversorOptions opciones) : IOrdenInversorService
{
    public async Task<ResultadoOrdenInversorDto> EjecutarAsync(SolicitudOrdenInversorDto solicitud, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
        var admin = usuario.EstaAutenticado && usuario.Roles.Contains("Administrador");
        if (solicitud.Correlacion == Guid.Empty || !Enum.IsDefined(solicitud.Accion)
            || solicitud.NodoId?.Length > 128 || solicitud.PanelId?.Length > 128 || usuario.Sub?.Length > 255)
            return new(null, EstadoOrdenInversor.Rechazada, "solicitud-invalida", false);
        IntentoOrdenInversor intento = new(solicitud.Correlacion, new(usuario.Sub, usuario.EstaAutenticado, admin),
            solicitud.Accion, solicitud.NodoId, solicitud.PanelId, Ahora());
        IAsyncDisposable? bloqueo;
        try { bloqueo = await repository.IntentarBloqueoAsync(cancellationToken); }
        catch { return new(null, EstadoOrdenInversor.Fallida, "auditoria-no-disponible", false); }
        await using var lease = bloqueo;
        try
        {
            var anterior = await ConsultarOrdenAnteriorAsync(solicitud, usuario, admin, bloqueo is not null, cancellationToken);
            if (anterior is not null) return anterior;
            await ValidarOrdenAsync(intento, usuario.Sub, bloqueo is not null, cancellationToken);
            await repository.GuardarAsync(intento, cancellationToken);
        }
        catch { return new(null, EstadoOrdenInversor.Fallida, "auditoria-inicial-fallida", false); }
        if (intento.Estado != EstadoOrdenInversor.Pendiente) return Resultado(intento, true);

        await EnviarOrdenAsync(solicitud, intento, cancellationToken);
        return await GuardarResultadoAsync(intento);
    }

    private async Task<ResultadoOrdenInversorDto?> ConsultarOrdenAnteriorAsync(SolicitudOrdenInversorDto solicitud,
        UsuarioActualDto usuario, bool admin, bool tieneBloqueo, CancellationToken cancellationToken)
    {
        if (usuario.Sub is not { } actor) return null;
        var anterior = await repository.BuscarAsync(actor, solicitud.Correlacion, cancellationToken);
        if (anterior is null) return null;
        if (!admin) return new(anterior.Id, EstadoOrdenInversor.Rechazada, "sin-permiso", true);
        if (anterior.Accion != solicitud.Accion || anterior.NodoSolicitado != solicitud.NodoId || anterior.PanelSolicitado != solicitud.PanelId)
            return new(anterior.Id, EstadoOrdenInversor.Rechazada, "correlacion-incompatible", true);
        if (tieneBloqueo && anterior.Estado == EstadoOrdenInversor.Pendiente)
        {
            await repository.RecuperarPendientesAsync(Ahora(), cancellationToken);
            anterior = await repository.BuscarAsync(actor, solicitud.Correlacion, cancellationToken)
                ?? throw new InvalidOperationException("No se encontró el intento de orden recuperado.");
        }
        return Resultado(anterior, true);
    }

    private async Task ValidarOrdenAsync(IntentoOrdenInversor intento, string? actor, bool tieneBloqueo,
        CancellationToken cancellationToken)
    {
        if (!intento.Administrador || string.IsNullOrWhiteSpace(actor))
            intento.Finalizar(EstadoOrdenInversor.Rechazada, "sin-permiso", Ahora());
        else if (!tieneBloqueo)
            intento.Finalizar(EstadoOrdenInversor.Rechazada, "inversor-ocupado", Ahora());
        else
        {
            await repository.RecuperarPendientesAsync(Ahora(), cancellationToken);
            if (intento.Accion is AccionOrdenInversor.ActivarPanel or AccionOrdenInversor.DesactivarPanel)
                await ValidarDestinoAsync(intento, cancellationToken);
            else if (intento.NodoSolicitado is not null || intento.PanelSolicitado is not null)
                intento.Finalizar(EstadoOrdenInversor.Rechazada, "volcado-es-global", Ahora());
        }
    }

    private async Task ValidarDestinoAsync(IntentoOrdenInversor intento, CancellationToken cancellationToken)
    {
        var nodos = await inventario.ListarAsync(cancellationToken);
        var nodo = nodos.SingleOrDefault(n => n.Id == intento.NodoSolicitado);
        var panel = nodo?.Paneles.SingleOrDefault(p => p.Id == intento.PanelSolicitado);
        if (nodo is null || panel is null || !Guid.TryParse(nodo.Id, out var nodoId) || !Guid.TryParse(panel.Id, out var panelId))
        {
            intento.Finalizar(EstadoOrdenInversor.Rechazada, "destino-no-vigente", Ahora());
            return;
        }
        var nombre = $"{nodo.Parque} / {nodo.Nombre} / {panel.NumeroSerie}";
        intento.ValidarDestino(nodoId, panelId, nombre[..Math.Min(300, nombre.Length)]);
    }

    private async Task EnviarOrdenAsync(SolicitudOrdenInversorDto solicitud, IntentoOrdenInversor intento,
        CancellationToken cancellationToken)
    {
        var envioPosible = false;
        try
        {
            // Sincronizar conserva las banderas operacionales del mismo destino.
            var nodos = await inventario.ListarAsync(cancellationToken);
            if (intento.PanelValidado.HasValue && !nodos.Any(n => n.Id == solicitud.NodoId && n.Paneles.Any(p => p.Id == solicitud.PanelId)))
                intento.Finalizar(EstadoOrdenInversor.Rechazada, "destino-no-vigente", Ahora());
            else
            {
                await inversor.SincronizarInventarioAsync(nodos, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                envioPosible = true;
                switch (solicitud.Accion)
                {
                    case AccionOrdenInversor.ActivarPanel: await inversor.ActivarPanelAsync(solicitud.NodoId!, solicitud.PanelId!, cancellationToken); break;
                    case AccionOrdenInversor.DesactivarPanel: await inversor.DesactivarPanelAsync(solicitud.NodoId!, solicitud.PanelId!, cancellationToken); break;
                    case AccionOrdenInversor.ActivarVolcadoRed: await inversor.ActivarVolcadoRedAsync(cancellationToken); break;
                    case AccionOrdenInversor.DesactivarVolcadoRed: await inversor.DesactivarVolcadoRedAsync(cancellationToken); break;
                }
                intento.Finalizar(EstadoOrdenInversor.Confirmada, "ack", Ahora(), true);
            }
        }
        catch (Exception exception) when (envioPosible && exception is OrdenInversorRechazadaException or KeyNotFoundException or ArgumentException)
        { intento.Finalizar(EstadoOrdenInversor.Rechazada, "rechazo-inversor", Ahora()); }
        catch
        { intento.Finalizar(envioPosible ? EstadoOrdenInversor.Indeterminada : EstadoOrdenInversor.Fallida,
            envioPosible ? "sin-confirmacion" : "sin-envio", Ahora()); }
    }

    private async Task<ResultadoOrdenInversorDto> GuardarResultadoAsync(IntentoOrdenInversor intento)
    {
        using var cierre = new CancellationTokenSource(TimeSpan.FromSeconds(opciones.CierreAuditoriaSegundos));
        for (var numero = 0; numero < opciones.IntentosPersistencia && !cierre.IsCancellationRequested; numero++)
        {
            try { await repository.GuardarAsync(intento, cierre.Token); return Resultado(intento, true); }
            catch { /* Solo se reintenta persistencia; jamás el envío TCP. */ }
        }
        return Resultado(intento, false);
    }

    private DateTime Ahora() => reloj.GetUtcNow().UtcDateTime;
    private static ResultadoOrdenInversorDto Resultado(IntentoOrdenInversor intento, bool completa)
        => new(intento.Id, intento.Estado, intento.Codigo, completa);
}
