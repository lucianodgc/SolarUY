export async function descargarCsv(nombre, tipoContenido, contenido) {
    const url = URL.createObjectURL(new Blob([contenido], { type: tipoContenido }));
    const enlace = document.createElement('a');
    try {
        enlace.href = url;
        enlace.download = nombre;
        document.body.appendChild(enlace);
        enlace.click();
        // Permite al navegador iniciar la descarga antes de liberar la URL.
        await new Promise(resolve => setTimeout(resolve, 1000));
    } finally {
        enlace.remove();
        URL.revokeObjectURL(url);
    }
}
