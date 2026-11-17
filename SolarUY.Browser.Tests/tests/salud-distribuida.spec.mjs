import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

const runtime = path.resolve('.runtime/salud-distribuida');
const salud = page => page.getByRole('region', { name: 'Salud y alertas de paneles' });
const filaA = page => salud(page).getByRole('table').first().getByRole('row')
  .filter({ has: page.getByRole('cell', { name: 'A', exact: true }) });

async function control(accion) {
  const id = randomUUID();
  await fs.writeFile(path.join(runtime, 'control.json'), JSON.stringify({ id, accion }));
  await expect.poll(async () => {
    try { return JSON.parse(await fs.readFile(path.join(runtime, 'resultado.json'), 'utf8')).id; }
    catch { return null; }
  }, { timeout: 30000 }).toBe(id);
}
async function abrir(page, instancia, datos) {
  const metricas = { errores: [], conexiones: 0, cerradas: 0, avisos: 0 };
  page.on('pageerror', e => metricas.errores.push(e.message));
  page.on('websocket', socket => {
    if (!socket.url().includes('/hubs/monitoreo')) return;
    metricas.conexiones++;
    socket.on('close', () => metricas.cerradas++);
    socket.on('framereceived', frame => { if (String(frame.payload).includes('SaludActualizada')) metricas.avisos++; });
  });
  await page.context().addCookies([{ name: '.AspNetCore.Cookies', value: datos.cookie, url: datos.proxy, httpOnly: true, sameSite: 'Lax' }]);
  await expect.poll(async () => {
    await page.context().addCookies([{ name: 'SolarUYInstancia', value: instancia, url: datos.proxy, httpOnly: true, sameSite: 'Lax' }]);
    const respuesta = await page.context().request.get(`${datos.proxy}/lib/signalr/8.0.17/signalr.min.js`);
    return respuesta.headers()['x-solaruy-instancia'];
  }).toBe(`req16-${instancia}`);
  const respuesta = await page.goto(`${datos.proxy}/monitoreo?parque=${datos.parque}`);
  expect(respuesta.headers()['x-solaruy-instancia']).toBe(`req16-${instancia}`);
  await expect(page.getByRole('heading', { name: 'Monitoreo en vivo', exact: true })).toBeVisible();
  await expect(filaA(page).getByRole('cell', { name: /^● Normal$/ })).toBeVisible();
  await page.evaluate(() => { window.__marcaSaludDistribuida = 'sin-recarga'; });
  return metricas;
}

test('salud y alertas se distribuyen a A/B y recuperan el circuito tras relevar A', async ({ page, browser }) => {
  let datos;
  await expect.poll(async () => {
    try { datos = JSON.parse(await fs.readFile(path.join(runtime, 'sesion.json'), 'utf8')); return true; }
    catch { return false; }
  }).toBe(true);
  const otroContexto = await browser.newContext();
  try {
    const paginaB = await otroContexto.newPage();
    const a = await abrir(page, 'a', datos); const b = await abrir(paginaB, 'b', datos);
    const inicio = Date.now(); await control('falla');
    for (const pagina of [page, paginaB]) {
      await expect(filaA(pagina).getByRole('cell', { name: /^● Falla$/ })).toBeVisible();
      await expect(salud(pagina).getByRole('heading', { name: 'Alertas activas (1)', exact: true })).toBeVisible();
      expect(await pagina.evaluate(() => window.__marcaSaludDistribuida)).toBe('sin-recarga');
    }
    console.log(`Falla confirmada visible en ambas instancias: ${Date.now() - inicio} ms; B solo observa.`);
    expect(a.avisos).toBeGreaterThan(0); expect(b.avisos).toBeGreaterThan(0);
    await filaA(paginaB).getByRole('button', { name: 'Ver cambios' }).click();
    await expect(salud(paginaB).getByRole('heading', { name: 'Historial de A', exact: true })).toBeVisible();
    await control('recuperar');
    for (const pagina of [page, paginaB]) {
      await expect(filaA(pagina).getByRole('cell', { name: /^● Normal$/ })).toBeVisible();
      await expect(salud(pagina).getByRole('heading', { name: 'Alertas activas (0)', exact: true })).toBeVisible();
    }
    const recarga = page.waitForEvent('framenavigated', { predicate: frame => frame === page.mainFrame(), timeout: 30000 });
    await control('relevar'); await recarga;
    await expect(page.getByRole('heading', { name: 'Monitoreo en vivo', exact: true })).toBeVisible();
    expect(new URL(page.url()).searchParams.get('parque')).toBe(datos.parque);
    await expect.poll(async () => (await page.context().request.get(`${datos.proxy}/lib/signalr/8.0.17/signalr.min.js`)).headers()['x-solaruy-instancia'])
      .toBe('req16-b');
    expect(b.cerradas).toBe(0);
    await control('restaurar'); await control('falla');
    for (const pagina of [page, paginaB]) await expect(filaA(pagina).getByRole('cell', { name: /^● Falla$/ })).toBeVisible();
    await control('recuperar');
    for (const pagina of [page, paginaB]) await expect(filaA(pagina).getByRole('cell', { name: /^● Normal$/ })).toBeVisible();
    await paginaB.screenshot({ path: test.info().outputPath('salud-dos-instancias-tras-relevo.png'), fullPage: true });
    expect(a.errores).toEqual([]); expect(b.errores).toEqual([]);
  } finally { await otroContexto.close(); }
});
