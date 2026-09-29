import { test, expect } from '../../fixtures/test';
import { seededBusinesses } from '../../fixtures/accounts';

test.describe('Anonymous golden path', () => {
  test('home loads, search, filter by type, open a business, see packages, get redirected to login', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: /surplus/i })).toBeVisible();

    // Search "bakery" — matches Poarta de Aur Bakery / Hat-Trick Bakery / Fotbal & Focaccia by name.
    await page.getByPlaceholder('Search kitchens or packages…').fill('bakery');
    await expect(page.getByText(/kitchens? found/i)).toBeVisible();
    await expect(page.getByText('Poarta de Aur Bakery')).toBeVisible();
    await page.getByPlaceholder('Search kitchens or packages…').fill('');

    // Filter by kitchen type via the Filters popover. Its <label> and <select> are siblings with
    // no for/id link, so getByLabel doesn't apply — scope to the field wrapper instead.
    await page.getByRole('button', { name: 'Filters' }).click();
    await page.locator('.em-popover-field', { hasText: 'Kitchen type' }).locator('select').selectOption({ label: 'Bakery' });
    await page.getByRole('button', { name: 'Done' }).click();
    await expect(page.getByText('Poarta de Aur Bakery')).toBeVisible();
    await expect(page.getByText('Stadionul de Gusturi')).not.toBeVisible();

    // Open a business and see its packages.
    await page.getByRole('button', { name: /Poarta de Aur Bakery/ }).click();
    await page.waitForURL(`**/businesses/${seededBusinesses.poartaDeAurBakery}`);
    await expect(page.getByRole('heading', { name: 'Poarta de Aur Bakery' })).toBeVisible();
    await expect(page.getByText('Golden Goal Bread Bag')).toBeVisible();
    // Anonymous visitors get no "Add"/"Add to basket" affordance at all (AuthorizeView with no
    // NotAuthorized branch) — the equivalent gate to exercise is a direct hit on a customer-only
    // route redirecting to login with a returnUrl, same as clicking a hidden nav link would.
    await page.goto('/orders');
    // The cookie middleware's own challenge redirect fires before Blazor ever renders, using
    // ASP.NET's default casing (?ReturnUrl=), not the app's own RedirectToLogin component's
    // lowercase ?returnUrl= (that one only fires for an already-authenticated wrong-role hit).
    await page.waitForURL(/\/account\/login\?ReturnUrl=/i);
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  });
});
