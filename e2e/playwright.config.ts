import { defineConfig, devices } from '@playwright/test';

// Specs use user-visible selectors only (getByRole/getByLabel/getByText), so they run
// unchanged against any frontend — only BASE_URL changes.
//
//   BASE_URL=http://localhost:5174 npx playwright test        # React, via docker-compose.test.yml (nginx)
//   BASE_URL=http://localhost:5173 npx playwright test        # React, via the Vite dev server
const baseURL = process.env.BASE_URL ?? 'http://localhost:5174';

const desktopViewport = { width: 1280, height: 900 };
const mobileViewport = { width: 390, height: 844 };

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  // Golden-path specs share seeded accounts and mutate real state (orders, staff, roles...) —
  // parallel workers would race each other against the same rows. Keep this at 1 until accounts
  // are split per-worker.
  workers: 1,
  retries: 0,
  // Blazor Server's SignalR circuit setup/teardown in this environment (Docker Desktop) has been
  // observed taking several seconds, occasionally 30s+ — well past Playwright's 30s default. Give
  // real per-step slowness room without masking genuine hangs entirely.
  timeout: 60_000,
  reporter: [['html', { open: 'never' }], ['list']],
  outputDir: './test-results',
  // {projectName} is required here — without it, all 4 visual projects (desktop/mobile x
  // light/dark) collide on the same file, and every project after the first "fails" comparing
  // its screenshot against a different project's differently-sized baseline image.
  snapshotPathTemplate: '{testDir}/../baseline/screenshots/{arg}-{projectName}{ext}',
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  expect: {
    toHaveScreenshot: { maxDiffPixelRatio: 0.02 },
  },
  projects: [
    // Golden-path / functional coverage — one role per file, run once at desktop size.
    {
      name: 'functional',
      testMatch: /golden-path\/.*\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], viewport: desktopViewport, colorScheme: 'light' },
    },
    // Visual regression baseline — every route, both themes, both breakpoints.
    {
      name: 'visual-desktop-light',
      testMatch: /visual\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], viewport: desktopViewport, colorScheme: 'light' },
    },
    {
      name: 'visual-desktop-dark',
      testMatch: /visual\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], viewport: desktopViewport, colorScheme: 'dark' },
    },
    {
      name: 'visual-mobile-light',
      testMatch: /visual\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], viewport: mobileViewport, colorScheme: 'light' },
    },
    {
      name: 'visual-mobile-dark',
      testMatch: /visual\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], viewport: mobileViewport, colorScheme: 'dark' },
    },
  ],
});
