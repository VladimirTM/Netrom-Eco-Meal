# Netrom Eco Meal

An app for rescuing surplus food. Restaurants, bakeries, cafes, grocery stores and food
trucks list surplus packages (surprise bags, meal boxes, bread bags...) at a discount,
and customers browse, order and pick them up before they'd otherwise go to waste.

A React + TypeScript SPA (`Frontend/`) talks to a layered ASP.NET Core Web API
(`Backend/`) over JWT-authenticated REST and a SignalR hub for live stock updates. See
[BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md) and
[FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md) for how each half is put together.

Three roles: **Customer** (browses, orders, leaves reviews), **BusinessManager** (manages
packages and orders for whichever business or businesses they're staff of) and **Admin**
(manages businesses, staff, types and users).

See [USER_GUIDE.md](USER_GUIDE.md) for a step-by-step walkthrough of each role — this section
is just the capability summary.

## What each role can do

**Customer** — what you get on self-registration:

- Browse, search and filter businesses on the home page, sorted by name, "closing soon",
  or "near me" (browser geolocation) — filter by kitchen type or dietary/allergen tag, with
  an optional map view of every kitchen that has a saved location
- View a business's live packages, including its weekly hours/holiday closures, and add
  packages to a basket — the package list (and "N left"/"Sold out" state) updates live for
  anyone browsing it, with no refresh needed, if stock changes while they're looking
- Check out via Stripe Checkout, and track past orders with pickup windows on `/orders` —
  optionally leaving a free-text logistics note for the business ("running late," "can't carry
  it to my car"...) right in the basket before paying
- Once more than one order is live at once, plan a pickup route on `/trip-planner` — a
  nearest-neighbor stop order (starting from your own location, if shared) over a numbered list
  and map
- Split a basket with friends as a Rescue Circle (2-6 people): everyone pays their own share
  via their own Stripe Checkout, the order only moves forward once every share is in, and
  each participant gets their own labeled pickup pass — see `/circles`
- Show a QR pickup pass for a confirmed order, scanned by the business at collection —
  split a group order into several separate passes so whoever gets there first can scan
- Leave a star rating and comment on a business (optionally tagged to a specific package)
  once you've ordered from it
- Report a business or package that looks wrong, and apply to list your own business for
  admin review on `/businesses/apply`
- Opt in to the `/impact` community leaderboard, ranking the month's top food-rescuers by kg
  saved — off by default, and a customer who never opts in never appears on it; the page also
  shows the platform's total kg saved translated into a rough km-driven/water-saved equivalency,
  the same one shown on a completed order's own receipt
- Earn an automatic discount from a kitchen's loyalty punch card (where one's configured), and
  save a "usual" as a standing order — matched against a kitchen's newly published packages and
  added straight to your basket to confirm, up to a weekly budget you set, on `/standing-orders`
- Build a rescue streak — consecutive weeks with at least one completed order, shown on `/orders`
  and as an optional column on the `/impact` leaderboard
- Leave a short practical tip on a business page ("use the side door after 8pm"), separate from
  star reviews and moderated the same way a reported business/package is
- Invite a friend from `/referrals`: once they complete their first order, you both get store
  credit, applied automatically (alongside any loyalty discount) at your next checkout
- Browse chains with more than one location on `/brands` — a combined rating across every
  branch, and a favorite button that follows the whole chain rather than one address, with a
  "nearest location open now" shortcut if you share your location

**BusinessManager** — staff of one or more businesses (assigned by an Admin), scoped to
whichever one they pick in the sidebar switcher:

- Manage packages on `/packages` for the currently selected business — including a photo
  (upload or paste a URL), "repeat this every day" recurring templates managed on
  `/packages/templates`, bulk duplicate/adjust-quantity/extend-pickup-window actions, and an
  AI markdown-price suggestion for any package closing soon with stock still unsold, and
  "mark as donated" for a package that closed with nothing sold, so it still counts toward
  food-saved impact instead of just expiring
- Set your business's weekly opening hours and one-off holiday closures, upload a business
  photo, and configure a loyalty punch card (every N orders/month, X lei off) — all from the
  business edit page
- Confirm, complete or cancel orders placed at the currently selected business on
  `/orders/manage` — cancelling automatically refunds the customer's Stripe payment; a customer's
  free-text logistics note, if left, shows right on the order
- Scan a customer's pickup QR code on `/orders/scan` to confirm pickup, or look the order up by
  number instead when scanning isn't practical
- See stats scoped to the currently selected business on `/dashboard` (including a
  sell-through rate and busiest pickup hours), a payout ledger of every payment collected
  (and refunded) on `/payments`, and export order history as CSV
- Grab a public, read-only "share your impact" embed snippet from `/dashboard` — a small script
  your own website can drop in to show your kitchen's live kg-saved/CO2e/water numbers, no login
  needed on the visitor's end
- Generate an API key from `/dashboard` and let your own POS/inventory system create live
  packages directly via a webhook, instead of typing each one into Add Package by hand
- Group your business with its other locations under one shared brand page (assigned from the
  business edit form; an admin manages the list of brands)
- Staffing more than one business surfaces a switcher in the sidebar to pick which one is
  "current" for every page above — staffing just one skips the switcher entirely

**Admin** — full access, plus the only role that can create businesses directly:

- Create and edit any business on `/businesses`, including assigning staff (any number of
  managers, and a manager can staff more than one business) and an optional lat/lng location
- Manage packages (and recurring templates) for any business on `/packages`
- Review and manage orders across every business on `/orders/manage`
- Promote or demote users between Customer, BusinessManager and Admin on `/users`
- See store-wide stats on `/dashboard` and every payment across every business on `/payments`
- Approve or reject self-service business applications on `/businesses`, hide/unhide a
  business or package without deleting it, and review customer reports on `/reports`
- Add, rename or remove kitchen and package types, and group businesses into brands, on
  `/types` — no code change or migration needed for a new category, and a type or brand still in
  use can't be deleted
- See who did what — role changes, business create/edit/delete/staffing, approvals,
  moderation — on `/audit-log`

Every role also gets a page to update their display name or change their password — customers
via the gear icon in the header (`/account/settings`), staff via "Account Settings" in the
dashboard sidebar (`/account-settings`), same form either way.

## Stack

**Backend** (`Backend/NetromEcoMeal.{Api,BusinessLogic,DataAccess,Tests}`):

- ASP.NET Core 10 Web API, JWT bearer auth (ASP.NET Identity underneath for password
  hashing/roles/tokens) — see D3 in [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md)
- EF Core + PostgreSQL (Npgsql)
- Serilog for structured logging (console sink, per-request logging, config-driven levels)
- QRCoder for server-side pickup QR generation, `jsqr` (frontend-side) for camera scanning
- Stripe Checkout (`Stripe.net`) for payment
- Web Push (`WebPush`, VAPID-signed) for browser push notifications
- `Microsoft.Extensions.AI` (`IChatClient`) backed by `OllamaSharp`, against a free, self-hosted
  [Ollama](https://ollama.com) instance — no paid/hosted AI API anywhere in the app
- SignalR hub (`/hubs/stock`) for live package-stock updates

**Frontend** (`Frontend/eco-meal-frontend`):

- React 19 + TypeScript, Vite, React Router v7
- Axios (bearer-token interceptor, global 401 → logout) + `@microsoft/signalr`
- Leaflet + OpenStreetMap tiles (CDN, no API key) for the home page's map view
- `qrcode` for client-side pickup-pass QR rendering
- Hand-written `src/index.css` design system (tokens, light/dark) on top of vendored Bootstrap CSS;

## Running locally

You need .NET 10, Node 22+, and a Postgres instance. Run the backend and frontend as two
separate processes.

**Backend** — configure the connection string and seed admin credentials with user
secrets rather than committing them to `appsettings.json` (run from
`Backend/NetromEcoMeal.Api`, which owns the user-secrets id):

```bash
cd Backend/NetromEcoMeal.Api
dotnet user-secrets set "ConnectionStrings:EcoMealContext" "Host=localhost;Port=5432;Database=EcoMeal;Username=postgres;Password=yourpassword"
dotnet user-secrets set "SeedAdmin:Email" "admin@ecomeal.local"
dotnet user-secrets set "SeedAdmin:Password" "Admin123!"
dotnet user-secrets set "Jwt:Key" "any-string-32-characters-or-longer"
dotnet run
```

Migrations and seed data (roles, business/package types, the demo Timișoara businesses
and packages, and the admin account) run automatically on startup — no separate migrate
step needed. The Api listens on `http://localhost:5080` by default (see
`Properties/launchSettings.json`).

No SMTP server is required to run the app: emails (order updates, back-in-stock alerts,
account confirmation/password reset) are just logged instead of sent when `Email:Smtp:Host`
isn't configured. See [Email](#email) below to wire up a real sender or a local catcher.

**Frontend** — point it at the Api above and start the Vite dev server:

```bash
cd Frontend/eco-meal-frontend
cp .env.example .env.local   # VITE_API_URL=http://localhost:5080/api
npm install
npm run dev
```

Opens on `http://localhost:5173`. Log in with the seeded admin account above, or any of
the [demo accounts](#seed-data) below.

## Logging

Every log line — app startup, request timing/status via `UseSerilogRequestLogging`, unhandled
exceptions, `DbSeeder`'s own `ILogger` calls — goes through Serilog rather than the default
console logger, writing structured lines to the console (readable locally, still line-per-event
under `docker compose logs`). Configure sinks and minimum levels under the `Serilog` section in
`appsettings.json`/`appsettings.{Environment}.json` — no code changes needed to add a sink (e.g.
a file or a hosted log aggregator) or turn up verbosity for a specific namespace; see the
`Serilog:MinimumLevel:Override` blocks already there for `Microsoft.AspNetCore` and
`Microsoft.EntityFrameworkCore` as an example. `appsettings.Development.json` defaults to
`Debug` instead of `Information` for local runs.

## Email

Order confirm/complete/cancel/no-show, pickup reminders, and back-in-stock alerts send an
email alongside the in-app notification, via a small SMTP sender (`IAppEmailSender`/
`SmtpEmailSender`, plain `System.Net.Mail`, no extra package). Configure it with:

```bash
dotnet user-secrets set "Email:Smtp:Host" "smtp.example.com"
dotnet user-secrets set "Email:Smtp:Port" "587"
dotnet user-secrets set "Email:Smtp:Username" "..."
dotnet user-secrets set "Email:Smtp:Password" "..."
dotnet user-secrets set "Email:Smtp:EnableSsl" "true"
dotnet user-secrets set "Email:FromAddress" "no-reply@ecomeal.local"
```

`App:BaseUrl` (used to build links in confirmation/reset emails, Stripe redirect URLs and
pickup-pass QR codes) already defaults to `http://localhost:5173` via
`appsettings.Development.json` — the **frontend's** origin, not the Api's, since that's
where a clicked link needs to land — only override it with
`dotnet user-secrets set "App:BaseUrl" "..."` if the frontend runs on a different port or URL.

Leave `Email:Smtp:Host` unset (the default) and every email is logged instead of sent —
handy for local dev without a real mailbox. `docker-compose.test.yml` instead points it at
a bundled [Mailpit](https://github.com/axllent/mailpit) container, so emails are visible in
a real inbox UI without any external service — see [Running with Docker](#running-with-docker).

By default, self-registered accounts sign in immediately (no confirmation required), the
same as before this feature existed. Set `Identity:RequireConfirmedAccount` to `true` to
require clicking an emailed confirmation link before sign-in works — this needs
`Email:Smtp:Host` configured to actually deliver that link. Password reset
(`/account/forgot-password`) works either way, regardless of that flag. Lost or expired the
confirmation email? `/account/resend-confirmation` sends a fresh one — also linked directly
from the login error when that's why sign-in failed.

## Payments

Checkout redirects to a real Stripe Checkout Session (test-mode). An `Order` is only created
once Stripe confirms payment — an abandoned checkout never creates a phantom order. Configure
a free Stripe **test-mode** secret key to enable it:

```bash
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:Currency" "ron"
```

Get a test-mode key from [dashboard.stripe.com/test/apikeys](https://dashboard.stripe.com/test/apikeys) —
no live/production key is ever needed for this app. Leave `Stripe:SecretKey` unset (the
default) and checkout shows a friendly "payments aren't configured yet" error instead of a
raw SDK exception; every other part of the app (browsing, order history, everything but
actually checking out) still works with zero Stripe setup. Cancelling a paid order (manually
or via the stale-Pending sweep) automatically refunds the charge; a `NoShow` deliberately
does **not** — the kept charge doubles as the no-show fee. If the refund itself fails (a
Stripe-side error), the order still cancels but the payment is flagged `RefundFailed` instead
of silently staying `Paid` — surfaced as a distinct badge everywhere payment status shows up,
plus a note in the customer's cancellation email. A Rescue Circle order (above) uses the same
Stripe setup, just split into one Checkout session per participant instead of one for the whole
order — cancelling it refunds whichever participants had actually paid, individually. Splitting
too many ways for too little (any share landing under Stripe's own minimum chargeable amount)
is rejected upfront with a plain "try fewer participants" message, same as a loyalty/store-credit
discount can never shrink a solo checkout below that same floor.

## Web Push Notifications

Every in-app notification (order updates, back-in-stock alerts, business approval/rejection...)
can also show up as a real browser notification, even when the app tab isn't open — the
notification bell's popup has an "enable browser alerts" toggle that registers a service
worker and subscribes via the browser's Push API. Unlike Stripe/SMTP above, this needs **no
external account at all** — push just needs a VAPID key pair, a self-generated cryptographic
key pair, not a third-party credential. `appsettings.Development.json` already ships a working
(if only locally-meaningful) key pair, so `dotnet run` and
`docker compose -f docker-compose.test.yml up` both have working push out of the box with no
setup. To use your own pair instead (e.g. before deploying anywhere real), generate one with
`WebPush.VapidHelper.GenerateVapidKeys()` (from the `WebPush` NuGet package) and set:

```bash
dotnet user-secrets set "WebPush:PublicKey" "..."
dotnet user-secrets set "WebPush:PrivateKey" "..."
dotnet user-secrets set "WebPush:Subject" "mailto:you@example.com"
```

Leave any of the three unset and the "enable browser alerts" toggle hides itself entirely —
same degrade-gracefully pattern as a missing Stripe key, just with no external signup behind
it. The pickup QR scanner's HTTPS-or-localhost restriction (see below) applies here too:
service workers (and so push) only work on `https://` or `localhost`.

## AI Features

Five AI features have shipped so far: a "Write it for me" button that drafts a package
description on `PackageForm`, an AI search bar on the home page — try "vegan dinner
under 30 lei, closing soon" — a periodic background sweep that nudges a business's
favoriters/past customers when one of its packages is closing soon with stock still unclaimed
(personalizing the copy when it matches something they've ordered before), a `/plan-basket`
page where a customer gives a headcount/budget/dietary tag and a tool-calling agent proposes a
basket of real, in-stock packages from a single kitchen for approval before it touches the cart,
and a 🏷️ badge on `/packages` for any package closing soon with stock left, where a manager can
ask a tool-calling agent for a markdown price suggestion grounded in that business's own real
sell-through history — shown as a dismissable suggestion, never applied automatically.
Every AI feature in this app runs against a free, self-hosted [Ollama](https://ollama.com)
instance via `Microsoft.Extensions.AI`'s `IChatClient` (backed by `OllamaSharp`) — there's no
paid or hosted AI API anywhere in the stack. Configure it with:

```bash
dotnet user-secrets set "Ollama:BaseUrl" "http://localhost:11434"
dotnet user-secrets set "Ollama:ModelId" "qwen2.5:7b"
```

Leave `Ollama:BaseUrl` unset (the default) and every AI feature shows a friendly "AI features
aren't available yet" error instead of failing — same degrade-gracefully pattern as a missing
Stripe key or SMTP host; the rest of the app works normally either way.
`docker-compose.test.yml` instead bundles an `ollama` container built from `Dockerfile.ollama`,
which bakes `qwen2.5:7b` into the image at build time — `docker compose up --build` gives a
fully working "Write it for me" button, AI search bar, near-expiry nudge sweep, budget planner,
and markdown-pricing suggestion with no separate manual pull step. `qwen2.5:7b` was picked
specifically because it's a model Ollama has verified function-calling support for, which the
budget planner's and markdown-pricing agent's tool-calling needs.
That first build downloads the model (a few GB), so it's slower than the app's own image the
first time; the result persists in the `ecomeal-test-ollama` volume across restarts either way.

## Running with Docker

`docker-compose.test.yml` spins up Postgres, the Api and the React frontend together,
which is the easiest way to try the whole app without installing anything locally. It's
meant for local testing/demoing, not for production (fixed DB password, HTTP only).

```bash
docker compose -f docker-compose.test.yml up --build
```

The frontend comes up on **http://localhost:5174**, talking to the Api on
**http://localhost:8081**, backed by a Postgres container on port 5433 (so it doesn't
clash with a Postgres you might already have running locally on 5432). Data persists in
the `ecomeal-test-db` volume across restarts — tear it down with
`docker compose -f docker-compose.test.yml down -v` if you want a clean slate. Manager-uploaded
package/business photos persist the same way, in a separate `ecomeal-test-uploads` volume.

A seeded admin account is created automatically:

- **Email:** admin@ecomeal.local
- **Password:** Admin123!

Change `SeedAdmin__Email` / `SeedAdmin__Password` in `docker-compose.test.yml` before
running if you don't want the default admin credentials. Five demo accounts (see
[Seed data](#seed-data) below) are also created regardless of that setting, so you can log
in as a customer or business manager and see the app already in use.

This compose file also runs a [Mailpit](https://github.com/axllent/mailpit) container and
points the Api's SMTP settings at it, so every email the app sends (order updates,
back-in-stock alerts, account confirmation, password reset) is visible at
**http://localhost:8025** instead of going nowhere. It also sets
`Identity__RequireConfirmedAccount=true`, so a freshly self-registered account needs its
confirmation link (check Mailpit) clicked before it can sign in — the two seeded demo
accounts are unaffected, since they're created pre-confirmed.

`Stripe__SecretKey` and `Jwt__Key` are driven from environment variables the committed
compose file never hardcodes a real value for (`${STRIPE_SECRET_KEY:-}` and a clearly
dev-only `Jwt__Key` fallback). To use a real Stripe test-mode key, copy `.env.example` to
a gitignored `.env` next to `docker-compose.test.yml` and fill it in:

```bash
cp .env.example .env
# then edit .env: STRIPE_SECRET_KEY=sk_test_...
```

`docker compose` reads `.env` automatically — no extra `-f` flag needed, and the key never
touches a tracked file. Leave it unset and checkout shows the same "payments aren't
configured yet" error as [Payments](#payments) above; no live/production Stripe key is
ever needed to try the app.

The pickup QR scanner (`/orders/scan`) uses the device camera, which browsers only allow
over HTTPS or on `localhost`. It works fine when you open the app as `localhost:5174`,
but won't get camera access if you open it via a LAN IP from another device (e.g. testing
on a phone) — that needs a real HTTPS deployment.

## Seed data

On first run (and on every subsequent startup) `DbSeeder` makes sure the reference data
(roles, business types, package types, order statuses) and a set of World Cup–themed demo
businesses/packages in Timișoara exist, each with an approximate lat/lng so "near me" sort
and the map view have real data to show. It's safe to re-run: it only fills in what's
missing and refreshes expired pickup windows or stale placeholder images, it never
touches data you've added or customized through the app.

It also turns one of the demo-managed business's packages into a recurring template, so
`/packages/templates` and the 🔁 "Daily" badge on `/packages` aren't empty on a fresh
database — `PackageTemplateGenerationService` takes over generating that package's future
daily instances from there.

Every demo business gets a full weekly schedule too — varied by type (restaurants run an
evening service and close one weekday, bakeries/cafes open mornings through early evening,
groceries run long hours every day, food trucks are evening-only) — so the "closed now"
indicator has real variety instead of every kitchen reading the same open/closed. One business
(Cartonaș Galben Café) is seeded with an active holiday closure so the closure banner is visible
immediately, and another (Poarta de Aur Bakery) has one starting a few weeks out, to demonstrate
removing a not-yet-active closure without it affecting "closed now" yet.

It also creates three demo accounts, so every feature has real data to look at right away
instead of an empty app:

- **Customer** — demo.customer@ecomeal.local / Demo123! — has past orders in every status
  (completed, confirmed, cancelled, no-show, pending) across several businesses, so
  `/orders`, reorder, the QR pickup pass, favorites and reviews all show something real.
  One confirmed order comes pre-split into 3 passes, to demo the group-pickup flow without
  having to split one yourself first. Between the seeded pending/confirmed orders and the
  Rescue Circle basket below, this account also has three simultaneous active orders across
  three different businesses, so `/trip-planner` has a real multi-stop route to plan on a
  fresh database instead of an empty state. The pending order carries a demo
  `LogisticsNote` ("Running about 10 minutes late…"), visible as a note icon on
  `/orders/manage`.
- **BusinessManager** — demo.manager@ecomeal.local / Demo123! — staffs both Stadionul de
  Gusturi and VAR Bistro, so the sidebar's business switcher has something to switch
  between out of the box. Has a pending order waiting to be confirmed on `/orders/manage`
  and enough order history for `/dashboard`'s trend chart, `/payments`'s ledger, and CSV
  export to be worth looking at. Also has a handful of already-closed packages with
  partial completed sales spread across several days/hours, purely so `/dashboard`'s
  Business Analytics card (sell-through rate, busiest pickup hours) has real history to
  show instead of an empty state — that same history is what the `/packages` markdown-price
  suggestion reasons over for the seeded "Away Day Meal Box", which is priced above it.
- **BusinessManager** — demo.manager2@ecomeal.local / Demo123! — staffs Stadionul de
  Gusturi alongside the first demo manager, demonstrating the other direction of the
  many-to-many (several staff, one business).

This activity is only ever seeded once, the first time the app starts against a genuinely
empty database — unlike the reference/demo-catalog data above, it won't touch orders
placed for real afterward.

Two more accounts — demo.customer2@ecomeal.local and demo.customer3@ecomeal.local (Demo123!
each) — exist purely for the `/impact` leaderboard: both get a real Completed order, but only
demo.customer2 opts in (`ShowOnLeaderboard`), so a fresh database visibly demonstrates the
opt-in filter actually excluding someone with real order history, not just excluding people
with none.

It also seeds trust & safety demo data so `/businesses`, `/reports`, and `/audit-log` aren't
empty on a fresh database: a `PendingApproval` and a `Rejected` business application (both
submitted by the demo customer), one existing demo business, one existing demo package, and one
kitchen tip marked hidden with a reason, five `Report`s (open, dismissed, and three actioned —
one against each moderatable target type: business, package, kitchen tip), and an audit-log
history consistent with all of it.

It also seeds two Rescue Circles on `/circles` at different stages: one still collecting
payments (organizer and demo.customer2 have paid, demo.customer3 has joined but still owes,
and one seat is left open to join), and one already fully paid and confirmed, so the
per-participant pickup passes it generates automatically are visible without having to run
the whole flow yourself first.

Stadionul de Gusturi is seeded with a loyalty punch card (every 8 completed orders/month, 2 lei
off), and the demo customer already has a standing order there (any package, up to 25 lei/week)
on `/standing-orders` — set up against the same business whose "Golden Boot Surprise Bag"
recurring template regenerates daily, so the next real generation tick actually demonstrates a
live match, not just the saved preference itself. demo.customer2 has a second one at VAR Bistro
narrowed to the `Vegetarian` tag.

Stadionul de Gusturi also has two packages demonstrating the donation flow: "Extra Time Surprise
Bag" closed recently with nothing sold and is still a live "mark as donated" candidate on
`/packages` (re-armed on every restart, so the background notification fires again each time),
while "Bench Warmer Bread Bag" is seeded already marked donated — so the home hero's kg-saved
figure, `/impact`'s equivalency stats, and a fresh `/dashboard` "share your impact" snippet all
have a real, non-zero donated contribution right away.

The demo customer also has a completed order in each of the last four calendar weeks, purely so
the "week streak" stat on `/orders` (and the matching column on `/impact`) shows a real, non-zero
streak on a fresh database rather than only whatever the rest of the seeded activity happens to
add up to.

Stadionul de Gusturi and VAR Bistro have a handful of kitchen tips too, including one already
hidden (see the trust & safety paragraph above) — practical hints like "use the side entrance
after 8pm" separate from the star reviews above. And on `/referrals`, the demo customer has
invited both other demo customers: demo.customer2's invite already paid off (a real
`StoreCreditEntry` pair, so the demo customer's balance is non-zero and gets applied automatically
at their next checkout), while demo.customer3's is left pending, so the page shows both a rewarded
and a waiting invite instead of only one.

Three of the bakery businesses (Poarta de Aur Bakery, Hat-Trick Bakery, Fotbal & Focaccia) are
grouped into a "Golden Boot Bakeries" brand, and the two cafes into "Full-Time Coffee Co.", so
`/brands` has real multi-location demo data — including two extra reviews across the bakery
branches, so its combined rating isn't empty. Stadionul de Gusturi also comes with a pre-generated
webhook API key (`DbSeeder.DemoWebhookApiKey` in the source) already set, so `POST
/api/webhooks/packages` can be tried immediately without generating a key from `/dashboard`
first — see [Business Manager → POS/inventory webhook](USER_GUIDE.md#keep-an-eye-on-things) in
the user guide for the request shape.

## Running tests

**Backend** — `Backend/NetromEcoMeal.Tests` is a separate xUnit project (unit tests for
`OrderService`'s status-transition/stock logic, `CheckoutService`'s Stripe checkout bridge,
`RescueCircleService`'s split-payment orchestration, API integration tests via
`WebApplicationFactory<Program>`, and an architecture test guarding the BusinessLogic/
DataAccess layering), plus integration tests that run the real migrations + `DbSeeder`
against a Postgres container via Testcontainers. Requires Docker to be running locally:

```bash
cd Backend
dotnet test
```

**Frontend** — Vitest covers contexts, hooks and utils:

```bash
cd Frontend/eco-meal-frontend
npx vitest run
```

**End-to-end** — a Playwright suite under `e2e/` covers one golden path per role
(anonymous, customer, business manager, admin) plus visual regression, written against
user-visible selectors only (`getByRole`, `getByLabel`, `getByText`):

```bash
cd e2e
BASE_URL=http://localhost:5173 npx playwright test   # against a locally-running frontend
```

See [e2e/README.md](e2e/README.md) for the suite's setup and design notes.
