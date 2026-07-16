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
- Leave a star rating and comment on a business once you've ordered from it

**BusinessManager** — assigned to one business by an Admin, scoped to it everywhere:

- Manage their business's packages on `/packages`
- Confirm, complete or cancel orders placed at their business on `/orders/manage`
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
running if you don't want the default admin credentials.

## Seed data

On first run (and on every subsequent startup) `DbSeeder` makes sure the reference data
(roles, business types, package types, order statuses) and a set of World Cup–themed demo
businesses/packages in Timișoara exist. It's safe to re-run: it only fills in what's
missing and refreshes expired pickup windows or stale placeholder images, it never
touches data you've added or customized through the app.
