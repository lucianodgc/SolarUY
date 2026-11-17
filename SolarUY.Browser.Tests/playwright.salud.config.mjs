import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests', testMatch: 'salud.spec.mjs', workers: 1, retries: 0, timeout: 60000,
  expect: { timeout: 12000 }, reporter: 'list',
  use: { browserName: 'chromium', headless: true, baseURL: 'http://127.0.0.1:5069',
    viewport: { width: 1440, height: 1000 }, screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  webServer: {
    command: 'dotnet test ../SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~ServidorTemporalSaludParaPlaywright',
    url: 'http://127.0.0.1:5069/lib/signalr/8.0.17/signalr.min.js', timeout: 60000,
    reuseExistingServer: false, stdout: 'pipe',
    env: { ...process.env, SOLARUY_SALUD_BROWSER: '1' }
  },
  globalTeardown: './salud-teardown.mjs'
});
