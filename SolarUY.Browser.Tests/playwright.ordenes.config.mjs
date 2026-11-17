import { defineConfig } from '@playwright/test';
const proxy = process.env.SOLARUY_MONITOREO_PROXY_URL || 'http://127.0.0.1:18088';
export default defineConfig({
  testDir: './tests', testMatch: 'ordenes.spec.mjs', workers: 1, retries: 0, timeout: 120000,
  expect: { timeout: 20000 }, reporter: 'list',
  use: { browserName: 'chromium', headless: true, baseURL: proxy, viewport: { width: 1440, height: 1000 },
    screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  webServer: {
    command: 'dotnet test ../SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~ServidorTemporalOrdenesParaPlaywright',
    url: `${proxy}/lib/signalr/8.0.17/signalr.min.js`, timeout: 60000, reuseExistingServer: false, stdout: 'pipe',
    env: { ...process.env, SOLARUY_ORDENES_BROWSER: '1' }
  }, globalTeardown: './ordenes-teardown.mjs'
});
