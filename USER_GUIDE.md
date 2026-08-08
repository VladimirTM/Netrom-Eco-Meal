# User Guide — Netrom Eco Meal

A walkthrough of how to actually use the app, day to day, for each of the three roles.
For *what each role can do* at a glance, see the [README](README.md#what-each-role-can-do);
this doc is the step-by-step version. Three demo accounts (`demo.customer@ecomeal.local`,
`demo.manager@ecomeal.local`, `demo.manager2@ecomeal.local`, all `Demo123!`) already exist
if you're following along on a fresh `docker compose up` — see
[Seed data](README.md#seed-data).

---

## Customer

### Create an account

Click **Create account** on the home page, or go to `/account/register`. Every
self-registered account starts as a Customer — there's no signup option for the other two
roles; an Admin has to promote you later (see [Admin](#admin) below). If the app has
`Identity:RequireConfirmedAccount` turned on (it is by default under Docker), check your
email — or [Mailpit](README.md#email) at `http://localhost:8025` in the Docker setup — for a
confirmation link before you can sign in.

### Find something to rescue

The home page (`/`) is a live browse of every kitchen with something available right now:

- **Search** matches kitchen name/description *and* what's actually in their live
  packages — searching "bread" surfaces a bakery even if the word "bread" is nowhere on its
  profile.
- **Filter** by kitchen type (Restaurant, Bakery, Cafe, Grocery Store, Food Truck).
- **Sort** by name, "closing soon" (nearest pickup window first), or "near me" — the last
  one asks your browser for location permission, then sorts by real distance and shows a
  "X km away" badge on every card.
- **Map view** swaps the grid for a Leaflet map with a pin per kitchen that has a saved
  location — click a pin's popup to go straight to that kitchen.
- **Favorites** — tap the heart on any kitchen card (or on its detail page) to follow it;
  the "Favorites" toggle filters the browse list down to just the kitchens you follow.

### Order

Open a kitchen to see what's live — each package shows its price, pickup window, dietary
tags, and how many are left. Click **Add to basket**. A basket can only hold packages from
one kitchen at a time; adding from a different one asks to start a new basket instead of
mixing the two. Open the basket (the icon in the header) to adjust quantities or remove a
line, then **Pay & place order**.

That sends you to a real Stripe Checkout page (test-mode, unless the deployment has a live
key configured — see [Payments](README.md#payments)). Use Stripe's standard test card
`4242 4242 4242 4242`, any future expiry, any CVC, to pay. You're redirected back to the app
once payment goes through, with your order number and a "kg of food saved" figure — nothing
is charged and no order exists until this point, so backing out of Stripe's page or closing
the tab just cancels the attempt with nothing lost.

### Track and pick up

`/orders` lists everything you've ever ordered, with filter chips for each status
(Pending/Confirmed/Completed/NoShow/Cancelled) and a lifetime stats header (orders placed,
portions rescued, kitchens visited, kg saved).

- **Pending** orders can be cancelled — the kitchen hasn't confirmed yet, so cancelling
  gets you an automatic refund.
- **Confirmed** orders show a **Show QR code** link — that's your pickup pass
  (`/orders/pickup/{id}`). Hand your phone to the counter at pickup time; the business scans
  it to mark your order Completed. Confirmed orders can also still be cancelled (also
  refunded) if your plans change.
- **Completed**/**Cancelled** orders show an **Order again** button, which re-adds whatever's
  still live and in stock from that order straight into your basket.
- **NoShow** means a Confirmed order's pickup window closed without you collecting it — the
  payment is *not* refunded in that case, since the kitchen held the food for you.

### Everything else

- The **bell icon** in the header is your notification feed — order confirmations,
  cancellations, pickup reminders, and back-in-stock alerts on kitchens you favorite.
- Once you've completed at least one order with a kitchen, its detail page lets you leave a
  **star rating and comment**. Submitting again updates your existing review instead of
  adding a second one.

---

## Business Manager

You can't self-register as a manager — an Admin has to both promote your account to
BusinessManager and staff you to at least one business (see [Admin](#admin)). Once that's
done, sign in and every page below appears in the sidebar.

### Pick which business you're managing

If you're staffed to more than one business, a **switcher** appears at the top of the
sidebar — everything below (packages, orders, dashboard, payments) is scoped to whichever
one is currently selected there. Staffed to just one? The switcher doesn't show up at all;
there's nothing to switch between.

### Manage what's available

`/packages` lists your current business's packages — **Add Package** to create one (name,
description, type, price, quantity, weight in kg, dietary/allergen tags, pickup window,
optional image). Tick **Repeat this every day** while creating one to turn it into a
recurring template instead of a one-off: from then on, `PackageTemplateGenerationService`
auto-generates a fresh instance of it every day, so you don't have to hand-recreate the same
package each morning. Manage existing templates — pause, resume, or stop repeating — on
`/packages/templates`; a 🔁 "Daily" badge marks any package on `/packages` that a template is
currently generating.

### Handle incoming orders

`/orders/manage` is your order queue, filterable by status or by order number/customer name.
Each Pending order shows **Confirm** (moves it to Confirmed and reserves the stock) and
**Cancel** (refunds the customer). Each Confirmed order shows **Complete**, **No-show**, and
**Cancel** — No-show only becomes clickable once that order's pickup window has actually
passed. Export a date range as CSV with the button next to the filters, for your own
bookkeeping.

### Confirm pickup with a QR scan

`/orders/scan` opens your device's camera (tap **Start scanning** — it won't prompt for
camera access until you do). Point it at the customer's pickup pass; a successful scan takes
you straight to a confirmation screen for that order. This needs `https://` or `localhost` to
get camera access at all — scanning won't work if you're opening the app over a LAN IP from
a phone (see [Running with Docker](README.md#running-with-docker)).

### Keep an eye on things

`/dashboard` shows package/order counts and a 14-day orders/kg-saved trend chart, scoped to
your currently selected business. `/payments` is the same idea for money: every order's
payment status (Paid/Refunded/Unpaid) plus "collected" and "refunded" totals for the page
you're looking at.

---

## Admin

Everything a BusinessManager can do, for *any* business, plus the platform-management pages
below. There's no self-registration path to Admin either — the very first one comes from
`SeedAdmin:Email`/`SeedAdmin:Password` (see [Running locally](README.md#running-locally)); after
that, an existing Admin promotes further accounts via `/users`.

### Onboard a business

`/businesses` is admin-only for creating new ones — **Add Business** takes a name,
description, address, type, optional image, and an optional latitude/longitude (there's a
"use my location" button, or type coordinates by hand; leave it blank and that business just
won't appear in "near me" sorting or the map view). Every row also has a **Staff** column —
add any BusinessManager account as staff with the `+` dropdown, or remove them with the `×`
on their chip. A business can have several staff, and the same manager can staff more than
one business.

### Manage packages and orders anywhere

`/packages` and `/orders/manage` both gain an **All businesses** dropdown filter for admins
instead of being locked to one currently-selected business — pick a specific business or
leave it on "All" to see everything at once. Order actions (Confirm/Complete/Cancel/No-show)
work exactly the same way here as they do for a BusinessManager.

### Manage people

`/users` lists every account with its role and (for BusinessManager rows) the businesses
they staff. Change a role from the dropdown next to it — **you can't demote your own
account**, and demoting the platform's very last remaining Admin is blocked outright, so
there's always at least one way back in. Moving someone *off* BusinessManager automatically
releases every business they were staffed to. The **Businesses** column here does the exact
same staff-assignment as `/businesses`' Staff column, just organized by person instead of by
business — add or remove businesses per user with the same chip-and-dropdown UI.

### Platform-wide visibility

`/dashboard` and `/payments` show store-wide totals for an Admin instead of being scoped to
one business — every order, every package, every payment collected or refunded, across every
kitchen on the platform.
