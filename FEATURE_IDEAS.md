# Feature Ideas for Netrom Eco Meal

A brainstorm of features that would extend the current app, organized into
**implementation phases** based on value, difficulty, and dependencies between
ideas. Each idea notes roughly what it touches in the existing codebase.
Nothing here is committed to — just a menu, sequenced.

> **Status (2026-08-07):** Phases 1–7 are all shipped now, including payments — only the
> opportunistic/reconsider items below are still open. Items marked `[x]` below are done;
> `[ ]` are not.

## Where the app stands today

Blazor Server app, 3 roles (Customer / BusinessManager / Admin), Postgres + EF Core.
Customers browse businesses (`/`), view live packages, add to a single-business cart
(`CartService`, localStorage-backed), pay via Stripe Checkout, track orders (`/orders`),
show a QR pickup pass, and leave one review per business. Managers/Admins run `/packages`,
`/orders/manage`, `/orders/scan`, `/payments`, and a basic `/dashboard` with counts.
This section is a snapshot from before Phases 1–7 shipped — see the phase list below for
what's actually landed since.

---

## Phase 1 — Immediate, no dependencies ✅ shipped

Highest visible payoff, pure additions to existing pages, nothing else needs to
land first. Start here.

- [x] **Impact tracking basics**: add a weight (kg) to `Package`/`PackageType`, show "X kg
  saved" on order confirmation, `/orders` history, and the pickup pass; add a running
  personal total and a platform-wide counter on the home hero (`Home.razor` already
  has a `home-stats` row — natural 4th stat). This is the app's core "eco" value prop
  and today nothing measures it at all.
- [x] **Package-level search on the home page**: `Home.razor`'s search only matches
  business name/type today, not what's actually in live packages (e.g. "bread"). Also
  add a **"closing soon" sort** by nearest `PickupEnd` — small change, likely improves
  conversion.

## Phase 2 — Quick wins that unblock later work ✅ shipped

Still small schema/UI changes, but prioritized because later phases depend on them.

- [x] **In-app notification bell**: a `Notification` table + bell icon in
  `MainLayout.razor`. No external service dependency, and it's the foundation Phase 5
  (no-show alerts, back-in-stock) needs.
- [x] **Background job to expire stale Pending orders**: orders that are never confirmed
  just sit there today, holding a stock reservation via the `pendingElsewhere` check
  in `OrderService.PlaceOrderAsync`. A scheduled sweep to auto-cancel them fixes a
  real phantom-stock-lock bug, not just a nice-to-have — do this before order volume
  grows.
- [x] **Favorites / follow a business**: one `Favorite(UserId, BusinessId)` join table, a
  heart icon on `BusinessDetail.razor` and business cards, a favorites filter on the
  home page.
- [x] **Allergens & dietary tags**: tags on `Package`/`PackageType` (vegetarian, vegan,
  gluten-free, contains nuts...) shown in `PackageDetailModal.razor` — table-stakes
  for a food app and currently completely missing.

## Phase 3 — Remaining quick wins ✅ shipped

Cheap and independent — no reason to wait, but no other phase depends on them either.

- [x] **Reorder / "order again"**: one click from `/orders` history to re-add a past
  order's packages to the cart, subject to current availability.
- [x] **CSV export**: export orders or impact stats for a date range — mostly a query +
  a download button.
- [x] **Richer dashboard counts → trends**: `Dashboard.razor` only shows current totals;
  charting orders/day or food-saved/week over time is a query change, not a redesign.
- [x] **Rate limiting on order placement**: nothing stops one customer from spamming
  `PlaceOrderAsync`; a simple per-user throttle closes an easy abuse path.

## Phase 4 — Pay down risk before the big bets ✅ shipped

- [x] **Automated test suite**: no test project exists yet. Unit tests around
  `OrderService`'s status-transition/stock logic (the trickiest logic in the app)
  plus integration tests around seeding aren't a "feature" but pay for themselves
  once the codebase keeps growing. Do this **before**, not after, Phases 5–7 — those
  touch the same order-flow code, and bugs there get more expensive to find once
  more logic is layered on top.

## Phase 5 — Big bets unlocked by the Phase 2 notification bell ✅ shipped

- [x] **Email notifications**: order confirmed/cancelled, pickup reminders, back-in-stock
  alerts. Bigger than the notification bell because it needs a real email sender, but
  high leverage — ASP.NET Identity's email confirmation is already wired for
  `RequireConfirmedAccount` and unused (`Program.cs`), so this also unlocks account
  confirmation and password reset for free.
