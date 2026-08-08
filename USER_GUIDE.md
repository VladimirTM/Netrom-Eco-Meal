# User Guide — Netrom Eco Meal

A detailed walkthrough of how to actually use the app, day to day, for each of the three
roles — what every button does, what the field validation rules are, and what happens in the
less obvious cases (a sold-out package, a denied camera permission, a business with no saved
location). For *what each role can do* at a glance instead, see the
[README](README.md#what-each-role-can-do); this doc is the exhaustive version. Three demo
accounts (`demo.customer@ecomeal.local`, `demo.manager@ecomeal.local`,
`demo.manager2@ecomeal.local`, all `Demo123!`) already exist if you're following along on a
fresh `docker compose up` — see [Seed data](README.md#seed-data).

---

## Customer

### Create an account

Click **Create account** on the home page, or go to `/account/register`. The form asks for
full name, email, and a password of at least 8 characters — every self-registered account
starts as a Customer, there's no signup option for the other two roles; an Admin has to
promote you later (see [Manage people](#manage-people) below).

If the app has `Identity:RequireConfirmedAccount` turned on (it is by default under Docker),
you can't sign in until you click the confirmation link — check your inbox, or
[Mailpit](README.md#email) at `http://localhost:8025` in the Docker setup, which catches every
outgoing email locally instead of sending it. The confirmation and password-reset emails share
one branded template with a single call-to-action button ("Confirm my account" /
"Reset my password"); the reset link doesn't reveal whether the email address exists, so it's
safe against account enumeration.

### Browse and find something to rescue

The home page (`/`) is a live browse of every kitchen with something available right now. The
hero shows four running platform stats: packages live, portions to save, kitchens on board,
and total kg of food saved to date.

- **Search** (debounced 300ms as you type) matches kitchen name/description *and* what's
  actually in their live packages — searching "bread" surfaces a bakery even if the word
  "bread" is nowhere on its profile.
- **Filter** by kitchen type via a dropdown ("All kitchen types" plus one option per type:
  Restaurant, Bakery, Cafe, Grocery Store, Food Truck).
- **Sort** by "Name (A–Z)", "Closing soon" (nearest pickup window first), or "Nearest" — the
  last option only appears in the dropdown once you've used **Near me**.
- **Near me** button asks your browser for location permission, then sorts by real distance
  and switches every card to show a distance badge ("123 m away" under 1 km, otherwise
  "2.3 km away"). If permission is denied or location can't be resolved, you'll see: *"Couldn't
  get your location — check your browser's location permission."* Clicking it again while
  already sorted by distance toggles back to Name (A–Z).
- **Map view** (toggle next to List view) swaps the grid for a Leaflet map with a pin per
  kitchen that has a saved location — click a pin's popup to go straight to that kitchen. If no
  kitchen has a saved location yet, the map shows: *"No kitchens have a saved location yet."*
- **Favorites** toggle (Customers only) filters the list down to just the kitchens you've
  hearted. A heart icon also sits on every business card and on the kitchen's own detail page —
  clicking it toggles instantly, no confirmation needed.
- A **Clear** button appears next to the controls whenever any search/filter/sort/favorites
  setting is non-default, to reset everything in one click.
- Below the controls, a result count reads "N kitchen(s) found". If there are genuinely no
  kitchens on the platform yet you'll see *"No kitchens have joined yet — check back soon."*;
  if your filters just happen to match nothing, it's *"Nothing matches your filters yet."*
  instead. Results are paginated, 9 kitchens per page.

Each card shows a live-count badge ("N live", green dot) if the kitchen has anything available
right now, a star rating badge if it has reviews, and either "from $X.XX" (its cheapest live
package) or "Nothing live now" if it's temporarily empty.

### View a kitchen and order

Opening a kitchen shows its full profile — description, address, star rating — and every
currently-live package (already sorted soonest-closing first), each with its type, pickup
window, description, price, dietary/allergen tag pills, and how many are left after everyone
else's pending reservations are accounted for.

Click a package (or **Add** directly from the list) to open its detail view. If a package has
sold out since the page loaded, the button reads **Sold out** and is disabled instead of
letting you add it. Otherwise, **Add to basket** adds one unit.

A basket can only hold packages from **one kitchen at a time** — adding from a different
kitchen than what's already in your basket opens a confirmation dialog ("Start a new basket?")
warning that continuing will clear your existing basket first. Cancelling that dialog keeps
your current basket untouched.

### Manage your basket

Open the basket panel from the header icon. Each line shows the package name, its per-unit
price, a `-`/`+` quantity stepper (the `+` button disables itself once you hit the package's
remaining stock, so you can't reserve more than actually exists), and a remove button. The
footer shows a running total and a **Pay & place order** button.

If checkout can't start — you're not signed in as a Customer, something in your basket sold
out in the meantime, or Stripe isn't configured on this deployment — you'll see an inline error
instead of being redirected.

### Checkout with Stripe

**Pay & place order** sends you to a real Stripe Checkout page (test-mode, unless the
deployment has a live key configured — see [Payments](README.md#payments)). Use Stripe's
standard test card `4242 4242 4242 4242`, any future expiry, any CVC, to pay. **No order exists
in the app until Stripe confirms the payment** — a short-lived hold bridges the redirect, so
backing out of Stripe's page or just closing the tab cancels the attempt cleanly with nothing
charged and nothing left behind. Once payment succeeds, you're redirected back with your order
number and a "kg of food saved" figure for that order.

### Track and pick up

`/orders` lists everything you've ever ordered, with a lifetime stats header (orders placed,
portions rescued, kitchens visited, kg saved) and filter chips for each status: All, Pending,
Confirmed, Completed, NoShow, Cancelled. Each order renders as a ticket card — click anywhere
on it to open the full detail view, including a payment badge ("Paid" or "Refunded") when
applicable and the pickup window formatted as e.g. "Aug 8 · 14:00–16:00" (or spanning two dates
if the window crosses midnight).

- **Pending** orders can be cancelled — the kitchen hasn't confirmed yet, so cancelling issues
  an automatic refund. A confirmation dialog ("Cancel order?") double-checks before it's final.
- **Confirmed** orders show a **Show QR code** link to your pickup pass, and can also still be
  cancelled (also refunded) if your plans change.
- **Completed**, **Cancelled**, and **NoShow** orders show an **Order again** button instead —
  it re-adds whatever's still live and in stock from that order straight into your basket. If
  none of the original items are available anymore, you'll see *"None of the items from this
  order are available right now."* instead of an empty add. If your basket currently holds
  items from a different kitchen, the same "start a new basket?" confirmation from checkout
  applies here too.
- **NoShow** means a Confirmed order's pickup window closed without you collecting it — unlike
  a cancellation, **the payment is not refunded** in that case, since the kitchen held the food
  for you and the kept charge is the no-show fee.

Order history is paginated, 10 per page. Every status transition above also fires an email (the
same branded template as account emails) alongside the in-app notification, so you don't have
to keep the tab open to know your order was confirmed, is about to close, or was marked as a
no-show.

### Show your pickup pass

`/orders/pickup/{id}` is only populated for **Confirmed** orders — for any other status it
explains why there's nothing to show (e.g. *"Your pickup pass appears once the business
confirms this order."* for a still-Pending one, or *"This order has already been picked
up."* for a Completed one). When it is available, the pass is a ticket: kitchen name and
address, every item and its price, the pickup window, an estimated kg-of-food-saved teaser, and
a QR code stub with the hint *"Hand your phone to the counter — they'll scan this to confirm
pickup."* Scanning it (see [Confirm pickup with a QR scan](#confirm-pickup-with-a-qr-scan))
is what actually moves the order to Completed.

### Leave a review

Once you've completed at least one order with a kitchen, its detail page shows a review form
in place of the "order first" hint. Pick a star rating (required) and an optional comment (up
to 600 characters), then **Submit review**. Submitting again after you already have one for
that kitchen updates it in place — the button relabels to **Update review** and there's no way
to end up with two.

### Notifications

The **bell icon** in the header shows an unread-count badge (capped at "9+") that refreshes
every 30 seconds even while the dropdown is closed. Opening it lists your recent notifications
with relative timestamps ("just now", "5m ago", "2h ago", "3d ago"); a **Mark all read** button
appears whenever you have unread ones. Clicking any notification marks it read and navigates
you straight to the relevant order or kitchen. This covers order confirmations/cancellations/
no-shows, pickup reminders shortly before a window closes, and back-in-stock alerts for
kitchens you've favorited — each with a matching email sent in parallel.

---

## Business Manager

You can't self-register as a manager — an Admin has to both promote your account to
BusinessManager and staff you to at least one business (see [Manage people](#manage-people)).
Once that's done, sign in and every page below appears in the sidebar.

### Pick which business you're managing

If you're staffed to more than one business, a **switcher** appears at the top of the sidebar —
everything below (packages, orders, dashboard, payments) is scoped to whichever one is
currently selected there. Staffed to just one? The switcher doesn't show up at all; there's
nothing to switch between.

### Manage what's available

`/packages` lists your current business's packages. **Add Package** opens a form for:

- **Name** and **Description** (both required)
- **Business** (locked to yours unless you're an Admin) and **Type**
- **Price** in RON, must be greater than 0 and no more than 10,000
- **Quantity**, an integer from 1 to 1,000
- **Weight (kg)**, greater than 0 and no more than 100 — this is what feeds every "kg saved"
  figure customers see
- **Dietary & allergen tags** (optional, multi-select checkboxes): Vegetarian, Vegan,
  Gluten-Free, Dairy-Free, Halal, Contains Nuts, Contains Gluten, Contains Dairy — the
  "Contains X" ones are styled as allergen warnings rather than dietary preferences
- **Pickup Start** / **Pickup End** (date-time pickers, shown and entered in your own local
  time zone) — pickup end must be after pickup start and must still be in the future; when
  *editing* a package that's already mid-window, only the end time is checked, so an
  in-progress pickup window doesn't block your edit
- **Image URL** (optional)

Ticking **Repeat this every day** while *creating* a package (not available when editing)
turns it into a recurring template instead of a one-off: a background sweep auto-generates a
fresh instance of it — same name, price, and pickup window — every day going forward, so you
don't have to hand-recreate the same package each morning. A 🔁 "Daily" badge on `/packages`
marks any package a template is currently generating.

### Manage recurring templates

`/packages/templates` lists every template for the current business: name and description,
its daily pickup window, quantity per day, when it last generated an instance (or "—" if it
hasn't yet), and a status pill — **Active** (green) or **Paused** (gray). Each row has a
**Pause**/**Resume** toggle (the label flips depending on current state) and a delete button.
Deleting asks first: *"Stop repeating? \"{Name}\" will stop generating new daily packages.
Instances already created stay as they are."* — so deleting a template never retroactively
removes packages that already exist.

### Handle incoming orders

`/orders/manage` is your order queue — searchable by order number or customer name, and
filterable by status (All, Pending, Confirmed, Completed, NoShow, Cancelled). Each row shows
the order number, customer, line items, total, pickup window, status, and payment badge
("Paid"/"Refunded", blank if unpaid yet).

Action buttons follow the same state machine customers see from the other side:

- **Pending** → **Confirm** (moves it to Confirmed and reserves the stock) or **Cancel**
  (refunds the customer).
- **Confirmed** → **Complete**, **No-show**, or **Cancel**. **No-show only becomes clickable
  once that order's pickup window has actually passed** — you can't mark someone a no-show
  early.
- Any other status shows no actions.

Export a date range as CSV with the **Export CSV** button next to the filters (pick "Export
from"/"Export to" dates first) — handy for your own bookkeeping outside the app. The queue is
paginated, 10 per page.

### Confirm pickup with a QR scan

`/orders/scan` starts idle with a single **Start scanning** button — the camera isn't touched,
and no permission prompt appears, until you tap it. This needs `https://` or `localhost` to get
camera access at all (a browser platform requirement, not an app one) — scanning won't work if
you're opening the app over a bare LAN IP from a phone (see
[Running with Docker](README.md#running-with-docker)). If the browser denies camera
permission, you'll see: *"Camera access was denied. Allow camera access for this site in your
browser settings and try again."*; any other camera failure shows *"Couldn't start the camera.
Make sure this device has one and try again."* Once scanning, point the camera at the
customer's pickup pass — a successful decode takes you straight to that order's confirmation
screen.

### Keep an eye on things

`/dashboard` shows package and order counts for your currently selected business, plus a
"Last 14 days" trend chart with two rows — daily order count and daily kg saved — each bar
carrying a hover/focus tooltip with the exact date and value, and today's bar visually
highlighted. If you don't currently manage a business, it explains that instead of showing an
empty chart.

`/payments` is the money version, scoped the same way: every order for your business with its
payment status (Unpaid/Paid/Refunded), amount, and paid/refunded timestamps, plus two totals —
"Collected" and "Refunded" — for whatever page of results you're currently looking at (not a
platform-wide total).

---

## Admin

Everything a BusinessManager can do, for *any* business, plus the platform-management pages
below. There's no self-registration path to Admin either — the very first one comes from
`SeedAdmin:Email`/`SeedAdmin:Password` (see [Running locally](README.md#running-locally)); after
that, an existing Admin promotes further accounts via `/users`.

### Onboard a business

`/businesses` is admin-only for creating new ones. **Add Business** takes:

- **Name**, **Description**, **Address** (all required)
- **Type** (dropdown)
- **Image URL** (optional)
- **Location** (optional — powers "near me" sort and the map view): latitude and longitude
  fields you can type by hand, or a **geo-pin button** next to them that asks your browser for
  its current position and fills both fields in automatically. If location can't be resolved,
  you'll see the same *"Couldn't get your location — check your browser's location
  permission."* message as the customer-side "Near me" button. Latitude must be between -90
  and 90, longitude between -180 and 180 — leave both blank and the business simply won't
  appear in "near me" sorting or the map view.

Every row on `/businesses` also has a **Staff** column — add any BusinessManager account as
staff with the `+` dropdown (it stays open so you can add several in one go, and shows "No more
managers to add" once every manager is already staffed), or remove them with the `×` on their
chip. A business can have several staff, and the same manager can staff more than one business.
Deleting a business asks first, warning that it and its full package/order history are
permanently removed.

### Manage packages and orders anywhere

`/packages` and `/orders/manage` both gain an **All businesses** dropdown filter for admins
instead of being locked to one currently-selected business — pick a specific business or leave
it on "All" to see everything at once. Order actions (Confirm/Complete/Cancel/No-show) work
exactly the same way here as they do for a BusinessManager.

### Manage people

`/users` lists every account — searchable by name/email, filterable by role — with its role and
(for BusinessManager rows) the businesses they staff. Each row's role is a colored dropdown
(purple Admin, blue Business Manager, green Customer) with the active role checkmarked; it's
**disabled on your own row** with a tooltip explaining you can't change your own role, and
demoting the platform's very last remaining Admin is blocked outright — so there's always at
least one way back in. Moving someone *off* BusinessManager automatically releases every
business they were staffed to.

The **Businesses** column here does the exact same staff-assignment as `/businesses`' Staff
column, just organized by person instead of by business — add or remove businesses per user
with the same chip-and-dropdown UI, shown only for BusinessManager accounts.

### Platform-wide visibility

`/dashboard` and `/payments` show store-wide totals for an Admin instead of being scoped to one
business — every order, every package, every payment collected or refunded, across every
kitchen on the platform, plus a `Businesses` and `Users` stat tile that only Admins see.
