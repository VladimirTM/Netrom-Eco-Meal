import { test as base, expect } from '@playwright/test';

// Several pages (Login/Register/etc.) have a CSS entrance fade-in on load. Playwright's
// actionability check retries a click until the target's bounding box is stable across two
// consecutive frames — an opacity/transform transition can keep failing that check for the
// transition's full duration, which reads as "element is not stable" and can eat most of a
// test's timeout. Killing transitions/animations for every page this test suite opens is the
// standard fix, applied once here instead of in every spec.
export const test = base.extend({
  page: async ({ page }, use) => {
    await page.addInitScript(() => {
      const style = document.createElement('style');
      style.textContent = `
        *, *::before, *::after {
          animation-duration: 0s !important;
          animation-delay: 0s !important;
          transition-duration: 0s !important;
          transition-delay: 0s !important;
        }
      `;
      document.documentElement.appendChild(style);

      // Repeat visits to /account/login (register -> unconfirmed-login -> confirm -> login, all
      // in one browser context) let Chromium's own autofill start suggesting/overwriting the
      // Email field after we've already filled it, clearing it again before submit. None of the
      // app's own forms need browser autofill for an automated test to work, so disable it
      // outright on every form field this suite ever fills.
      const disableAutocomplete = () => {
        document.querySelectorAll('input, form').forEach((el) => el.setAttribute('autocomplete', 'off'));
      };
      document.addEventListener('DOMContentLoaded', disableAutocomplete);
      disableAutocomplete();
    });
    await use(page);
  },
});

export { expect };
