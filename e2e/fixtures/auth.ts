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

// Login.razor is a plain HTML form POST (not client-rendered), same shape the React app's own
// login page will keep — user-visible label/role selectors only, per Phase 0's rule.
//
// Repeat visits to /account/login within one browser context intermittently and unpredictably
// clear the Email field between our fill() and the submit click (reproduced with retry-and-verify
// fills, reordered fields, and autocomplete disabled — none fully eliminated it, most likely some
// Chromium autofill/bfcache interaction specific to this environment). Setting both fields' values
// directly and submitting the form via JS sidesteps the browser's normal focus/blur/autofill
// pipeline entirely, which the UI-driven approach could not reliably avoid.
export async function login(page: Page, email: string, password: string) {
  await page.goto('/account/login');
  await expect(page.getByLabel('Email')).toBeVisible();
  await page.evaluate(
    ([email, password]) => {
      const form = document.querySelector('form[action="api/auth/login"]') as HTMLFormElement;
      (form.querySelector('#login-email') as HTMLInputElement).value = email;
      (form.querySelector('#login-password') as HTMLInputElement).value = password;
      form.requestSubmit();
    },
    [email, password],
  );
  // A failed login re-renders the same /account/login with ?error=; success redirects away.
  await page.waitForURL((url) => !url.pathname.startsWith('/account/login'));
}

export { fillReliably };

export async function logout(page: Page) {
  // The logout button's only child is an icon with no aria-label — its `title` attribute isn't
  // surfaced as an accessible name in practice, so getByRole('button', { name: 'Sign out' })
  // doesn't match. A plain CSS attribute selector is the reliable way to hit it.
  await page.locator('button[title="Sign out"]').click();
  // AuthController.LogoutAsync redirects to /account/login (no returnUrl set from this form).
  await page.waitForURL('**/account/login');
}
