export async function crear(destino) {
    let parque = null;
    let cerrado = false;
    let reintento = null;
    let iniciando = false;
    const conexion = new signalR.HubConnectionBuilder().withUrl('/hubs/monitoreo')
        .withAutomaticReconnect([0, 2000, 10000, 30000]).configureLogging(signalR.LogLevel.Warning).build();
    const avisar = async (metodo, ...args) => {
        if (cerrado || window.solarUyCircuitoConectado === false) return;
        // Durante el relevo, el circuito Blazor puede caer antes que el hub propio.
        // invokeMethodAsync también puede lanzar antes de devolver una promesa.
        try { await destino.invokeMethodAsync(metodo, ...args); } catch { }
    };
    const estado = async texto => {
        if (cerrado) return;
        const indicador = document.querySelector('[data-monitoreo-conexion]');
        if (indicador) indicador.textContent = texto;
        // Las transiciones de transporte no pueden usar un circuito que está cayendo.
        if (texto === 'Conectado' || texto === 'Sesión no autorizada') await avisar('EstadoConexion', texto);
    };
    const suscribir = async () => { if (parque) await conexion.invoke('SuscribirParque', parque); };
    conexion.on('LecturasConfirmadas', id => {
        if (id.toLowerCase() === parque?.toLowerCase()) void avisar('NuevaLectura', id);
    });
    conexion.onreconnecting(() => estado('Reconectando'));
    conexion.onreconnected(async () => {
        try { await suscribir(); await estado('Conectado'); if (parque) await avisar('NuevaLectura', parque); }
        catch { await estado('Sin autorización o parque no disponible'); }
    });
    const iniciar = async () => {
        if (cerrado || iniciando) return;
        iniciando = true;
        try {
            await conexion.start();
            if (cerrado) { await conexion.stop(); return; }
            try { await suscribir(); }
            catch { await estado('Sin autorización o parque no disponible'); return; }
            await estado('Conectado');
            if (parque) await avisar('NuevaLectura', parque);
        } catch (error) {
            if (cerrado) return;
            const noAutorizado = error.statusCode === 401 || error.statusCode === 403;
            await estado(noAutorizado ? 'Sesión no autorizada' : 'Desconectado');
            if (!noAutorizado) { clearTimeout(reintento); reintento = setTimeout(iniciar, 2000); }
        }
        finally { iniciando = false; }
    };
    conexion.onclose(() => {
        if (!cerrado) { void estado('Desconectado'); clearTimeout(reintento); reintento = setTimeout(iniciar, 2000); }
    });
    const circuitoAbajo = () => { void estado('Reconectando'); };
    const circuitoArriba = async () => {
        if (cerrado || conexion.state !== signalR.HubConnectionState.Connected) return;
        try { await suscribir(); await estado('Conectado'); if (parque) await avisar('NuevaLectura', parque); }
        catch { await estado('Sin autorización o parque no disponible'); }
    };
    window.addEventListener('solaruy:circuito-abajo', circuitoAbajo);
    window.addEventListener('solaruy:circuito-arriba', circuitoArriba);
    await iniciar();
    return {
        async seleccionar(id) {
            parque = id;
            if (conexion.state === signalR.HubConnectionState.Connected) { await suscribir(); await estado('Conectado'); }
        },
        async dispose() {
            cerrado = true; parque = null; clearTimeout(reintento);
            window.removeEventListener('solaruy:circuito-abajo', circuitoAbajo);
            window.removeEventListener('solaruy:circuito-arriba', circuitoArriba);
            conexion.off('LecturasConfirmadas');
            await conexion.stop();
        }
    };
}
