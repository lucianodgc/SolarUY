import { defineConfig } from '@playwright/test';
import path from 'node:path';

const runtime = path.resolve('.runtime');
process.env.SOLARUY_MONITOREO_BROWSER_RUNTIME = runtime;
process.env.SOLARUY_MONITOREO_BROWSER_PROXY = '1';
const proxy = process.env.SOLARUY_MONITOREO_PROXY_URL || 'http://127.0.0.1:18088';

export default defineConfig({
  testDir: './tests', workers: 1, retries: 0, timeout: 45000,
  expect: { timeout: 15000 },
  reporter: 'list',
  use: { browserName: 'chromium', headless: true, viewport: { width: 1440, height: 1000 },
    baseURL: proxy, screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  webServer: {
    command: 'dotnet test ../SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~ServidorTemporalParaPlaywright',
    url: `${proxy}/lib/signalr/8.0.17/signalr.min.js`, timeout: 60000,
    reuseExistingServer: false, stdout: 'pipe',
    env: { ...process.env, SOLARUY_MONITOREO_BROWSER_PROXY: '1', SOLARUY_MONITOREO_BROWSER_RUNTIME: runtime }
  },
  globalTeardown: './global-teardown.mjs'
});
