import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
const region = page => page.locator('main');
async function control(accion) {
  const id = randomUUID();
  await fs.writeFile('.runtime/ordenes/control.json', JSON.stringify({ id, accion }));
  let resultado;
  await expect.poll(async () => {
    try { resultado = JSON.parse(await fs.readFile('.runtime/ordenes/resultado.json', 'utf8')); return resultado.id; }
    catch { return null; }
  }).toBe(id);
  return resultado;
}
async function abrir(page, instancia, datos, cookie = datos.cookie) {
  await page.context().addCookies([
    { name: '.AspNetCore.Cookies', value: cookie, url: datos.proxy, httpOnly: true, sameSite: 'Lax' },
    { name: 'SolarUYInstancia', value: instancia, url: datos.proxy, httpOnly: true, sameSite: 'Lax' }
  ]);
  await expect.poll(async () => (await page.request.get(`${datos.proxy}/lib/signalr/8.0.17/signalr.min.js`)).headers()['x-solaruy-instancia']).toBe(`req17-${instancia}`);
  await expect.poll(async () => {
    const respuesta = await page.goto('/simulador');
    return respuesta.headers()['x-solaruy-instancia'];
  }).toBe(`req17-${instancia}`);
  await expect(page.getByRole('heading', { name: 'Simulador de inversor', exact: true })).toBeVisible();
  await page.locator('[name="ParqueSimulado"]').click();
  await page.getByRole('option', { name: 'Parque histórico', exact: true }).click();
}
async function confirmar(page, boton, destino) {
  await boton.click();
  const dialogo = page.getByRole('dialog');
  await expect(dialogo).toContainText(destino);
  await dialogo.getByRole('button', { name: 'Confirmar', exact: true }).click();
  await expect(region(page).getByRole('status').filter({ hasText: 'Intento' })).toContainText('Confirmada');
}
test('órdenes auditadas, observación A/B, roles, simulador y relevo detrás de Nginx', async ({ page, browser }) => {
  let datos;
  await expect.poll(async () => {
    try { datos = JSON.parse(await fs.readFile('.runtime/ordenes/sesion.json', 'utf8')); return true; } catch { return false; }
  }).toBe(true);
  const contextoB = await browser.newContext(); const b = await contextoB.newPage();
  const errores = []; page.on('pageerror', e => errores.push(e.message)); b.on('pageerror', e => errores.push(e.message));
  let conexiones = 0;
  page.on('websocket', socket => { if (socket.url().includes('/hubs/monitoreo')) conexiones++; });
  await page.addInitScript(() => {
    const Original = window.WebSocket; window.__socketsOrdenes = [];
    window.WebSocket = class extends Original {
      constructor(...args) { super(...args); window.__socketsOrdenes.push(this); }
    };
  });
  const fila = p => region(p).getByRole('row').filter({ hasText: datos.panel });
  try {
    await abrir(page, 'a', datos); await abrir(b, 'b', datos);
    await expect(fila(page).getByRole('cell', { name: 'Activo', exact: true })).toBeVisible();
    await fila(page).getByRole('button', { name: 'Desactivar', exact: true }).evaluate(el => { el.click(); el.click(); });
    await expect(page.getByRole('dialog')).toHaveCount(1);
    await expect(page.getByRole('dialog')).toContainText(datos.panel);
    await page.getByRole('dialog').getByRole('button', { name: 'Cancelar', exact: true }).click();
    expect((await control('auditoria')).cantidad).toBe(0);
    await confirmar(page, fila(page).getByRole('button', { name: 'Desactivar', exact: true }), datos.panel);
    for (const p of [page, b]) await expect(fila(p).getByRole('cell', { name: 'Inactivo', exact: true })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Monitoreo de generación' }).getByRole('row')
      .filter({ has: page.getByRole('cell', { name: 'A', exact: true }) }).first()).toContainText('0,00 W');
    await confirmar(b, fila(b).getByRole('button', { name: 'Activar', exact: true }), datos.panel);
    await expect(fila(page).getByRole('cell', { name: 'Activo', exact: true })).toBeVisible();
    await confirmar(page, region(page).getByRole('button', { name: 'Activar volcado a red', exact: true }), 'alcance global');
    await expect(region(b).locator('.metric-card').filter({ hasText: 'Volcado a la red' }).getByText('Activo', { exact: true })).toBeVisible();
    await confirmar(b, region(b).getByRole('button', { name: 'Desactivar volcado a red', exact: true }), 'alcance global');
    await expect(region(page).locator('.metric-card').filter({ hasText: 'Volcado a la red' }).getByText('Inactivo', { exact: true })).toBeVisible();
    const auditoria = await control('auditoria'); expect(auditoria.cantidad).toBe(4); expect(auditoria.confirmadas).toBe(4);
    expect(auditoria.actores).toEqual(['prueba-nginx']);
    await page.locator('[name="ParqueSimulado"]').click();
    await page.getByRole('option', { name: 'Otro parque', exact: true }).click();
    await expect(region(page).getByRole('status').filter({ hasText: 'Intento' })).toHaveCount(0);
    await expect(region(page).getByRole('cell').filter({ hasText: 'D' }).first()).toBeVisible();
    await page.locator('[name="ParqueSimulado"]').click();
    await page.getByRole('option', { name: 'Parque histórico', exact: true }).click();
    const anteriores = conexiones;
    await page.evaluate(() => window.__socketsOrdenes.filter(s => s.url.includes('/hubs/monitoreo')).forEach(s => s.close()));
    await expect.poll(() => conexiones).toBeGreaterThan(anteriores);
    await expect(fila(page).getByRole('cell', { name: 'Activo', exact: true })).toBeVisible();
    for (const cookie of [datos.consulta, datos.tecnico]) {
      const lectura = await browser.newContext(); const p = await lectura.newPage();
      await abrir(p, 'b', datos, cookie);
      await expect(region(p).getByRole('button', { name: /^(Activar|Desactivar|Apagar|Prender)/ })).toHaveCount(0); await lectura.close();
    }
    await page.goto('/simulador');
    await expect(page.getByRole('heading', { name: 'Simulador de inversor', exact: true })).toBeVisible();
    const activarVolcado = page.getByRole('button', { name: 'Activar volcado a red', exact: true });
    await activarVolcado.click(); await expect(page.getByRole('dialog')).toContainText('alcance global');
    await page.getByRole('dialog').getByRole('button', { name: 'Cancelar', exact: true }).click();
    expect((await control('auditoria')).cantidad).toBe(4);
    await activarVolcado.click(); await page.getByRole('dialog').getByRole('button', { name: 'Confirmar', exact: true }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Intento' })).toContainText('Confirmada');
    await expect(region(b).locator('.metric-card').filter({ hasText: 'Volcado a la red' }).getByText('Activo', { exact: true })).toBeVisible();
    await page.goto('/simulador');
    await expect(region(page).getByRole('button', { name: 'Desactivar volcado a red', exact: true })).toBeEnabled();
    const recarga = page.waitForEvent('framenavigated', { predicate: frame => frame === page.mainFrame(), timeout: 30000 });
    await control('relevar');
    await recarga;
    await expect(page.getByRole('heading', { name: 'Simulador de inversor', exact: true })).toBeVisible();
    await confirmar(page, region(page).getByRole('button', { name: 'Desactivar volcado a red', exact: true }), 'alcance global');
    await control('restaurar');
    await abrir(page, 'a', datos); await abrir(b, 'b', datos);
    await region(page).getByRole('button', { name: 'Activar volcado a red', exact: true }).click();
    await region(b).getByRole('button', { name: 'Activar volcado a red', exact: true }).click();
    await Promise.all([
      page.getByRole('dialog').getByRole('button', { name: 'Confirmar', exact: true }).click(),
      b.getByRole('dialog').getByRole('button', { name: 'Confirmar', exact: true }).click()
    ]);
    await expect.poll(async () => (await control('auditoria')).cantidad).toBe(8);
    await page.screenshot({ path: test.info().outputPath('ordenes-auditadas.png'), fullPage: true });
    expect((await control('auditoria')).confirmadas).toBeGreaterThanOrEqual(7);
    expect(errores).toEqual([]);
  } finally { await contextoB.close(); }
});
