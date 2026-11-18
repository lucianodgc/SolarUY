export default async function finalizarPruebas() {
  try { await fetch('http://127.0.0.1:5069/pruebas/terminar', { method: 'POST' }); } catch { }
  for (let i = 0; i < 30; i++) {
    try { await fetch('http://127.0.0.1:5069/lib/signalr/8.0.17/signalr.min.js'); }
    catch { return; }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
}
