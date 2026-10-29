import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
const ejecutar = promisify(execFile);

test.describe.configure({ mode: 'serial' });
const runtime = path.resolve('.runtime');
const metricas = new WeakMap();

async function sesion() {
  let dato;
  await expect.poll(async () => {
    try { dato = JSON.parse(await fs.readFile(path.join(runtime, 'sesion.json'), 'utf8')); return true; }
    catch { return false; }
  }).toBe(true);
  return dato;
}

async function abrir(page, instancia = 'a', parque) {
  const datos = await sesion();
  await page.context().addCookies([
    { name: '.AspNetCore.Cookies', value: datos.cookie, url: datos.proxy, httpOnly: true, sameSite: 'Lax' },
    { name: 'SolarUYInstancia', value: instancia, url: datos.proxy, httpOnly: true, sameSite: 'Lax' }
  ]);
  await page.addInitScript(() => {
    const Original = window.WebSocket;
    window.__socketsMonitoreoPrueba = [];
    window.WebSocket = class extends Original {
      constructor(...args) { super(...args); window.__socketsMonitoreoPrueba.push(this); }
    };
  });
  const estado = { conexiones: 0, avisos: 0, cerrados: 0, errores: [] };
  metricas.set(page, estado);
  page.on('pageerror', e => estado.errores.push(e.message));
  page.on('websocket', socket => {
    if (!socket.url().includes('/hubs/monitoreo')) return;
    estado.conexiones++;
    socket.on('close', () => estado.cerrados++);
    socket.on('framereceived', frame => {
      if (String(frame.payload).includes('LecturasConfirmadas')) estado.avisos++;
    });
  });
  // Esperar que venza cualquier penalización temporal de fail_timeout del arranque.
  await expect.poll(async () => {
    await page.context().addCookies([{ name: 'SolarUYInstancia', value: instancia, url: datos.proxy, httpOnly: true, sameSite: 'Lax' }]);
    const respuesta = await page.context().request.get(`${datos.proxy}/lib/signalr/8.0.17/signalr.min.js`);
    return respuesta.headers()['x-solaruy-instancia'];
  }).toBe(`req14-${instancia}`);
  const respuesta = await page.goto(`${datos.proxy}/monitoreo?parque=${parque || datos.parqueId}`);
  expect(respuesta.headers()['x-solaruy-instancia']).toBe(`req14-${instancia}`);
  await expect(page.getByRole('heading', { name: 'Monitoreo en vivo', exact: true })).toBeVisible();
  await expect(page.getByText('Conexión: Conectado', { exact: true })).toBeVisible();
  await expect(page.locator('section[aria-label="Gráfico de potencia en vivo"] svg').first()).toBeVisible();
  return datos;
}

const puntos = page => page.locator('section[aria-label="Gráfico de potencia en vivo"] details p');
async function ultimo(page) { return (await puntos(page).allTextContents()).at(-1); }

async function control(accion) {
  const id = randomUUID();
  await fs.writeFile(path.join(runtime, 'control.json'), JSON.stringify({ id, accion }));
  await expect.poll(async () => {
    try { return JSON.parse(await fs.readFile(path.join(runtime, 'resultado-control.json'), 'utf8')).id; }
    catch { return null; }
  }, { timeout: 30000 }).toBe(id);
}

test('ambas instancias actualizan el gráfico sin recargar y acotan sus puntos', async ({ page, browser }) => {
  await abrir(page, 'a');
  const otroContexto = await browser.newContext();
  try {
    const otraPagina = await otroContexto.newPage();
    await abrir(otraPagina, 'b');
    const inicialA = await ultimo(page); const inicialB = await ultimo(otraPagina);
    await expect.poll(() => ultimo(page)).not.toBe(inicialA);
    await expect.poll(() => ultimo(otraPagina)).not.toBe(inicialB);
    await expect.poll(() => puntos(page).count()).toBe(3);
    const primero = await puntos(page).first().textContent();
    await expect.poll(() => puntos(page).first().textContent()).not.toBe(primero);
    expect(await puntos(page).count()).toBe(3);
    expect(metricas.get(page).avisos).toBeGreaterThan(0);
    expect(metricas.get(otraPagina).avisos).toBeGreaterThan(0);
    expect(metricas.get(page).errores).toEqual([]);
    await page.screenshot({ path: test.info().outputPath('monitoreo-dos-instancias.png'), fullPage: true });
  } finally { await otroContexto.close(); }
});

