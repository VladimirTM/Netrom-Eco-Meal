# Feature Ideas for Netrom Eco Meal

A personal project, so this is just a running scratch list — no team to coordinate, no
users to grow, no roadmap process to justify. Ideas get picked because they're fun to
build, teach something new, or round out the app, not because of an ROI calculation. See
[AI_FEATURE_IDEAS.md](AI_FEATURE_IDEAS.md) for the AI-specific ideas, split out because
they all share one dependency (a local LLM) rather than being independent work.

## Shipped so far

A build log, grouped roughly by when each batch landed — not a promise of what's next.

**Core storefront & impact tracking** — food-saved (kg) tracking end to end (package
weight → order confirmation → personal and platform-wide totals on the home hero);
package-level home page search (matches live packages, not just business name/type) plus
a "closing soon" sort.

**Foundations** — an in-app notification bell; a background sweep that auto-cancels
stale Pending orders (this one was a real bug fix, not just a nice-to-have — an abandoned
Pending order held a stock reservation indefinitely); favorites; dietary/allergen tags.

**Remaining quick wins** — reorder ("order again"), CSV export, a dashboard trend chart,
and per-user rate limiting on order placement.

**Test suite** — an xUnit project: unit tests for `OrderService`'s status-transition/
stock logic, plus integration tests for `DbSeeder` against a real Postgres container.

**Email + no-show handling** — order-lifecycle emails, pickup reminders, and a `NoShow`
status where the charge is deliberately kept (it doubles as the no-show fee).

**Recurring packages + geolocation** — "repeat this every day" templates so a manager
doesn't hand-recreate the same package every morning; lat/lng on `Business` plus
distance sort and a Leaflet map view.

**Payments + multi-staff businesses** — real Stripe Checkout (test-mode); an `Order` is
only created once Stripe confirms payment, bridged by a short-lived `PendingCheckout`
row; automatic refund on cancel. `BusinessStaff` many-to-many replaced the old
single-manager-per-business model, so a manager can staff more than one business.

**Manager productivity + trust & safety** — bulk package actions (duplicate/adjust
quantity/extend pickup window) and a business analytics card (sell-through rate, busiest
pickup hours); self-service business applications with admin approval, hide/unhide
moderation on businesses and packages, customer reports, and an audit log.

**Observability + business hours** — Serilog replaces the default console logger with
structured, enriched logging (request timing/exceptions via `UseSerilogRequestLogging`,
environment-aware minimum levels in config); `BusinessHours`/`BusinessClosure` give each
business a weekly schedule plus holiday date ranges, surfaced as a "closed now" badge and
an hours/closure panel on the home page, business cards, and business detail page.

**Package-level reviews** — `Review` keeps its `(BusinessId, UserId)` scope but gains an
optional `PackageId`, tagging a review to a specific package the reviewer actually
completed an order for (`OrderRepository.GetCompletedPackagesAsync` backs the picker).
Shows as a package-name pill on each review card, a per-package rating in
`PackageDetailModal`, and an optional "which package?" dropdown on `BusinessDetail.razor`'s
review form — an unrecognized/stale `PackageId` is silently dropped rather than rejected.

**Web push notifications** — a service worker (`wwwroot/service-worker.js`) + PWA manifest
(`wwwroot/manifest.webmanifest`) stand up the browser-push plumbing; the notification bell's
panel gains an "enable browser alerts" toggle that subscribes via the Push API and hands the
subscription to `PushSubscriptionController`. `NotificationService.CreateAsync` — the one
choke point every existing notification (order lifecycle, restock, business
approve/reject/hide...) already goes through — now also fires a best-effort push via
`IWebPushGateway` (the `WebPush` NuGet package, VAPID-signed, no third-party account needed)
to every subscription a user has, pruning any the push service reports as gone (404/410). No
per-caller changes were needed anywhere else in the app.

**Multiple pickup passes** — reworked the order/QR model from an implicit one-QR-per-order
into `OrderPickupPass`, a first-class child of `Order`. A Confirmed order still gets exactly
one pass by default (`OrderService.ApplyStatusChangeAsync`), but the customer can split it
into up to `PickupPasses.MaxPasses` separate QR codes on `OrderPickupPass.razor` — one per
person in a group order — via `OrderService.SplitPickupPassesAsync`. Each pass gets its own
`/orders/validate/{orderId}/{passId}` URL; redeeming any single one
(`OrderService.RedeemPickupPassAsync`) completes the whole order, so whoever from the group
gets there first is the one who scans, and the rest just see "already picked up." Hit (and
fixed) a real EF Core gotcha along the way: adding a new `OrderPickupPass` via the
`order.PickupPasses` collection-navigation instead of `dbContext.OrderPickupPasses.Add(...)`
gets tracked as `Modified` rather than `Added` — a client-assigned, non-default Guid key
discovered only through relationship fixup looks like an existing row to EF Core — which
turns the INSERT into a silent no-op UPDATE. A mocked `IOrderRepository` can't catch that
class of bug, which is why `OrderServicePickupPassIntegrationTests` runs the real
`OrderService` against a real Postgres container instead.

