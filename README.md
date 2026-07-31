# Netrom Eco Meal

A Blazor Server app for rescuing surplus food. Restaurants, bakeries, cafes, grocery
stores and food trucks list surplus packages (surprise bags, meal boxes, bread bags...)
at a discount, and customers browse, order and pick them up before they'd otherwise go
to waste.

Three roles: **Customer** (browses, orders, leaves reviews), **BusinessManager** (manages
their business's packages and orders) and **Admin** (manages businesses, types and users).

## What each role can do

**Customer** — what you get on self-registration:

- Browse, search and filter businesses on the home page
- View a business's live packages and add them to a basket
- Check out and track past orders with pickup windows on `/orders`
- Show a QR pickup pass for a confirmed order, scanned by the business at collection
- Leave a star rating and comment on a business once you've ordered from it

**BusinessManager** — assigned to one business by an Admin, scoped to it everywhere:

- Manage their business's packages on `/packages`
- Confirm, complete or cancel orders placed at their business on `/orders/manage`
- Scan a customer's pickup QR code on `/orders/scan` to confirm pickup
- See business-scoped stats on `/dashboard`

**Admin** — full access, plus the only role that can create businesses:

- Create and edit any business on `/businesses`, including assigning it a manager
- Manage packages for any business on `/packages`
- Review and manage orders across every business on `/orders/manage`
- Promote or demote users between Customer, BusinessManager and Admin on `/users`
- See store-wide stats on `/dashboard`

## Stack

- ASP.NET Core 10 / Blazor Server (interactive server render mode)
- EF Core + PostgreSQL (Npgsql)
- ASP.NET Identity for auth/roles
- QRCoder for server-side pickup QR generation, jsQR (vendored) for client-side camera scanning

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
dotnet user-secrets set "App:BaseUrl" "http://localhost:5000"   # used to build links in emails
```

Leave `Email:Smtp:Host` unset (the default) and every email is logged instead of sent —
handy for local dev without a real mailbox. `docker-compose.test.yml` instead points it at
a bundled [Mailpit](https://github.com/axllent/mailpit) container, so emails are visible in
a real inbox UI without any external service — see [Running with Docker](#running-with-docker).

By default, self-registered accounts sign in immediately (no confirmation required), the
same as before this feature existed. Set `Identity:RequireConfirmedAccount` to `true` to
require clicking an emailed confirmation link before sign-in works — this needs
`Email:Smtp:Host` configured to actually deliver that link. Password reset
(`/account/forgot-password`) works either way, regardless of that flag.

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
`docker compose -f docker-compose.test.yml down -v` if you want a clean slate.

A seeded admin account is created automatically:

- **Email:** admin@ecomeal.local
- **Password:** Admin123!

Change `SeedAdmin__Email` / `SeedAdmin__Password` in `docker-compose.test.yml` before
running if you don't want the default admin credentials. Two demo accounts (see
[Seed data](#seed-data) below) are also created regardless of that setting, so you can log
in as a customer or business manager and see the app already in use.

This compose file also runs a [Mailpit](https://github.com/axllent/mailpit) container and
points the app's SMTP settings at it, so every email the app sends (order updates,
back-in-stock alerts, account confirmation, password reset) is visible at
**http://localhost:8025** instead of going nowhere. It also sets
`Identity__RequireConfirmedAccount=true`, so a freshly self-registered account needs its
confirmation link (check Mailpit) clicked before it can sign in — the two seeded demo
accounts are unaffected, since they're created pre-confirmed.

The pickup QR scanner (`/orders/scan`) uses the device camera, which browsers only allow
over HTTPS or on `localhost`. It works fine when you open the app as `localhost:8081`,
but won't get camera access if you open it via a LAN IP from another device (e.g. testing
on a phone) — that needs a real HTTPS deployment.

## Seed data

On first run (and on every subsequent startup) `DbSeeder` makes sure the reference data
(roles, business types, package types, order statuses) and a set of World Cup–themed demo
businesses/packages in Timișoara exist. It's safe to re-run: it only fills in what's
missing and refreshes expired pickup windows or stale placeholder images, it never
touches data you've added or customized through the app.

It also creates two demo accounts, so every feature has real data to look at right away
instead of an empty app:

- **Customer** — demo.customer@ecomeal.local / Demo123! — has past orders in every status
  (completed, confirmed, cancelled, no-show, pending) across several businesses, so
  `/orders`, reorder, the QR pickup pass, favorites and reviews all show something real.
- **BusinessManager** — demo.manager@ecomeal.local / Demo123! — assigned to Stadionul de
  Gusturi, with a pending order waiting to be confirmed on `/orders/manage` and enough
  order history for `/dashboard`'s trend chart and CSV export to be worth looking at.

This activity is only ever seeded once, the first time the app starts against a genuinely
empty database — unlike the reference/demo-catalog data above, it won't touch orders
placed for real afterward.

## Running tests

`Tests/Netrom-Eco-Meal.Tests.csproj` is a separate xUnit project (unit tests for
`OrderService`'s status-transition/stock logic, plus integration tests that run the real
migrations + `DbSeeder` against a Postgres container via Testcontainers). Requires Docker
to be running locally:

```bash
dotnet test
```