test('cambios rápidos de parque conservan la última selección frente a avisos tardíos', async ({ page }) => {
  const datos = await abrir(page);
  for (const nombre of ['Otro parque', 'Parque histórico', 'Otro parque', 'Parque histórico']) {
    await page.getByTestId('selector-parque').click();
    await page.getByRole('option', { name: nombre, exact: true }).click();
  }
  await expect(page.getByRole('heading', { level: 2, name: 'Parque histórico', exact: true })).toBeVisible();
  await expect(page).toHaveURL(new RegExp(`parque=${datos.parqueId}`));
  const antes = await ultimo(page);
  await expect.poll(() => ultimo(page)).not.toBe(antes);
  await expect(page.getByRole('heading', { level: 2, name: 'Parque histórico', exact: true })).toBeVisible();
  expect(metricas.get(page).errores).toEqual([]);
});

test('avisos repetidos no duplican puntos y reconectar o salir libera la suscripción', async ({ page }) => {
  await abrir(page);
  try {
    await control('pausar');
    const antes = metricas.get(page).avisos;
    await control('repetir');
    await expect.poll(() => metricas.get(page).avisos).toBeGreaterThan(antes);
    const fechas = (await puntos(page).allTextContents()).map(p => p.split(' UTC')[0]);
    expect(new Set(fechas).size).toBe(fechas.length);
    const conexiones = metricas.get(page).conexiones;
    await page.evaluate(() => window.__socketsMonitoreoPrueba
      .filter(s => s.url.includes('/hubs/monitoreo') && s.readyState === WebSocket.OPEN).forEach(s => s.close()));
    await expect.poll(() => metricas.get(page).conexiones).toBeGreaterThan(conexiones);
    await expect(page.getByText('Conexión: Conectado', { exact: true })).toBeVisible();
  } finally { await control('reanudar'); }
  const cerrados = metricas.get(page).cerrados;
  await page.getByRole('link', { name: 'Perfil y permisos' }).click();
  await expect(page).toHaveURL(/mi-cuenta/);
  await expect.poll(() => metricas.get(page).cerrados).toBeGreaterThan(cerrados);
  const conexiones = metricas.get(page).conexiones;
  await page.waitForTimeout(2500); // Superar el reintento de 2 s para detectar una suscripción filtrada.
  expect(metricas.get(page).conexiones).toBe(conexiones);
  expect(metricas.get(page).errores).toEqual([]);
});

test('una interrupción de MariaDB vence las muestras y recupera las actualizaciones', async ({ page }) => {
  const contenedor = process.env.SOLARUY_MONITOREO_DB_CONTAINER;
  test.skip(!contenedor, 'Requiere el nombre del contenedor MariaDB aislado.');
  await abrir(page);
  const antes = await ultimo(page);
  await ejecutar('docker', ['pause', contenedor]);
  try {
    await expect(page.getByText('Fuera de vigencia', { exact: true }).first()).toBeVisible();
    await expect(page.locator('.metric-value')).toHaveText('Sin datos vigentes');
  } finally { await ejecutar('docker', ['unpause', contenedor]); }
  await expect.poll(() => ultimo(page)).not.toBe(antes);
  await expect(page.locator('.metric-value')).not.toHaveText('Sin datos vigentes');
  expect(metricas.get(page).errores).toEqual([]);
});

test('el navegador recupera circuito, cookie y parque al reemplazar la instancia', async ({ page, browser }) => {
  const datos = await abrir(page, 'a');
  const contextoB = await browser.newContext();
  try {
    const paginaB = await contextoB.newPage();
    await abrir(paginaB, 'b');
    const recargaA = page.waitForResponse(r => r.request().isNavigationRequest() && r.url().includes('/monitoreo') && r.status() === 200);
    const conexionesB = metricas.get(paginaB).conexiones;
    await control('relevar');
    expect((await recargaA).headers()['x-solaruy-instancia']).toBe('req14-b');
    expect(metricas.get(paginaB).conexiones).toBe(conexionesB);
    for (const pagina of [page, paginaB]) {
      await expect(pagina).toHaveURL(new RegExp(`parque=${datos.parqueId}`));
      await expect(pagina.getByText('Conexión: Conectado', { exact: true })).toBeVisible();
      await expect(pagina.getByRole('heading', { level: 2, name: 'Parque histórico', exact: true })).toBeVisible();
      const antes = await ultimo(pagina);
      await expect.poll(() => ultimo(pagina)).not.toBe(antes);
      expect(metricas.get(pagina).errores).toEqual([]);
    }
    await page.locator('section[aria-label="Gráfico de potencia en vivo"]').scrollIntoViewIfNeeded();
    await page.screenshot({ path: test.info().outputPath('monitoreo-tras-relevo.png'), fullPage: true });
  } finally { await contextoB.close(); }
});
