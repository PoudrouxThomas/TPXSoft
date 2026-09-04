import { defineConfig, devices } from '@playwright/test';

/**
 * e2e is deliberately outside the inner loop -- it is slow, and it is never in the
 * Stop hook. CI and on demand only. A handful of real user journeys is the right
 * number; a per-component e2e suite is slow, brittle, and duplicates the unit tests.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  reporter: process.env['CI'] ? 'dot' : [['list']],
  use: {
    baseURL: 'http://localhost:__PORT__',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm start',
    url: 'http://localhost:__PORT__',
    reuseExistingServer: !process.env['CI'],
    timeout: 120_000,
  },
});
