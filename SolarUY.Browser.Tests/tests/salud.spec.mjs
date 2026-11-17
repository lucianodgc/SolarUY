import { test, expect } from '@playwright/test';

test('salud confirma falla, cierra al recuperar y conserva historial sin recargar', async ({ page }) => {
  const errores = [];
  let conexiones = 0; let cerradas = 0;
  page.on('websocket', socket => {
    if (!socket.url().includes('/hubs/monitoreo')) return;
    conexiones++; socket.on('close', () => cerradas++);
  });
  await page.addInitScript(() => {
    const Original = window.WebSocket; window.__socketsSalud = [];
    window.WebSocket = class extends Original {
      constructor(...args) { super(...args); window.__socketsSalud.push(this); }
    };
  });
  page.on('pageerror', e => errores.push(e.message));
  const datos = await (await page.request.get('/pruebas/datos')).json();
  await page.request.post('/pruebas/ingresar/Consulta');
  await page.goto(`/monitoreo?parque=${datos.parque}`);
  const salud = page.getByRole('region', { name: 'Salud y alertas de paneles' });
  await expect(salud.getByRole('heading', { name: 'Salud de paneles y nodos' })).toBeVisible();
  await page.request.post('/pruebas/medir/false');
  const fila = salud.getByRole('row').filter({ has: page.getByRole('cell', { name: 'A', exact: true }) }).first();
  await expect(fila.getByRole('cell', { name: /^● Normal$/ })).toBeVisible();
  const marca = await page.evaluate(() => { window.__marcaSalud = 'sin-recarga'; return window.__marcaSalud; });
  await page.waitForTimeout(1100);
  const inicioFalla = Date.now();
  await page.request.post('/pruebas/medir/true');
  await expect(fila.getByRole('cell', { name: /^● Falla$/ })).toBeVisible();
  console.log(`Latencia de falla simulada a indicador visible: ${Date.now() - inicioFalla} ms; adquisición de prueba 1 s, evaluación 1 s.`);
  await expect(salud.getByRole('heading', { name: /Alertas activas \(1\)/ })).toBeVisible();
  await fila.getByRole('button', { name: 'Ver cambios' }).click();
  await expect(salud.getByRole('heading', { name: 'Historial de A', exact: true })).toBeVisible();
  await expect(salud.getByText('Sin estado previo', { exact: true })).toBeVisible();
  await page.waitForTimeout(1100);
  await page.request.post('/pruebas/medir/false');
  await expect(fila.getByRole('cell', { name: /^● Normal$/ })).toBeVisible();
  await expect(salud.getByRole('heading', { name: /Alertas activas \(0\)/ })).toBeVisible();
  expect(await page.evaluate(() => window.__marcaSalud)).toBe(marca);
  await page.screenshot({ path: test.info().outputPath('salud-recuperada.png'), fullPage: true });
  await expect(fila.getByRole('cell', { name: /^● Desconectado$/ })).toBeVisible();
  await page.getByTestId('selector-parque').click();
  await page.getByRole('option', { name: 'Otro parque', exact: true }).click();
  await expect(salud.getByRole('heading', { name: 'Historial de A', exact: true })).toHaveCount(0);
  await expect(salud.getByRole('table').first().getByRole('cell', { name: 'D', exact: true })).toBeVisible();
  const previas = conexiones;
  await page.evaluate(() => window.__socketsSalud.find(s => s.url.includes('/hubs/monitoreo') && s.readyState === 1)?.close());
  await expect.poll(() => conexiones).toBeGreaterThan(previas);
  await expect(page.getByText('Conexión: Conectado', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: /Perfil y permisos/ }).click();
  await expect.poll(() => cerradas).toBe(conexiones);
  const alSalir = conexiones;
  await page.waitForTimeout(2200);
  expect(conexiones).toBe(alSalir);
  expect(errores).toEqual([]);
});
