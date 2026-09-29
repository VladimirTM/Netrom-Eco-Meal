import { test, expect } from '../fixtures/test';
import type { Page } from '@playwright/test';
import { accounts, seededBusinesses } from '../fixtures/accounts';
import { login } from '../fixtures/auth';

// Full-page screenshot baseline. Runs once per project in playwright.config.ts
// (desktop/mobile x light/dark = 4 projects), so each call below produces 4 snapshot files.
// Routes that need an ID minted at runtime (an order, a pickup pass, a rescue circle invite) are
// deliberately excluded — those flows are covered by the golden-path specs' own assertions
// instead of a static screenshot.

const brandId = '77777777-0000-0000-0000-000000000001'; // Golden Boot Bakeries

async function screenshotRoute(page: Page, route: string, name: string) {
  await page.goto(route);
  // Home/BusinessDetail/Impact fetch their data client-side (InteractiveServer) — wait for the
  // loading spinner to clear before snapshotting, same signal a real user waits on.
  await page.locator('.spinner-border').first().waitFor({ state: 'detached', timeout: 15_000 }).catch(() => {});
  await expect(page).toHaveScreenshot(`${name}.png`, { fullPage: true });
}

test.describe('Anonymous routes', () => {
  test('home', async ({ page }) => screenshotRoute(page, '/', 'anon-home'));
  test('login', async ({ page }) => screenshotRoute(page, '/account/login', 'anon-login'));
  test('register', async ({ page }) => screenshotRoute(page, '/account/register', 'anon-register'));
  test('forgot password', async ({ page }) => screenshotRoute(page, '/account/forgot-password', 'anon-forgot-password'));
  test('resend confirmation', async ({ page }) => screenshotRoute(page, '/account/resend-confirmation', 'anon-resend-confirmation'));
  test('brands', async ({ page }) => screenshotRoute(page, '/brands', 'anon-brands'));
  test('brand detail', async ({ page }) => screenshotRoute(page, `/brands/${brandId}`, 'anon-brand-detail'));
  test('impact', async ({ page }) => screenshotRoute(page, '/impact', 'anon-impact'));
  test('business detail', async ({ page }) => screenshotRoute(page, `/businesses/${seededBusinesses.stadionulDeGusturi}`, 'anon-business-detail'));
  test('access denied', async ({ page }) => screenshotRoute(page, '/account/access-denied', 'anon-access-denied'));
  test('not found', async ({ page }) => screenshotRoute(page, '/not-found', 'anon-not-found'));
});

test.describe('Customer routes', () => {
  test.beforeEach(async ({ page }) => login(page, accounts.customer.email, accounts.customer.password));

  test('home (authed)', async ({ page }) => screenshotRoute(page, '/', 'customer-home'));
  test('orders', async ({ page }) => screenshotRoute(page, '/orders', 'customer-orders'));
  test('basket planner', async ({ page }) => screenshotRoute(page, '/plan-basket', 'customer-basket-planner'));
  test('trip planner', async ({ page }) => screenshotRoute(page, '/trip-planner', 'customer-trip-planner'));
  test('rescue circles', async ({ page }) => screenshotRoute(page, '/circles', 'customer-circles'));
  test('standing orders', async ({ page }) => screenshotRoute(page, '/standing-orders', 'customer-standing-orders'));
  test('referrals', async ({ page }) => screenshotRoute(page, '/referrals', 'customer-referrals'));
  test('business apply', async ({ page }) => screenshotRoute(page, '/businesses/apply', 'customer-business-apply'));
  test('account settings', async ({ page }) => screenshotRoute(page, '/account/settings', 'customer-account-settings'));
});

test.describe('Business manager routes', () => {
  test.beforeEach(async ({ page }) => login(page, accounts.manager.email, accounts.manager.password));

  test('dashboard', async ({ page }) => screenshotRoute(page, '/dashboard', 'manager-dashboard'));
  test('businesses', async ({ page }) => screenshotRoute(page, '/businesses', 'manager-businesses'));
  test('business edit', async ({ page }) => screenshotRoute(page, `/businesses/edit/${seededBusinesses.stadionulDeGusturi}`, 'manager-business-edit'));
  test('packages', async ({ page }) => screenshotRoute(page, '/packages', 'manager-packages'));
  test('package create', async ({ page }) => screenshotRoute(page, '/packages/create', 'manager-package-create'));
  test('package templates', async ({ page }) => screenshotRoute(page, '/packages/templates', 'manager-package-templates'));
  test('order management', async ({ page }) => screenshotRoute(page, '/orders/manage', 'manager-order-management'));
  test('order scan', async ({ page }) => screenshotRoute(page, '/orders/scan', 'manager-order-scan'));
  test('payments', async ({ page }) => screenshotRoute(page, '/payments', 'manager-payments'));
  test('account settings dashboard', async ({ page }) => screenshotRoute(page, '/account-settings', 'manager-account-settings'));
});

test.describe('Admin routes', () => {
  test.beforeEach(async ({ page }) => login(page, accounts.admin.email, accounts.admin.password));

  test('businesses (admin)', async ({ page }) => screenshotRoute(page, '/businesses', 'admin-businesses'));
  test('business create', async ({ page }) => screenshotRoute(page, '/businesses/create', 'admin-business-create'));
  test('users', async ({ page }) => screenshotRoute(page, '/users', 'admin-users'));
  test('reports', async ({ page }) => screenshotRoute(page, '/reports', 'admin-reports'));
  test('audit log', async ({ page }) => screenshotRoute(page, '/audit-log', 'admin-audit-log'));
  test('types', async ({ page }) => screenshotRoute(page, '/types', 'admin-types'));
});
