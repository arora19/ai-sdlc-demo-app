import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'http://localhost:5001',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command:
      'dotnet run --no-launch-profile --project ../backend/src/InvoiceApp.Api -- --urls http://localhost:5001',
    url: 'http://localhost:5001',
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
