import type { Locator, Page } from '@playwright/test';
import { expect } from '@playwright/test';

// These plain (non-Blazor-rendered) forms occasionally lose a field's value between our fill()
// and the submit click — reproduced with Email specifically on repeat visits to /account/login in
// the same browser context, most likely Chromium's own autofill reconciling against a field it
// recognizes. Filling, then verifying the value stuck (re-filling once if not) is cheap insurance
// against that, regardless of the exact cause.
async function fillReliably(locator: Locator, value: string) {
  await locator.fill(value);
  try {
    await expect(locator).toHaveValue(value, { timeout: 2_000 });
  } catch {
    await locator.fill(value);
    await expect(locator).toHaveValue(value, { timeout: 2_000 });
  }
}

export async function login(page: Page, email: string, password: string) {
  await page.goto('/account/login');
  await expect(page.getByLabel('Email')).toBeVisible();

  await fillReliably(page.getByLabel('Email'), email);
  await fillReliably(page.getByLabel('Password'), password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  // A failed login re-renders the same /account/login with ?error=; success redirects away.
  await page.waitForURL((url) => !url.pathname.startsWith('/account/login'));
}

export { fillReliably };

export async function logout(page: Page) {
  // The logout button's only child is an icon with no aria-label — its `title` attribute isn't
  // surfaced as an accessible name in practice, so getByRole('button', { name: 'Sign out' })
  // doesn't match. A plain CSS attribute selector is the reliable way to hit it.
  const signOutButton = page.locator('button[title="Sign out"]');
  await signOutButton.click();
  // AuthContext's logout is client-side only: it clears the token/user and lets RequireRole's own
  // re-render redirect fire — but only on a route that RequireRole actually guards. Logging out
  // from a public page (home, a business detail page) clears the session in place with no
  // navigation at all, since there's no guard there to react to it. Waiting for the sign-out
  // button itself to disappear is the one signal that's true either way; a caller that specifically
  // needs the post-redirect URL (e.g. the `?returnUrl=` round-trip) asserts that separately.
  await expect(signOutButton).toBeHidden();
}
