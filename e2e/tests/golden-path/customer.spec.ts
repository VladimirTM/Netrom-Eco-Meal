import { test, expect } from '../../fixtures/test';
import { accounts, seededBusinesses } from '../../fixtures/accounts';
import { login, fillReliably } from '../../fixtures/auth';
import { getLatestEmailLink } from '../../fixtures/mailpit';
import { payWithStripeTestCard } from '../../fixtures/checkout';

test.describe('Customer golden path', () => {
  test('register, confirm via Mailpit, and sign in', async ({ page, request }) => {
    // A fresh email per run — this test (unlike the rest of the suite, which reuses seeded
    // accounts) must stay safe to run 3x in a row.
    const email = `e2e.customer.${Date.now()}@ecomeal.local`;
    const password = 'E2ePassword123!';

    await page.goto('/account/register');
    await fillReliably(page.getByLabel('Full name'), 'E2E Customer');
    await fillReliably(page.getByLabel('Email', { exact: true }), email);
    await fillReliably(page.getByLabel('Password'), password);
    await page.getByRole('button', { name: 'Create account' }).click();

    await page.waitForURL(/\/account\/login\?info=/);
    await expect(page.getByText(/check your email/i)).toBeVisible();

    // Logging in before confirming shows the distinct "unconfirmed" error with a resend link.
    await fillReliably(page.getByLabel('Email'), email);
    await fillReliably(page.getByLabel('Password'), password);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByRole('link', { name: 'Resend confirmation email' })).toBeVisible();

    const confirmLink = await getLatestEmailLink(request, email, /https?:\/\/[^\s"']*\/account\/confirm-email\?[^\s"']*/);
    await page.goto(confirmLink);
    await expect(page.getByRole('heading', { name: 'Confirm account' })).toBeVisible();
    await expect(page.getByText('Your account is confirmed')).toBeVisible();

    await login(page, email, password);
    await expect(page.locator('button[title="Sign out"]')).toBeVisible();
  });

  test('browse, basket, checkout, orders, pickup pass, review and favorite', async ({ page }) => {
    await login(page, accounts.customer.email, accounts.customer.password);

    // Add a package from one business.
    await page.goto(`/businesses/${seededBusinesses.poartaDeAurBakery}`);
    await page.locator('.biz-pkg-row', { hasText: 'Golden Goal Bread Bag' }).getByRole('button', { name: 'Add' }).click();
    await page.getByRole('button', { name: 'Open your basket' }).click();
    await expect(page.getByText('Poarta de Aur Bakery', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Close basket' }).click();

    // Single-business basket rule: adding from a different business prompts to replace it.
    await page.goto(`/businesses/${seededBusinesses.stadionulDeGusturi}`);
    await page.locator('.biz-pkg-row', { hasText: 'Final Whistle Meal Box' }).getByRole('button', { name: 'Add' }).click();
    await expect(page.getByRole('heading', { name: 'Start a new basket?' })).toBeVisible();
    await page.getByRole('button', { name: 'Start new basket' }).click();

    // Checkout the replaced basket with the Stripe test card.
    await page.getByRole('button', { name: 'Open your basket' }).click();
    await expect(page.getByText('Stadionul de Gusturi', { exact: true })).toBeVisible();
    const orderNumber = await payWithStripeTestCard(page, accounts.customer.email);

    // The new order shows up in /orders. The seed data has other Stadionul de Gusturi orders
    // already, so match on the order number, not just the business name.
    await page.goto('/orders');
    const newTicket = page.locator('.order-ticket', { hasText: `#${orderNumber}` });
    await expect(newTicket).toBeVisible();

    // Cancel the order we just placed (self-contained — never touches the fixed seed data, so
    // this test stays safe to re-run without a fresh database).
    await newTicket.getByRole('button', { name: 'Cancel order' }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Cancel order' }).click();
    await expect(newTicket).toContainText(/Cancelled/i);

    // Open the pickup pass for the seeded Confirmed order (VAR Bistro), which the seed data
    // splits into 3 passes — exercises the pass switcher too. Filter to the Confirmed tab first:
    // with enough repeat runs, pagination alone could push it past an unfiltered first page.
    await page.getByRole('button', { name: 'Confirmed' }).click();
    const confirmedTicket = page.locator('.order-ticket', { hasText: 'VAR Bistro' }).first();
    await confirmedTicket.getByRole('link', { name: 'Show QR code' }).click();
    await page.waitForURL(/\/orders\/pickup\//);
    await expect(page.getByRole('tab', { name: 'Pass 1' })).toBeVisible();
    await page.getByRole('tab', { name: 'Pass 2' }).click();
    // Blazor's plain-bool attribute binding renders aria-selected as a bare/empty attribute
    // rather than the string "true"/"false" the ARIA spec calls for — matching actual behavior
    // here rather than the spec; the active tab's CSS class is the reliable signal either way.
    await expect(page.getByRole('tab', { name: 'Pass 2' })).toHaveClass(/active/);

    // Leave (or update) a review at a business with a Completed order — CanReview only requires
    // order history there, so this is safe to re-run (SubmitReviewAsync upserts).
    await page.goto(`/businesses/${seededBusinesses.stadionulDeGusturi}`);
    const reviewForm = page.locator('.biz-review-form');
    if (await reviewForm.isVisible().catch(() => false)) {
      await reviewForm.getByRole('button', { name: '5 stars' }).click();
      await reviewForm.getByRole('button', { name: /Submit review|Update review/ }).click();
    }

    // Toggle a favorite and toggle it back, so the run stays idempotent. Role/name matching is
    // unreliable here (getByRole with this name regex hung rather than resolving even though the
    // button was clearly on screen) — fall back to the CSS class BusinessDetail.razor gives the
    // favorite button specifically (shared with the report button next to it, minus its title).
    const favoriteBtn = page.locator('.biz-modal-fav-btn:not([title])');
    const wasFavorited = (await favoriteBtn.textContent())?.includes('Favorited');
    await favoriteBtn.click();
    await expect(favoriteBtn).toHaveText(wasFavorited ? 'Favorite' : 'Favorited');
    await favoriteBtn.click();
    await expect(favoriteBtn).toHaveText(wasFavorited ? 'Favorited' : 'Favorite');

    // Toggle the theme and toggle it back.
    const themeToggle = page.getByRole('button', { name: 'Toggle dark mode' });
    await themeToggle.click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    await themeToggle.click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  });
});
