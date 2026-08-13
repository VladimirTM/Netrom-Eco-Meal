# Netrom Eco Meal

A Blazor Server app for rescuing surplus food. Restaurants, bakeries, cafes, grocery
stores and food trucks list surplus packages (surprise bags, meal boxes, bread bags...)
at a discount, and customers browse, order and pick them up before they'd otherwise go
to waste.

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
- Check out via Stripe Checkout, and track past orders with pickup windows on `/orders`
- Show a QR pickup pass for a confirmed order, scanned by the business at collection —
  split a group order into several separate passes so whoever gets there first can scan
- Leave a star rating and comment on a business (optionally tagged to a specific package)
  once you've ordered from it
- Report a business or package that looks wrong, and apply to list your own business for
  admin review on `/businesses/apply`
- Opt in to the `/impact` community leaderboard, ranking the month's top food-rescuers by kg
  saved — off by default, and a customer who never opts in never appears on it

**BusinessManager** — staff of one or more businesses (assigned by an Admin), scoped to
whichever one they pick in the sidebar switcher:

- Manage packages on `/packages` for the currently selected business — including a photo
  (upload or paste a URL), "repeat this every day" recurring templates managed on
  `/packages/templates`, and bulk duplicate/adjust-quantity/extend-pickup-window actions
- Set your business's weekly opening hours and one-off holiday closures, and upload a
  business photo, from the business edit page
- Confirm, complete or cancel orders placed at the currently selected business on
  `/orders/manage` — cancelling automatically refunds the customer's Stripe payment
- Scan a customer's pickup QR code on `/orders/scan` to confirm pickup
- See stats scoped to the currently selected business on `/dashboard` (including a
  sell-through rate and busiest pickup hours), a payout ledger of every payment collected
  (and refunded) on `/payments`, and export order history as CSV
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
- Add, rename or remove kitchen and package types on `/types` — no code change or
  migration needed for a new category, and a type still in use can't be deleted
- See who did what — role changes, business create/edit/delete/staffing, approvals,
  moderation — on `/audit-log`

## Stack

- ASP.NET Core 10 / Blazor Server (interactive server render mode)
- EF Core + PostgreSQL (Npgsql)
- ASP.NET Identity for auth/roles
- Serilog for structured logging (console sink, per-request logging, config-driven levels)
- QRCoder for server-side pickup QR generation, jsQR (vendored) for client-side camera scanning
- Leaflet + OpenStreetMap tiles (CDN, no API key) for the home page's map view
- Stripe Checkout (`Stripe.net`) for payment
- Web Push (`WebPush`, VAPID-signed) + a service worker/PWA manifest for browser push notifications

## Running locally

You need .NET 10 and a Postgres instance. Configure the connection string and seed admin
credentials with user secrets rather than committing them to `appsettings.json`:

```bash
dotnet user-secrets set "ConnectionStrings:EcoMealContext" "Host=localhost;Port=5432;Database=EcoMeal;Username=postgres;Password=yourpassword"
dotnet user-secrets set "SeedAdmin:Email" "admin@ecomeal.local"
dotnet user-secrets set "SeedAdmin:Password" "Admin123!"
```

Then just run it:

```bash
dotnet run
```

Migrations and seed data (roles, business/package types, the demo Timișoara businesses
and packages, and the admin account) run automatically on startup — no separate migrate
step needed.

No SMTP server is required to run the app: emails (order updates, back-in-stock alerts,
account confirmation/password reset) are just logged instead of sent when `Email:Smtp:Host`
isn't configured. See [Email](#email) below to wire up a real sender or a local catcher.

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

`App:BaseUrl` (used to build links in confirmation/reset emails) already defaults to
`http://localhost:5116` via `appsettings.Development.json`, matching the `dotnet run`
dev port — only override it with `dotnet user-secrets set "App:BaseUrl" "..."` if you're
running on a different port or URL.

