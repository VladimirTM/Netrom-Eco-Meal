import { defineConfig, devices } from '@playwright/test';

// The whole point of Phase 0: these test files must run unchanged against the Blazor app today
// and against the React app from Phase 5 onward. Only BASE_URL and the two project lists below
// are expected to change between phases.
//
//   BASE_URL=http://localhost:8081 npx playwright test        # Blazor, via docker-compose.test.yml
//   BASE_URL=http://localhost:5173 npx playwright test        # React, from Phase 4 onward
const baseURL = process.env.BASE_URL ?? 'http://localhost:8081';

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