**Home page browsing polish** — a dietary/allergen filter dropdown next to the kitchen-type
one on `Home.razor` (options straight from `Constants.DietaryTags.All`, split into a
preference optgroup and a "Contains X" allergen-warning optgroup); a `dietaryTag` parameter
threaded through `BusinessController`/`BusinessService`/`BusinessRepository.GetPagedAsync`
narrows results to businesses with at least one live package carrying that tag, same
server-side query shape as the existing kitchen-type filter. A red "Ends in N min" countdown
badge shows on `BusinessDetail.razor`'s package rows (and `PackageDetailModal`) for anything
closing within the hour — the same threshold the existing "closing soon" sort already used,
now visible per package instead of only affecting sort order.

**Business/package type management + community impact leaderboard** — a new admin-only `/types`
page gives `BusinessType`/`PackageType` a real write side (`BusinessTypeController`/
`PackageTypeController` over the new `AddAsync`/`UpdateAsync`/`DeleteAsync` on their services),
so a new kitchen or package category no longer needs a code change and a migration to add one
row. Delete is blocked with a friendly `Conflict` (not a raw DB error) whenever a `Business`/
`Package` still references that type — both FKs are required relationships with no explicit
`OnDelete` configured, so EF Core's convention default is `Cascade`, and deleting a type in use
would otherwise silently take every business/package of that type down with it. Separately, a
new public `/impact` page ranks the current month's top rescuers by kg saved
(`OrderRepository.GetTopRescuersAsync`, grouped server-side, not pulled into memory like the
existing per-user kg stats on `/orders`/`/dashboard`) — opt-in only: `ApplicationUser` gained a
`ShowOnLeaderboard` bool (off by default, flipped from a toggle right on `/impact` itself), and
a customer with real Completed orders who never opted in simply never appears, not even as an
anonymized row.

**Package/business image upload** — `Package.ImageUrl`/`Business.ImageUrl` stay plain text
columns, but `BusinessForm.razor`/`PackageForm.razor` now offer a real upload next to the
manual-URL fallback: an `InputFile` streams straight to the new `IImageUploadService`
(`ImageUploadService`, local disk under `wwwroot/uploads/{businesses,packages}`, GUID
filenames, extension allowlist from `Constants.ImageUpload`), which hands back the saved
file's `/uploads/...` URL to drop into the same `ImageUrl` field a pasted link would've gone
into — no schema change needed. The one real gotcha: `app.MapStaticAssets()` only serves the
build-time static-web-assets manifest, so a file written to `wwwroot/uploads` *after* the app
has started would 404 (or get swallowed by the Blazor Server fallback route) despite sitting
right there on disk — `Program.cs` adds a small, separate `UseStaticFiles` middleware scoped to
that one directory to cover it. `docker-compose.test.yml` mounts `wwwroot/uploads` as a named
volume so uploads survive a `docker compose up --build`, and it's gitignored since it's runtime
content, not source.

**Live "sold out" stock updates** — went with "reusing Blazor Server's own circuit connections"
over a standalone SignalR hub: a singleton `PackageStockBroadcaster` (the only service here that
isn't `Scoped` or a `BackgroundService`) exposes one C# event, `BusinessStockChanged`.
`OrderService`/`PackageService` call `NotifyBusinessChanged(businessId)` right after any
stock-relevant `SaveChangesAsync` succeeds; `BusinessDetail.razor` subscribes with the same
`OnChange` + `IDisposable`-unsubscribe idiom `CartService`/`ClientTimeZoneService` already use,
except this event reaches every open circuit, not just its own. Hit a real EF Core gotcha along
the way: `EcoMealDbContext` lives for a whole circuit, so a re-query after the broadcast kept
returning the *first* query's tracked `Package` instances — EF's identity map ignores a query's
fresh values for any row it already tracks. Fixed with `.AsNoTracking()` on
`PackageRepository.GetPagedAsync`/`GetAllAsync` (pure read paths; every write re-fetches through
`GetByIdAsync`/`GetByIdsAsync`). Caught only by a real two-tab browser test, not the unit suite —
same class of bug the `OrderPickupPass` gotcha above needed a real database to catch.
