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

// Login.razor was a plain HTML form POST (not client-rendered); the React login page (Phase 4) is
// a controlled form submitted via fetch instead — no native `action` attribute to find, so the two
// targets need different submission strategies. Callers stay unchanged either way — user-visible
// label/role selectors only, per Phase 0's rule.
//
// Blazor-specific repeat-visit flake (observed only against Blazor): repeat visits to
// /account/login within one browser context intermittently and unpredictably clear the Email field
// between our fill() and the submit click (reproduced with retry-and-verify fills, reordered
// fields, and autocomplete disabled — none fully eliminated it, most likely some Chromium
// autofill/bfcache interaction specific to this environment). Setting both fields' values directly
// and submitting the form via JS sidesteps the browser's normal focus/blur/autofill pipeline
// entirely, which the UI-driven approach could not reliably avoid. The React login page hasn't
// shown this flake, so it uses plain fill+click.
export async function login(page: Page, email: string, password: string) {
  await page.goto('/account/login');
  await expect(page.getByLabel('Email')).toBeVisible();

  const blazorForm = page.locator('form[action="api/auth/login"]');
  if ((await blazorForm.count()) > 0) {
    await page.evaluate(
      ([email, password]) => {
        const form = document.querySelector('form[action="api/auth/login"]') as HTMLFormElement;
        (form.querySelector('#login-email') as HTMLInputElement).value = email;
        (form.querySelector('#login-password') as HTMLInputElement).value = password;
        form.requestSubmit();
      },
      [email, password],
    );
  } else {
    await fillReliably(page.getByLabel('Email'), email);
    await fillReliably(page.getByLabel('Password'), password);
    await page.getByRole('button', { name: 'Sign in' }).click();
  }

  // A failed login re-renders the same /account/login with ?error=; success redirects away.
  await page.waitForURL((url) => !url.pathname.startsWith('/account/login'));
}

export { fillReliably };

export async function logout(page: Page) {
  // The logout button's only child is an icon with no aria-label — its `title` attribute isn't
  // surfaced as an accessible name in practice, so getByRole('button', { name: 'Sign out' })
  // doesn't match. A plain CSS attribute selector is the reliable way to hit it.
  await page.locator('button[title="Sign out"]').click();
  // Blazor's logout button posts to AuthController.LogoutAsync, which redirects server-side to a
  // bare /account/login (no returnUrl set from that form). React's logout (Phase 4's AuthContext)
  // is client-side only — it clears the token/user and lets RequireRole's own re-render redirect,
  // which (correctly, see RequireRole.tsx) appends `?returnUrl=<the page you were on>`. Matching
  // only the path, not the full URL, is what makes this assertion work against both targets.
  await page.waitForURL((url) => url.pathname === '/account/login');
}
