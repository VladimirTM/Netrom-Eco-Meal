# Eco Meal — Playwright regression suite

A Playwright suite
against **user-visible selectors only** (`getByRole`, `getByLabel`, `getByText`,
`getByPlaceholder`) so the same spec files could run unchanged against the old Blazor app and the
React app that replaced it — only `BASE_URL` changed. The Blazor app is gone now; these specs run
against React exclusively.

## Prerequisites

- The regression stack up via `docker compose -f ../docker-compose.test.yml up --build` (db,
  Mailpit, Ollama, the Api on `http://localhost:8081`, the frontend on `http://localhost:5174`).
  Seeded accounts are documented in `fixtures/accounts.ts`.
- A real Stripe **test-mode** secret key (set `STRIPE_SECRET_KEY` in a `.env` next to
  `docker-compose.test.yml` — see the root `.env.example`) — the customer and manager golden paths
  pay with Stripe's `4242 4242 4242 4242` test card against `checkout.stripe.com`, so this needs
  network access.
- `npm install` and `npx playwright install --with-deps chromium` inside this directory.

## Running

```bash
npm test                                  # everything, against BASE_URL (default http://localhost:5174)
npx playwright test tests/golden-path     # just the functional/golden-path specs
npx playwright test tests/visual.spec.ts  # just the visual regression baseline
npx playwright test --ui                  # interactive
npm run report                            # open the last HTML report
```

Point it at the Vite dev server instead of the Dockerized nginx build:

```bash
BASE_URL=http://localhost:5173 npm test
```

## Layout

- `fixtures/accounts.ts` — seeded demo credentials and business IDs (from `Database/DbSeeder.cs`).
- `fixtures/auth.ts` — login/logout helpers.
- `fixtures/checkout.ts` — the shared Stripe-test-card checkout flow (customer + manager specs).
- `fixtures/mailpit.ts` — polls Mailpit's REST API for a confirmation/reset email and extracts the
  link, instead of scripting Mailpit's own UI.
- `tests/golden-path/*.spec.ts` — one file per role, each covering that role's golden path.
- `tests/visual.spec.ts` — full-page screenshot baseline for every static route, run once per
  `playwright.config.ts` project (desktop/mobile × light/dark).

## Design notes

- **Idempotent by construction.** The whole suite must pass 3 times
  in a row without a database reset in between. Specs that mutate state either operate on data they
  create themselves in that same run (a fresh order via Stripe checkout, a fresh business
  application, a throwaway registered account for the role-change test) or undo what they changed
  (a bulk quantity adjustment of +1 then -1, a favorite toggled on then off, dark mode toggled on
  then off). Nothing here permanently mutates the fixed seed data from `DbSeeder.cs`.
- **`workers: 1`.** The specs share seeded accounts and a single database — parallel workers would
  race each other (e.g. two workers both trying to be "the" cart for `demo.customer@ecomeal.local`).
  Splitting seed data per worker is a good follow-up once this suite needs to run faster.
- Screenshots that would need an ID minted at runtime (an order, a pickup pass, a Rescue Circle
  invite) are deliberately left out of `visual.spec.ts` — those flows already have functional
  assertions in `tests/golden-path/`, just not a static screenshot.
- **One known, accepted exception to "nothing mutates seed data permanently":** the manager golden
  path completes a real order (confirm + redeem the pickup pass), which — correctly — leaves that
  package's seeded stock permanently decremented by one, the same as a real pickup would. It reuses
  "Golden Boot Surprise Bag" at Stadionul de Gusturi (seeded quantity 5), so this is safe for several
  runs but not infinite; if it ever runs dry, bump that package's seed quantity in
  `Database/DbSeeder.cs` or switch the spec to a higher-quantity package.
