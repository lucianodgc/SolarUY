(() => {
    let recuperando = false;
    let cancelar = false;
    window.solarUyCircuitoConectado = false;
    const aviso = document.getElementById('estado-reconexion');
    const fallo = () => {
        if (location.pathname.startsWith('/monitoreo')) location.reload();
        else if (aviso) aviso.textContent = 'No se pudo recuperar la conexión. Recargá la página para continuar.';
    };
    Blazor.start({ circuit: { reconnectionHandler: {
        onConnectionDown: () => {
            window.solarUyCircuitoConectado = false;
            window.dispatchEvent(new Event('solaruy:circuito-abajo'));
            if (recuperando) return;
            recuperando = true; cancelar = false;
            if (aviso) { aviso.hidden = false; aviso.textContent = 'Se perdió la conexión. Intentando recuperar…'; }
            void (async () => {
                for (let intento = 0; intento < 3 && !cancelar; intento++) {
                    await new Promise(resolve => setTimeout(resolve, 2000));
                    if (cancelar) return;
                    try { if (await Blazor.reconnect()) return; fallo(); return; } catch { }
                }
                if (!cancelar) fallo();
            })();
        },
        onConnectionUp: () => {
            cancelar = true; recuperando = false; if (aviso) aviso.hidden = true;
            window.solarUyCircuitoConectado = true;
            window.dispatchEvent(new Event('solaruy:circuito-arriba'));
        }
    } } }).then(() => {
        window.solarUyCircuitoConectado = true;
        window.dispatchEvent(new Event('solaruy:circuito-arriba'));
    });
})();