Leave `Email:Smtp:Host` unset (the default) and every email is logged instead of sent —
handy for local dev without a real mailbox. `docker-compose.test.yml` instead points it at
a bundled [Mailpit](https://github.com/axllent/mailpit) container, so emails are visible in
a real inbox UI without any external service — see [Running with Docker](#running-with-docker).

By default, self-registered accounts sign in immediately (no confirmation required), the
same as before this feature existed. Set `Identity:RequireConfirmedAccount` to `true` to
require clicking an emailed confirmation link before sign-in works — this needs
`Email:Smtp:Host` configured to actually deliver that link. Password reset
(`/account/forgot-password`) works either way, regardless of that flag.

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
plus a note in the customer's cancellation email.

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

## Running with Docker

`docker-compose.test.yml` spins up Postgres and the app together, which is the easiest
way to try the app without installing a local Postgres. It's meant for local
testing/demoing, not for production (fixed DB password, HTTP only).

```bash
docker compose -f docker-compose.test.yml up --build
```

The app comes up on **http://localhost:8081**, backed by a Postgres container on port
5433 (so it doesn't clash with a Postgres you might already have running locally on
5432). Data persists in the `ecomeal-test-db` volume across restarts — tear it down with
`docker compose -f docker-compose.test.yml down -v` if you want a clean slate. Manager-uploaded
package/business photos persist the same way, in a separate `ecomeal-test-uploads` volume.

A seeded admin account is created automatically:

- **Email:** admin@ecomeal.local
- **Password:** Admin123!

Change `SeedAdmin__Email` / `SeedAdmin__Password` in `docker-compose.test.yml` before
running if you don't want the default admin credentials. Three demo accounts (see
[Seed data](#seed-data) below) are also created regardless of that setting, so you can log
in as a customer or business manager and see the app already in use.

This compose file also runs a [Mailpit](https://github.com/axllent/mailpit) container and
points the app's SMTP settings at it, so every email the app sends (order updates,
back-in-stock alerts, account confirmation, password reset) is visible at
**http://localhost:8025** instead of going nowhere. It also sets
`Identity__RequireConfirmedAccount=true`, so a freshly self-registered account needs its
confirmation link (check Mailpit) clicked before it can sign in — the two seeded demo
accounts are unaffected, since they're created pre-confirmed.

`Stripe__SecretKey` is empty by default here too, so checkout shows the same "payments
aren't configured yet" error until you set a real test-mode key — see [Payments](#payments)
above. The safe way to do that locally is a gitignored `docker-compose.override.yml` next
to this file (never edit `docker-compose.test.yml` itself — it's tracked, so a real key
typed directly into it risks getting committed):
```yaml
services:
  app:
    environment:
      Stripe__SecretKey: "sk_test_..."
```
then run `docker compose -f docker-compose.test.yml -f docker-compose.override.yml up --build`
— Compose merges the two, and the key never touches a tracked file. An environment variable
works too; either way, no live/production Stripe key is ever needed to try the app.

The pickup QR scanner (`/orders/scan`) uses the device camera, which browsers only allow
over HTTPS or on `localhost`. It works fine when you open the app as `localhost:8081`,
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
  having to split one yourself first.
- **BusinessManager** — demo.manager@ecomeal.local / Demo123! — staffs both Stadionul de
  Gusturi and VAR Bistro, so the sidebar's business switcher has something to switch
  between out of the box. Has a pending order waiting to be confirmed on `/orders/manage`
  and enough order history for `/dashboard`'s trend chart, `/payments`'s ledger, and CSV
  export to be worth looking at. Also has a handful of already-closed packages with
  partial completed sales spread across several days/hours, purely so `/dashboard`'s
  Business Analytics card (sell-through rate, busiest pickup hours) has real history to
  show instead of an empty state.
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
submitted by the demo customer), one existing demo business and one existing demo package
marked hidden with a reason, four `Report`s (open, dismissed, and two actioned), and an
audit-log history consistent with all of it.

## Running tests

`Tests/Netrom-Eco-Meal.Tests.csproj` is a separate xUnit project (unit tests for
`OrderService`'s status-transition/stock logic and `CheckoutService`'s Stripe checkout
bridge, plus integration tests that run the real migrations + `DbSeeder` against a
Postgres container via Testcontainers). Requires Docker to be running locally:

```bash
dotnet test
```
