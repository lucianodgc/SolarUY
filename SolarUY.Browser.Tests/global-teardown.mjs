import fs from 'node:fs/promises';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

export default async function () {
  const runtime = path.resolve('.runtime');
  await fs.mkdir(runtime, { recursive: true });
  await fs.writeFile(path.join(runtime, 'control.json'), JSON.stringify({ id: randomUUID(), accion: 'terminar' }));
  for (let i = 0; i < 300; i++) {
    try { await fs.access(path.join(runtime, 'finalizado.json')); return; } catch { }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
}
