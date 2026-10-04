import { test, expect } from '../../fixtures/test';
import { accounts, seededBusinesses } from '../../fixtures/accounts';
import { login, logout, fillReliably } from '../../fixtures/auth';
import { placeOrderViaStripe } from '../../fixtures/checkout';

// A minimal valid 1x1 PNG, uploaded as the package photo — no fixture image file needed on disk.
const onePixelPng = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64',
);

test.describe('Business manager golden path', () => {
  test('switch business, create a package, bulk-adjust quantity, confirm+complete an order, validate by number, export CSV, dashboard stats', async ({ page }) => {
    await login(page, accounts.manager.email, accounts.manager.password);

    // Dashboard stats render.
    await page.goto('/dashboard');
    await expect(page.getByRole('heading', { name: /Business Analytics|Business tools/i }).first()).toBeVisible();

    // Switch business — demo.manager staffs both Stadionul de Gusturi and VAR Bistro.
    await page.getByTitle('Switch business').click();
    await page.getByRole('button', { name: 'VAR Bistro' }).click();
    await expect(page.getByTitle('Switch business')).toContainText('VAR Bistro');
    await page.getByTitle('Switch business').click();
    await page.getByRole('button', { name: 'Stadionul de Gusturi' }).click();
    await expect(page.getByTitle('Switch business')).toContainText('Stadionul de Gusturi');

    // Create a package, with a photo.
    await page.goto('/packages/create');
    // The first fill right after a fresh navigation occasionally loses its value (a Blazor
    // enhanced-navigation DOM-patch race) — fillReliably verifies and retries.
    await fillReliably(page.getByLabel('Name'), 'E2E Test Surprise Bag');
    await page.getByLabel('Description').fill('Created by the Phase 0 Playwright baseline.');
    // The Type/Business selects start empty until OnInitializedAsync's data loads over the
    // circuit — wait for its options rather than racing selectOption against an empty <select>.
    await expect(page.getByLabel('Type').locator('option')).not.toHaveCount(0);
    await page.getByLabel('Type').selectOption({ label: 'Surprise Bag' });
    await page.getByLabel('Price').fill('9.99');
    await page.getByLabel('Quantity').fill('3');
    await page.getByLabel('Weight (kg)').fill('1.0');
    const now = new Date();
    const start = new Date(now.getTime() + 60 * 60 * 1000);
    const end = new Date(now.getTime() + 4 * 60 * 60 * 1000);
    // datetime-local inputs read/write the browser's local wall-clock time — build the string from
    // local getters, not toISOString (UTC), or the submitted window drifts by the host's UTC offset.
    const pad = (n: number) => n.toString().padStart(2, '0');
    const toLocalInput = (d: Date) =>
      `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    await page.locator('#pickupStart').fill(toLocalInput(start));
    await page.locator('#pickupEnd').fill(toLocalInput(end));
    await page.locator('#imageFile').setInputFiles({ name: 'test-package.png', mimeType: 'image/png', buffer: onePixelPng });
    await expect(page.locator('#imageFile')).toBeEnabled({ timeout: 10_000 }); // upload finished
    await page.getByRole('button', { name: 'Create Package' }).click();
    await page.waitForURL('**/packages');
    await expect(page.getByText('E2E Test Surprise Bag')).toBeVisible();

    // Bulk-adjust quantity: +1 then -1, so the run is repeatable without drifting seed data.
    const targetPackage = 'Golden Boot Surprise Bag';
    await page.getByRole('checkbox', { name: `Select ${targetPackage}` }).check();
    await page.getByRole('button', { name: 'Adjust quantity' }).click();
    await page.getByLabel('Change quantity by').fill('1');
    await page.getByRole('button', { name: 'Apply' }).click();
    await expect(page.getByRole('heading', { name: 'Adjust quantity' })).not.toBeVisible();
    await page.getByRole('checkbox', { name: `Select ${targetPackage}` }).check();
    await page.getByRole('button', { name: 'Adjust quantity' }).click();
    await page.getByLabel('Change quantity by').fill('-1');
    await page.getByRole('button', { name: 'Apply' }).click();

    // Clean up the package created above, so repeated runs don't accumulate test data.
    await page.getByRole('row', { name: /E2E Test Surprise Bag/ }).getByTitle('Delete').click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Delete package' }).click();
    await expect(page.getByText('E2E Test Surprise Bag', { exact: true })).not.toBeVisible();

    // Place a fresh order as the demo customer (self-contained — this test doesn't depend on
    // customer.spec.ts having run first), then come back as the manager to confirm/complete it.
    await logout(page);
    await login(page, accounts.customer.email, accounts.customer.password);
    // Sanity-check the account switch actually took (a customer-only nav icon), since a stale
    // manager session would otherwise fail confusingly later at an unrelated locator.
    await expect(page.getByRole('link', { name: 'Your orders' })).toBeVisible();
    const orderNumber = await placeOrderViaStripe(page, seededBusinesses.stadionulDeGusturi, 'Golden Boot Surprise Bag', accounts.customer.email);
    // PaymentReturn.razor uses EmptyLayout (no header/sign-out button) — navigate to one that has it.
    await page.goto('/');
    await logout(page);
    await login(page, accounts.manager.email, accounts.manager.password);

    // Confirm the order (Pending -> Confirmed) — completing it happens below, via redeeming its
    // pickup pass, the same way a real counter handoff works (not the manual "Complete" button).
    await page.goto('/orders/manage');
    const managedRow = page.getByRole('row', { name: new RegExp(`#${orderNumber}\\b`) });
    await expect(managedRow).toBeVisible();
    await managedRow.getByRole('button', { name: 'Confirm' }).click();
    await expect(managedRow.getByRole('button', { name: 'Complete' })).toBeVisible();

    // Validate the pickup pass by order number, via OrderScan's manual lookup (no camera needed) —
    // this is what actually completes the order.
    await page.goto('/orders/scan');
    await page.getByPlaceholder('Order number, e.g. 021').fill(orderNumber);
    // The button's disabled state is bound server-side (@bind:event="oninput" over the Blazor
    // circuit) — wait for that round-trip to re-enable it rather than racing the click against it.
    const findOrderBtn = page.getByRole('button', { name: 'Find order' });
    await expect(findOrderBtn).toBeEnabled();
    await findOrderBtn.click();
    await page.getByRole('link', { name: 'Confirm pickup' }).click();
    await page.getByRole('button', { name: 'Confirm pickup' }).click();

    await page.goto('/orders/manage');
    await expect(page.getByRole('row', { name: new RegExp(`#${orderNumber}\\b`) })).toContainText('Completed');

    // CSV export. A JWT can't ride along on a plain <a href> download the way a cookie would, so
    // this is a real <button> (ExportsApiClient.ts): Axios fetches the CSV as a blob, then
    // downloadBlob() builds a throwaway <a download> and clicks it — Chromium still fires a real
    // download event for that synthetic click.
    await page.goto('/orders/manage');
    await page.getByRole('button', { name: 'Export CSV' }).click();
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Download CSV' }).click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/\.csv$/);
  });
});