- [x] **No-show / low-stock alerts**: flagging a Confirmed order that was never picked up
  by `PickupEnd` needs a new status (`OrderStatuses` has no "NoShow") plus a delivery
  channel (bell or email) — depends on the two items above.

## Phase 6 — Big bets, independent infra ✅ shipped

Each needs new infrastructure or a bigger schema change, but doesn't depend on
Phases 1–5.

- [x] **Recurring/template packages**: managers currently hand-recreate the same package
  every day (`PackageForm.razor`). A "repeat daily" template that auto-generates
  tomorrow's `Package` rows removes the single most repetitive manager task, but
  needs a scheduler and a template data model.
- [x] **Geolocation / distance sorting + map view**: store lat/lng on `Business`, use
  browser geolocation (there's already an `IJSRuntime` interop pattern in
  `CartService` to follow) to sort/filter by distance; a map view is a natural
  follow-on once coordinates exist. Meaningful UX upgrade, but touches data model,
  JS interop, and possibly a mapping library.

## Phase 7 — Highest ceiling, highest cost/risk

Scope each of these separately from everything else; don't bundle with other work.

- [x] **Payments (Stripe)**: checkout now redirects to a real Stripe Checkout Session
  (test-mode; `Stripe:SecretKey` config, empty by default with a friendly "payments aren't
  configured yet" error until it's set) — an `Order` is only created once Stripe confirms
  payment, bridged by a short-lived `PendingCheckout` row (`ICheckoutService`) so an
  abandoned payment never creates a phantom order. Confirmed on redirect back from Stripe
  (`/checkout/return`), no webhook — simpler to run locally, no Stripe CLI/ngrok needed.
  `Payment` is a new 1:1-with-`Order` entity; `OrderService` refunds automatically whenever
  an order is Cancelled (manual or the stale-Pending sweep), but deliberately *not* on
  NoShow — the kept charge doubles as the no-show fee with no extra fee-specific code. A new
  `/payments` page gives managers/admins a lightweight payout ledger (reuses the existing
  order-management query, no new controller/repository needed).
- [x] **Multiple staff per business**: replaced `Business.ManagerId` with a `BusinessStaff`
  many-to-many join table — a business can have several staff, and a staff member can be
  assigned to more than one business. `IBusinessService.IsStaffAsync` centralizes the
  authorization check that used to be duplicated per service; a new scoped
  `ManagedBusinessContext` (mirrors `CartService`'s pattern) tracks a manager's currently
  selected business with a switcher in `NavMenu.razor` when they staff more than one.
  `Businesses.razor`/`Users.razor` now show staff as removable chips with an add dropdown.

---

## Opportunistic fill-ins

Don't plan a cycle around these on their own — slot them in when touching related
code in one of the phases above.

- [ ] **Bulk actions on `/packages`**: multi-select to duplicate, adjust quantity, or
  extend pickup windows — natural alongside Phase 6's recurring-template work.
- [ ] **Business hours & holiday closures**: `Business` has no operating-hours model
  today (only packages' `PickupStart/PickupEnd`); explicit hours would let the home
  page show "closed now."
- [ ] **Audit log**: who promoted/demoted a user, who edited a business — `Users.razor`
  and `BusinessForm.razor` make silent changes today with no history. Natural
  alongside Phase 7's multi-staff/approval work.
- [ ] **Business approval workflow**: today only Admins create businesses directly; a
  "business signs up, admin approves" flow enables self-service onboarding.
- [ ] **Package/business moderation**: hide/report flow instead of only hard delete.
- [ ] **Structured logging / error tracking**: no telemetry beyond ASP.NET defaults —
  fine for now, but do this before a real production deployment regardless of which
  phase you're on.
- [ ] **Referral / invite a friend**: lightweight growth loop on top of existing auth.
- [ ] **Business-level analytics** (sell-through rate, busiest pickup hours): cheaper to
  build once Phase 3's dashboard trend charts exist — building on top of those beats
  building it standalone.

## Reconsider — lower value or high effort relative to payoff

Not worth prioritizing unless a specific need comes up.

- [ ] **Package-level reviews**: `Review` is `(BusinessId, UserId)` scoped today; adding
  per-package reviews means a schema change and UI for a fairly marginal signal gain
  over the existing business-level rating.
- [ ] **Multiple pickup passes / split QR across people**: real but niche use case,
  meaningful rework of the order/QR model for a small slice of orders.
- [ ] **Web push notifications**: same payoff as email/in-app bell for most use cases,
  but higher implementation cost (service worker, subscription management) — only
  worth it if going for a full PWA/install-as-app experience.
