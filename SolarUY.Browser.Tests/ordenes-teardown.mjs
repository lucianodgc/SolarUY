import fs from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
export default async function finalizarPruebas() {
  await fs.writeFile('.runtime/ordenes/control.json', JSON.stringify({ id: randomUUID(), accion: 'terminar' }));
  const fin = Date.now() + 20000;
  while (Date.now() < fin) {
    try { await fs.access('.runtime/ordenes/finalizado.json'); return; } catch { await new Promise(r => setTimeout(r, 100)); }
  }
}
