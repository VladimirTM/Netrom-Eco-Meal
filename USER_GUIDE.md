# User Guide — Netrom Eco Meal

A detailed walkthrough of how to actually use the app, day to day, for each of the three
roles — what every button does, what the field validation rules are, and what happens in the
less obvious cases (a sold-out package, a denied camera permission, a business with no saved
location). For *what each role can do* at a glance instead, see the
[README](README.md#what-each-role-can-do); this doc is the exhaustive version. Five demo
accounts (`demo.customer@ecomeal.local`, `demo.customer2@ecomeal.local`,
`demo.customer3@ecomeal.local`, `demo.manager@ecomeal.local`, `demo.manager2@ecomeal.local`,
all `Demo123!`) already exist if you're following along on a fresh `docker compose up` — see
[Seed data](README.md#seed-data).

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

Once signed in, the gear icon in the header (every role has one) opens `/account/settings` —
update your display name, or change your password by entering the current one plus a new one
twice. Both are separate forms, saved independently.

### Browse and find something to rescue

The home page (`/`) is a live browse of every kitchen with something available right now. The
hero shows four running platform stats: packages live, portions to save, kitchens on board,
and total kg of food saved to date.

- **Ask AI** — a second search bar above the plain one where you can just describe what you
  want in your own words, e.g. *"vegan dinner under 30 lei, closing soon"* or *"gluten-free
  near me"*. It fills in the filters/sort below for you (and shows a dismissable "Under X RON"
  chip for a price ceiling, since that's not otherwise a dropdown) rather than searching
  separately — you can still edit anything it set by hand afterward. A follow-up like "cheaper"
  or "gluten-free only" refines your last AI search instead of starting over. This is optional
  and needs an AI feature to be configured on the server; if it isn't, you'll see a message
  saying so instead of any filters changing.
- **Search** (debounced 300ms as you type) matches kitchen name/description *and* what's
  actually in their live packages — searching "bread" surfaces a bakery even if the word
  "bread" is nowhere on its profile.
- **Filter** by kitchen type via a dropdown ("All kitchen types" plus one option per type:
  Restaurant, Bakery, Cafe, Grocery Store, Food Truck).
- **Filter** by dietary preference or allergen via a second dropdown ("Any diet/allergen" plus
  every tag from the same list managers pick from when tagging a package — Vegetarian, Vegan,
  Gluten-Free, Dairy-Free, Halal in one group, the "Contains X" allergen warnings in another).
  Only kitchens with at least one live package carrying that tag show up.
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
package) or "Nothing live now" if it's temporarily empty. If the kitchen has set weekly hours
(or a holiday closure) and it's currently outside them, a red **Closed now** badge shows next to
the kitchen type — this only reflects a manager-entered schedule, so a kitchen that hasn't set
hours yet never shows the badge either way.

### View a kitchen and order

Opening a kitchen shows its full profile — description, address, star rating, and an **Open
now**/**Closed now** badge next to the address if the kitchen has set hours — and every
currently-live package (already sorted soonest-closing first), each with its type, pickup
window, description, price, dietary/allergen tag pills, and how many are left after everyone
else's pending reservations are accounted for. A package whose pickup window ends within the
next hour also gets a red **Ends in N min** badge next to its pickup time — the same urgency
the home page's "Closing soon" sort already uses, just visible per package instead of only
affecting order.

If the kitchen has set weekly hours, an **Opening hours** section lists all seven days with
today highlighted; a day with no open/close time shown reads "Closed". If a holiday closure is
active right now, a banner above the hours reads *"Closed for the holidays until [date] — [the
manager's reason, if one was given]."* A closure only affects this indicator and badge — it
doesn't hide the kitchen's live packages or block ordering from them.

The package list updates itself live — if someone else's order or a manager's edit changes
what's available while you're on the page, the "N left" counts and **Sold out** state update on
their own, no refresh needed. Click a package (or **Add** directly from the list) to open its
detail view. A sold-out package shows **Sold out** and is disabled instead of letting you add
it; otherwise, **Add to basket** adds one unit.

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

### Plan a basket with AI

The ✨ icon in the header (next to your orders) opens `/plan-basket` — tell it how many people
you're feeding, a budget in RON, and optionally a dietary/allergen need, and it proposes a
basket of real, currently-live packages with a one-line reason for each pick. Every item starts
pre-approved with a checkbox; uncheck any you don't want before clicking **Add approved to
basket** — nothing touches your actual basket until then, and the running "Approved total" above
that button only counts what's still checked.

Since a basket can only hold packages from one kitchen at a time (see [View a kitchen and
order](#view-a-kitchen-and-order) above), the proposal is always from a single kitchen too — if
your best options would otherwise span more than one, the explanation says so. If nothing live
fits your budget and dietary need, you'll see "Nothing fit that request right now" instead of an
empty-looking error. Like the Ask AI search bar, this needs an AI feature configured on the
server — if it isn't, you'll see a message saying so instead of a proposal.

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
on it to open the full detail view, including a payment badge ("Paid", "Refunded", or — if the
automatic refund itself failed — "Refund failed") when applicable, and the pickup window
formatted as e.g. "Aug 8 · 14:00–16:00" (or spanning two dates if the window crosses midnight).

- **Pending** orders can be cancelled — the kitchen hasn't confirmed yet, so cancelling issues
  an automatic refund. A confirmation dialog ("Cancel order?") double-checks before it's final.
  If the refund itself fails (a Stripe-side error), the order still cancels — you'll see a
  "Refund failed" badge instead of "Refunded", plus a note in the cancellation email that the
  team will follow up.
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

Picking it up as a group? Tap **Splitting with a group? Get separate passes**, choose how many
people are collecting (up to 6), and each gets their own tab with its own QR code — hand a
different one to each person. Whoever scans first completes the whole order for everyone; the
rest just show "already picked up" if scanned afterward. Changing your mind about the count is
free until someone actually redeems a pass.

### Leave a review

Once you've completed at least one order with a kitchen, its detail page shows a review form
in place of the "order first" hint. Pick a star rating (required) and an optional comment (up
to 600 characters), then **Submit review**. Submitting again after you already have one for
that kitchen updates it in place — the button relabels to **Update review** and there's no way
to end up with two.

If you've completed an order for a specific package from that kitchen, a dropdown lets you tag
your review to it ("Whole kitchen" stays the default if you'd rather not). A tagged review shows
a small package-name pill on the review card, and that package's own detail popup then shows its
own star rating and review count separately from the kitchen's overall rating.

### Report a kitchen or package

If something looks wrong with a kitchen or a specific package — inaccurate info, a stale photo,
a food-safety concern — use the **flag icon** next to the Favorite button on a kitchen's page, or
**Report this package** at the bottom of a package's detail popup. Both open the same small form:
type what's wrong and **Submit report**. There's no status to track afterward — an admin reviews
it and either dismisses it or takes action, and you'll see the effect (the listing disappearing,
for example) rather than a direct reply.

### List your own business

Running a kitchen and want to join the platform? Click the **shop icon** in the header (next to
Dashboard, if you have one) to open `/businesses/apply`. Fill in name, description, address,
type, and an optional image URL, then **Submit for review** — you'll see a confirmation message
immediately, and an admin reviews the application separately; there's no application-status page
to check back on. If it's approved, you'll get a notification and can then be added as staff (an
admin does this) to start managing it; if it isn't, the notification explains why.

### Notifications

The **bell icon** in the header shows an unread-count badge (capped at "9+") that refreshes
every 30 seconds even while the panel is closed. Opening it lists your recent notifications
with relative timestamps ("just now", "5m ago", "2h ago", "3d ago"); a **Mark all read** button
appears whenever you have unread ones. Clicking any notification marks it read and navigates
you straight to the relevant order or kitchen. This covers order confirmations/cancellations/
no-shows, pickup reminders shortly before a window closes, back-in-stock alerts for kitchens
you've favorited, an AI-drafted nudge when a package at a kitchen you favorite or have ordered
from before is closing soon with stock still unclaimed (personalized when it matches something
you've ordered there before — needs the same optional AI feature as Ask AI above), and — if
you've applied to list a business — its approval or rejection, each with a matching email sent
in parallel.

The small **bell icon** next to "Mark all read" turns these into real browser notifications too,
even when the tab isn't open — click it once to grant your browser's notification permission.
Click it again to turn them back off. It only appears if your browser supports it.

### Community impact leaderboard

The **trophy icon** in the header (visible whether or not you're signed in) opens `/impact`, a
running ranking of this month's top rescuers by kg saved. It's opt-in: your name only shows up
if you turn on **Appear on this leaderboard** — a toggle at the top of the page, visible only
when you're signed in as a Customer, off by default. Flip it and your own row appears or
disappears immediately, highlighted with a **You** badge among the ranked list. If nobody's
opted in yet this month, the page just says so instead of showing an empty table.

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

### Set your hours and closures

From `/businesses/edit/{id}` for your business (reachable via **Businesses** in the sidebar),
two sections below the main profile fields let you set the schedule customers see as the
"closed now" badge and hours panel:

- **Opening Hours** — one row per weekday. Tick **Closed** to mark a day fully closed, or set an
  open/close time (a day left with only one of the two, and not ticked Closed, shows an error on
  save: *"Set both an open and close time for [Day], or mark it closed."*). **Save hours** always
  replaces the whole week at once — there's no way to save just one day. Every business starts
  with no hours configured (the badge and hours panel stay hidden for customers until you save
  here for the first time), pre-filled with a 09:00–18:00 default on every day to edit from.
- **Holiday Closures** — an independent list of date ranges (with an optional reason) that
  override your weekly hours while active, e.g. a vacation. Pick a start and end date and
  **Add closure**; each existing closure has its own remove button. A closure doesn't need your
  weekly hours to be set first, and doesn't affect whether your packages can still be ordered —
  it only changes the "closed now" indicator customers see.

Both sections require you to be an Admin or staffed to the business, same as editing its profile
fields above them.

### Manage what's available

`/packages` lists your current business's packages. **Add Package** opens a form for:

- **Name** and **Description** (both required) — once you've typed a name, a **Write it for
  me** button next to the Description label can draft a short customer-facing description from
  the name, type, and any dietary tags you've picked, which you can still edit before saving.
  This is optional and needs an AI feature to be configured on the server; if it isn't, the
  button shows a message saying so instead of a description
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
- **Photo** (optional) — upload a JPG/PNG/WEBP/GIF up to 5 MB, or paste an image URL directly
  into the smaller field underneath if you'd rather link one

Ticking **Repeat this every day** while *creating* a package (not available when editing)
turns it into a recurring template instead of a one-off: a background sweep auto-generates a
fresh instance of it — same name, price, and pickup window — every day going forward, so you
don't have to hand-recreate the same package each morning. A 🔁 "Daily" badge on `/packages`
marks any package a template is currently generating.

**Bulk-select** packages with the checkbox column (each row's checkbox only appears on
packages you're allowed to manage; a header checkbox selects everything selectable on the
current page). Selecting one or more reveals a toolbar above the table:

- **Duplicate** — copies each selected package as a new, independent one (same name, price,
  quantity, tags, and pickup window; not linked to the original's recurring template even if
  it has one). Asks for confirmation first, showing how many will be copied.
- **Adjust quantity** — adds (or, with a negative number, subtracts) the same amount to every
  selected package's stock in one go; never drops below 0.
- **Extend pickup window** — pushes just the pickup *end* time later by however many hours you
  enter, for every selected package; the start time is untouched.

A selection persists as you page through or change filters, so you can build it up across
multiple pages before acting — "Clear selection" or completing an action empties it again.

Any of these — a quantity change, hiding a package, extending its window — shows up live on
that business's page for anyone already browsing it, no refresh needed on their end.

A 🏷️ badge next to the price marks any package closing soon that still has stock left. Clicking
it asks an AI agent for a price-cut suggestion, grounded in how similar past packages at your
business actually sold — you'll see the current price, the suggested price, and a one-sentence
reason. It's only ever a suggestion: nothing changes until you edit the package yourself, and
**Dismiss** hides it for that package going forward. If no cut looks warranted, or the AI feature
isn't configured on the server, you'll see a message saying so instead of a number.

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
("Paid"/"Refunded"/"Refund failed", blank if unpaid yet).

Action buttons follow the same state machine customers see from the other side:

- **Pending** → **Confirm** (moves it to Confirmed and reserves the stock) or **Cancel**
  (refunds the customer).
- **Confirmed** → **Complete**, **No-show**, or **Cancel**. **No-show only becomes clickable
  once that order's pickup window has actually passed** — you can't mark someone a no-show
  early.
- Any other status shows no actions.

**Cancel** always asks for confirmation first ("Cancel order?", same dialog shape as the
customer-side cancel) before actually refunding — a misclick here would otherwise refund a
paying customer with no way to undo it.

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
screen. A Confirmed order split across a group shows a small "👥 N" hint next to its status in
`/orders/manage` — scanning *any* one of that order's passes completes the whole order, so
scanning a second one afterward just shows "This order was already picked up using a different
pass."

No camera, or the code just won't scan? A **manual lookup** below the viewfinder takes an order
number instead — type or paste it and **Find order** lists any Confirmed match (scoped to your
current business, same as everywhere else) with a **Confirm pickup** button, or one button per
pass if that order was split across a group.

### Keep an eye on things

`/dashboard` shows package and order counts for your currently selected business, plus a
"Last 14 days" trend chart with two rows — daily order count and daily kg saved — each bar
carrying a hover/focus tooltip with the exact date and value, and today's bar visually
highlighted. If you don't currently manage a business, it explains that instead of showing an
empty chart.

Below that, a **Business Analytics** card covers packages with a pickup window in the last 14
days:

- **Sell-through rate** — the share of listed stock that actually got picked up, counting only
  packages whose pickup window has already closed (an open package hasn't finished selling
  yet, so its remaining stock isn't "unsold" — just not decided). Shows the percentage, a
  progress bar, and "X of Y listed picked up"; if nothing's closed yet in the period, it says so
  instead of showing a misleading 0%.
- **Busiest pickup hours** — a 24-bar chart of completed pickups bucketed by hour of day (in
  *your* local time), so you can see when foot traffic actually peaks. Hover or focus a bar for
  the exact hour range and count.

`/payments` is the money version, scoped the same way: every order for your business with its
payment status (Unpaid/Paid/Refunded/Refund failed), amount, and paid/refunded timestamps, plus
two totals — "Collected" and "Refunded" — for whatever page of results you're currently looking
at (not a platform-wide total). A third "Refund failed" tile appears next to them whenever the
current page has at least one, so a failed automatic refund can't go unnoticed. Its own
**Export CSV** button (pick "Export from"/"Export to" dates first) exports the payment ledger —
amount, currency, status, paid/refunded timestamps — separately from the order-focused export on
`/orders/manage` above.

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
- **Photo** (optional) — upload a JPG/PNG/WEBP/GIF up to 5 MB, or paste an image URL directly
  into the smaller field underneath if you'd rather link one
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

### Review business applications

Customers and Business Managers can apply to list a business themselves at `/businesses/apply`
(see [List your own business](#list-your-own-business)) instead of you creating it directly.
Applications show up right in `/businesses`' usual table with a **Pending** status badge — use
the **status filter** dropdown (All / Pending approval / Approved / Rejected / Hidden) to see
just them. A pending row gets two buttons: a green check to **Approve** it outright, or a red ✕
to **Reject** it, which asks for a short reason first (shown to the applicant, and visible as a
tooltip on the Rejected badge afterward). Approving or rejecting notifies whoever applied.
Approving also promotes the applicant to Business Manager (if they aren't already an Admin or
Business Manager) and staffs them on the newly-approved business automatically — otherwise
they'd have no way to reach `/businesses/edit/{id}` to actually manage what they just applied
for. Changed your mind about a rejected application? Its row gets a **reconsider** button (↺)
that approves it after all, with the same auto-staffing.

### Moderate businesses and packages

Sometimes a listing needs to come down without a full delete — an out-of-date photo, an
unverified claim, anything you'd want fixable rather than gone for good. On an **Approved**
row in `/businesses`, the eye-slash button **hides** it (asks for a reason first) — it
disappears from the customer-facing storefront immediately but stays fully intact for you to
edit; the eye button **unhides** it again once whatever prompted it is resolved. The **Hidden**
filter in the status dropdown shows every currently-hidden business. `/packages` has the
identical hide/unhide toggle per row, scoped to one package instead of a whole business — a
hidden package shows a **Hidden** badge (hover for the reason) next to its Daily badge, if any.

Customers can also flag a business or package themselves (see [Report a kitchen or
package](#report-a-kitchen-or-package)) — open reports land on `/reports`, admin-only. Each row
shows what was reported, why, who reported it, and when. **Dismiss** closes the report with no
action taken; **Hide target** hides the business or package using the reporter's own reason (you
don't have to retype it) and closes the report. Resolved reports drop off this list — their
outcome is recorded in the audit log instead.

### Audit log

`/audit-log` is the record of every sensitive action taken across the platform: role changes,
businesses created/edited/deleted, staff added/removed, applications submitted/approved/
rejected, and every hide/unhide — who did it, when, to what, and any reason given. Search by
actor or target name, or filter by action type or target type (User/Business/Package). It's
read-only — there's no way to edit or delete an entry, and nothing here is ever written except
as a side effect of an action actually happening elsewhere in the app.

### Manage packages and orders anywhere

`/packages` and `/orders/manage` both gain an **All businesses** dropdown filter for admins
instead of being locked to one currently-selected business — pick a specific business or leave
it on "All" to see everything at once. Order actions (Confirm/Complete/Cancel/No-show) work
exactly the same way here as they do for a BusinessManager. The [bulk-select
toolbar](#manage-whats-available) works the same way too, and isn't limited to one
business at a time — an admin viewing "All businesses" can select and act on packages from
several businesses in the same batch.

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

### Manage kitchen and package types

`/types` lets an Admin add, rename, or delete the categories businesses and packages pick from
(Restaurant/Bakery/Cafe/... for kitchens, Surprise Bag/Meal Box/... for packages) without
needing a code change or a database migration for a single new row. Each of the two lists (side
by side) supports an inline rename (pencil icon → edit the name in place → check to save) and a
delete (trash icon, confirmed before it happens). Deleting a type that's still used by at least
one business or package is blocked with an explanation instead of silently breaking those rows —
reassign or remove them first.

### Platform-wide visibility

`/dashboard` and `/payments` show store-wide totals for an Admin instead of being scoped to one
business — every order, every package, every payment collected or refunded, across every
kitchen on the platform, plus a `Businesses` and `Users` stat tile that only Admins see. The
[Business Analytics card](#keep-an-eye-on-things) follows the same rule: sell-through rate and
busiest pickup hours are platform-wide for an Admin, not just one business's.
