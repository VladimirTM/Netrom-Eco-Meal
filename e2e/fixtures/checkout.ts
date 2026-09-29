import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';

export async function addPackageToCart(page: Page, businessId: string, packageRowText: string) {
  await page.goto(`/businesses/${businessId}`);
  await page.locator('.biz-pkg-row', { hasText: packageRowText }).getByRole('button', { name: 'Add' }).click();
}

// From an already-open basket with items in it, pays with Stripe's test card and returns the new
// order's number (e.g. "042") so the caller can look it up later (in /orders, or OrderScan's
// manual search). Real Stripe test-mode round-trip — needs Stripe:SecretKey configured (see
// docker-compose.test.yml) and network access to checkout.stripe.com.
export async function payWithStripeTestCard(page: Page, payerEmail: string): Promise<string> {
  await page.getByRole('button', { name: /Pay.*place order/ }).click();

  await page.waitForURL(/checkout\.stripe\.com/, { timeout: 30_000 });
  await page.getByLabel('Email').fill(payerEmail);
  // "Card" is a payment-method accordion item alongside Klarna/Link — its toggle button is a
  // screen-reader-only element positioned off-screen (not a real click target for a pointer), so
  // dispatch the click event directly rather than trying to click it as if visible on screen.
  await page.getByRole('button', { name: 'Pay with card' }).dispatchEvent('click');
  await page.getByPlaceholder('1234 1234 1234 1234').fill('4242424242424242');
  await page.getByPlaceholder('MM / YY').fill('12/34');
  await page.getByPlaceholder('CVC').fill('123');
  const cardholderName = page.getByLabel(/Cardholder name/i);
  if (await cardholderName.isVisible().catch(() => false)) await cardholderName.fill('E2E Tester');
  await page.getByTestId('hosted-payment-submit-button').click();

  await page.waitForURL(/\/checkout\/return/, { timeout: 30_000 });
  const heading = page.getByRole('heading', { name: /placed/i });
  await expect(heading).toBeVisible();
  const text = await heading.textContent();
  const match = text?.match(/#(\d+)/);
  if (!match) throw new Error(`Could not read an order number out of "${text}"`);
  return match[1];
}

// Convenience for callers (the manager golden path) that don't need to exercise the basket UI
// itself — adds one package to an empty basket and pays straight through.
export async function placeOrderViaStripe(
  page: Page,
  businessId: string,
  packageRowText: string,
  payerEmail: string,
): Promise<string> {
  await addPackageToCart(page, businessId, packageRowText);
  await page.getByRole('button', { name: 'Open your basket' }).click();
  return payWithStripeTestCard(page, payerEmail);
}
