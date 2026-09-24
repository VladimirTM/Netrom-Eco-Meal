# Frontend Architecture — Netrom Eco Meal

**Stack:** Blazor Server (interactive server render mode) · Bootstrap 5 + Bootstrap Icons · a hand-written CSS design system (`app.css`) · vanilla JS interop (no bundler, no npm, no component library)
**Location:** `Components/` + `wwwroot/` (same project as the backend — see `BACKEND_ARCHITECTURE.md`)

There's no separate SPA here — "frontend" means the Razor component tree that runs server-side over a persistent SignalR circuit. There is no client-side state management library, no React/Vue-style virtual DOM, and no REST/GraphQL client: every page talks straight to the in-process "Controllers" documented in `BACKEND_ARCHITECTURE.md` §6. The handful of client-only concerns a SPA would put in Redux/Context (cart contents, the viewer's timezone, which business a multi-staff manager is currently managing) instead live in ordinary Blazor `Scoped` services with a C# event for change notification — see §5.

---

## Table of Contents

1. [Project Structure](#1-project-structure)
2. [Bootstrap & Entry Point](#2-bootstrap--entry-point)
3. [Routing & Route Guards](#3-routing--route-guards)
4. [Layouts](#4-layouts)
5. [Cross-Cutting Client Services](#5-cross-cutting-client-services)
6. [Home Page](#6-home-page)
7. [Business Detail Page](#7-business-detail-page)
8. [Cart Panel & Checkout](#8-cart-panel--checkout)
9. [Orders Page & Reorder](#9-orders-page--reorder)
10. [Pickup Pass, QR Scan & Validate](#10-pickup-pass-qr-scan--validate)
11. [Admin / Manager Pages](#11-admin--manager-pages)
12. [Shared Components](#12-shared-components)
13. [CSS Design System](#13-css-design-system)
14. [JS Interop Patterns](#14-js-interop-patterns)
15. [Key Frontend Design Decisions](#15-key-frontend-design-decisions)

---

## 1. Project Structure

```
Components/
├── App.razor                     # HTML shell — head, ResourcePreloader, ImportMap, <Routes>, ReconnectModal
├── Routes.razor                  # Router + AuthorizeRouteView + NotFoundPage
├── RedirectToLogin.razor         # Full-page redirect for unauthenticated NotAuthorized branch
├── _Imports.razor                # Global @using directives for every .razor file
│
├── Layout/
│   ├── MainLayout.razor          # Sidebar shell for /dashboard, /businesses, /packages, /orders/manage, /payments, /users, /types
│   ├── NavMenu.razor             # The sidebar itself — role-aware nav links + user footer
│   ├── PublicLayout.razor        # Header + footer shell for the customer storefront
│   ├── EmptyLayout.razor         # Chrome-free — Login/Register only
│   └── ReconnectModal.razor(.css/.js)  # Stock Blazor Server reconnect UI (framework template, not customized)
│
├── Pages/
│   ├── Home.razor                # / — storefront browse/search/filter
│   ├── BasketPlanner.razor       # /plan-basket — Phase 4 AI budget/rescue-basket planner
│   ├── BusinessDetail.razor      # /businesses/{Id} — packages + reviews + favorite + add to cart
│   ├── BusinessApply.razor       # /businesses/apply — Phase 9 self-service business signup
│   ├── Impact.razor              # /impact — Phase 11 monthly kg-saved leaderboard, opt-in toggle
│   ├── PaymentReturn.razor       # /checkout/return — Stripe success redirect landing page, confirms payment
│   ├── PaymentCancel.razor       # /checkout/cancel — Stripe cancel redirect landing page
│   ├── RescueCircleList.razor    # /circles — Phase 13 "your Rescue Circles" list, organized or joined
│   ├── RescueCircleInvite.razor  # /circles/{Id} — join/pay/leave a Rescue Circle, or view its public summary
│   ├── RescueCircleReturn.razor  # /circles/return — Stripe success/failure landing page for one participant's share
│   ├── AccountSettings.razor     # /account/settings — PublicLayout host, any signed-in role
│   ├── AccountSettingsDashboard.razor # /account-settings — MainLayout host, Admin/BusinessManager only
│   ├── TripPlanner.razor         # /trip-planner — nearest-neighbor pickup route across live orders (Phase 15)
│   ├── Referrals.razor           # /referrals — Phase 16 invite link, store-credit balance, invite history
│   ├── Brands.razor / BrandDetail.razor  # /brands, /brands/{Id} — Phase 17 multi-location chain grouping
│   ├── Orders.razor              # /orders — customer order history + cancel + reorder + rescue-streak stat (Phase 16)
│   ├── OrderPickupPass.razor     # /orders/pickup/{Id} — QR code(s) for a Confirmed order, splittable into several
│   ├── OrderScan.razor(.js)      # /orders/scan — manager camera scanner + manual order-number lookup fallback
│   ├── OrderValidate.razor       # /orders/validate/{Id}/{PassId} — confirm-pickup landing page (scanned or typed)
│   ├── OrderValidateLegacy.razor # /orders/validate/{Id} — pre-multi-pass QR redirects here, then on to OrderValidate
│   ├── Login.razor / Register.razor  # Register.razor prefills an optional referral code from ?ref=CODE (Phase 16)
│   ├── ForgotPassword.razor / ResetPassword.razor / ConfirmEmail.razor
│   ├── ResendConfirmation.razor  # /account/resend-confirmation — same shape as ForgotPassword.razor
│   ├── AccessDenied.razor / NotFound.razor / Error.razor
│   ├── Dashboard.razor           # /dashboard — stat cards + 14-day trend chart
│   ├── Businesses.razor / BusinessForm.razor
│   ├── Packages.razor / PackageForm.razor
│   ├── PackageTemplates.razor    # /packages/templates — recurring "repeat daily" template management
│   ├── OrderManagement.razor     # /orders/manage — confirm/complete/cancel + CSV export
│   ├── Payments.razor            # /payments — manager/admin payout ledger + its own CSV export
│   ├── Users.razor               # /users — role + business-manager assignment
│   ├── Reports.razor             # /reports — Phase 9 admin moderation queue (open reports)
│   ├── AuditLog.razor            # /audit-log — Phase 9 admin read-only action history
│   └── Types.razor               # /types — Phase 11 admin CRUD for BusinessType/PackageType
│
└── Shared/
    ├── AccountSettingsPanel.razor # Name + password form, shared by both AccountSettings pages above
    ├── AnchoredDropdown.razor    # Generic trigger+panel dropdown, JS-positioned to escape overflow clipping
    ├── ConfirmDialog.razor       # Generic confirm/cancel modal
    ├── ReportDialog.razor        # Phase 9 — same shell as ConfirmDialog, plus a required reason textarea
    ├── SafeFocusOnNavigate.razor # Replaces <FocusOnNavigate> — only focuses if nothing else has focus, see §15
    ├── ForbiddenPanel.razor / NotFoundPanel.razor
    ├── NotificationBell.razor   # Trigger button only — badge + polling, used in both layouts
    ├── NotificationPanel.razor  # The actual popup, rendered once at each layout's top level — also
                                  # owns the "enable browser alerts" web push toggle, see §12
    ├── OrderDetailModal.razor / PackageDetailModal.razor
    ├── ImpactEquivalencyStats.razor  # Impact Phase 1 — kg saved -> km not driven / L water saved
    ├── Pagination.razor
    ├── StarRating.razor         # Read-only fractional-fill display + editable 1-5 picker, same component
    ├── ThemeToggle.razor        # UI revamp Phase 2 — trigger only, used in both layouts, see §12/§13
    └── CartPanel.razor          # Slide-in basket + checkout

Constants/  Models/               # Debouncer, PaginatedList<T>, GeoDistance, TripPlanner — see below and BACKEND_ARCHITECTURE.md
wwwroot/
├── app.css                       # ~4300 lines, one file, no preprocessor — see §13
├── js/site.js                    # window.EcoMeal namespace — see §14
├── service-worker.js             # Web push: push + notificationclick handlers, registered by
│                                  # EcoMeal.push.registerAsync on every page load (§14)
├── manifest.webmanifest          # PWA manifest — served with an explicit application/manifest+json
│                                  # content type from Program.cs, since MapStaticAssets' default
│                                  # FileExtensionContentTypeProvider has no entry for .webmanifest
└── lib/{bootstrap, jsqr}/        # Vendored, checked-in dependencies (no package.json) — Leaflet is the one
                                   # exception, loaded from a CDN instead (§2), since it's used on one page
```

---

## 2. Bootstrap & Entry Point

**File:** `Components/App.razor`

```razor
<head>
    ...
    <link rel="preconnect" href="https://fonts.googleapis.com"/>
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin/>
    <link href="https://fonts.googleapis.com/css2?family=Fraunces:...&family=Hanken+Grotesk:...&family=JetBrains+Mono:...&display=swap" rel="stylesheet"/>
    <link rel="stylesheet" href="@Assets["lib/bootstrap/dist/css/bootstrap.min.css"]"/>
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css"/>
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" integrity="sha256-..." crossorigin=""/>
    <link rel="stylesheet" href="@Assets["app.css"]"/>
    <link rel="stylesheet" href="@Assets["Netrom-Eco-Meal.styles.css"]"/>  <!-- CSS-isolation bundle -->
    <ImportMap/>
    <HeadOutlet @rendermode="InteractiveServer"/>
</head>
<body>
<Routes @rendermode="InteractiveServer"/>
<ReconnectModal/>
<script src="@Assets["_framework/blazor.web.js"]"></script>
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js" integrity="sha256-..." crossorigin=""></script>
<script src="js/site.js"></script>
</body>
```

Google Fonts is the one other CDN dependency besides `bootstrap-icons`/Leaflet — three families (`Fraunces` for display headings, `Hanken Grotesk` for body text, `JetBrains Mono` for the order-ticket number/QR labels), wired up as CSS custom properties in `app.css`'s design tokens (§13) rather than applied ad hoc per element.

`@rendermode="InteractiveServer"` on `<Routes>` is what turns the *entire* app interactive over one persistent SignalR circuit — every `@page` in this project also carries its own explicit `@rendermode InteractiveServer`, which is redundant given the ancestor already set it, but keeps each page's rendering mode visible and self-documenting at the point of use rather than requiring a reader to know it's inherited from `App.razor`. `Netrom-Eco-Meal.styles.css` is the auto-generated bundle of every `*.razor.css` CSS-isolation file in the project (currently just `MainLayout.razor.css`, `NavMenu.razor.css`, `ReconnectModal.razor.css`) — everything else styles through the single global `app.css`, deliberately, so the visual language stays in one place instead of fragmenting across dozens of scoped stylesheets (see §13).

Leaflet is the one third-party JS dependency in the whole app, loaded the same way `bootstrap-icons` already was — a plain CDN `<link>`/`<script>` pair with a real `integrity` hash (SRI), no npm install, no bundler step. It's only ever exercised by `Home.razor`'s map view toggle (§6), so pulling it in via `wwwroot/lib/` like Bootstrap/jsQR would mean vendoring a dependency the rest of the app never touches for no real benefit.

**`<HeadOutlet>` needs its own `@rendermode`, matching `<Routes>`'s.** `<Routes @rendermode="InteractiveServer">` only makes the `<Routes>` subtree interactive — `<HeadOutlet>` lives in `<head>`, outside that subtree, as a *separate* render tree that `<PageTitle>`/`<HeadContent>` write into. Without its own render mode it stays static, rendered once from the initial prerender and never again: the tab title (and anything else pushed through `<PageTitle>`) would freeze at whatever the very first page loaded, silently going stale on every subsequent in-app navigation. Both call sites need to agree on the render mode for the header to stay live.

`ResourcePreloader`/`ImportMap`/`HeadOutlet` are stock ASP.NET Core 10 Blazor Web App template tags, unmodified.

`<head>` also carries `<link rel="manifest" href="manifest.webmanifest">` and a matching `<meta name="theme-color">` — the PWA manifest backing the web push feature's installability (§12, `BACKEND_ARCHITECTURE.md` §3 PushSubscription). Neither needs a render mode of its own; they're static `<head>` tags, not something a Razor component writes into after the fact the way `<PageTitle>` does.

---

## 3. Routing & Route Guards

**File:** `Components/Routes.razor`

```razor
<Router AppAssembly="typeof(Program).Assembly" NotFoundPage="typeof(Pages.NotFound)">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)">
            <NotAuthorized>
                @if (context.User.Identity?.IsAuthenticated == true)
                {
                    <ForbiddenPanel Message="Your account doesn't have permission to view this page." BackHref="/" BackLabel="Back to home"/>
                }
                else
                {
                    <RedirectToLogin/>
                }
            </NotAuthorized>
        </AuthorizeRouteView>
        <SafeFocusOnNavigate RouteData="routeData" Selector="h1"/>
    </Found>
</Router>
```

The `NotAuthorized` branch deliberately splits into two different experiences based on **why** access was denied:
- **Signed in, wrong role** → `ForbiddenPanel` renders inline, in whatever layout the target page would have used (since `AuthorizeRouteView` still renders `DefaultLayout`/the page's own `@layout` around the `NotAuthorized` fragment) — no navigation happens, the URL bar stays put.
- **Not signed in at all** → `RedirectToLogin`, which does a **`forceLoad: true`** full-page navigation to `/account/login?returnUrl=...`, not a Blazor in-circuit navigation:
  ```csharp
  NavigationManager.NavigateTo($"/account/login?returnUrl=/{returnUrl}", forceLoad: true);
  ```
  This fires from inside `Routes.razor`'s `NotAuthorized` branch — at that point, for an unauthenticated visitor, no meaningful circuit/interactivity has been established yet for the target page, so a soft navigation isn't available; the comment in the source is explicit about this being a hard requirement, not a stylistic choice.

`DefaultLayout="typeof(Layout.MainLayout)"` only applies when a page doesn't specify its own `@layout` — every customer-facing page overrides it with `@layout PublicLayout`, and the chrome-free auth/Stripe-redirect pages with `@layout EmptyLayout` (see §4).

`NotFoundPage="typeof(Pages.NotFound)"` handles client-side "no route matched." Separately, `Program.cs` wires `app.UseStatusCodePagesWithReExecute("/not-found", ...)` so a raw HTTP 404 (a bad static-asset path, etc.) re-executes the same page rather than showing a different one — the two 404 paths (router-level vs status-code-level) converge on identical UI.

One consequence worth knowing: when `NotFoundPage` renders via the Router's in-circuit fallback (no fresh HTTP request backing it), any `<AntiforgeryToken/>` on that render — e.g. the header's Sign Out form, present on every layout — has no request-scoped `HttpContext` to mint a token against, and renders no hidden field at all. See `BACKEND_ARCHITECTURE.md` §7 for why that's handled by not requiring antiforgery on logout, rather than by fighting this rendering path.

`SafeFocusOnNavigate` (`Components/Shared/SafeFocusOnNavigate.razor`) replaces the framework's own `<FocusOnNavigate>` — same "move focus to the page heading on route change" intent for screen readers, but the swap-in only calls `.focus()` if nothing else has focus yet (§15 has the bug that motivated it).

---

## 4. Layouts

| Layout | Used by | Shell |
|---|---|---|
| `PublicLayout` | Home, BusinessDetail, BusinessApply, Orders, TripPlanner, OrderPickupPass, BasketPlanner, Impact, AccountSettings, AccessDenied, NotFound | Sticky header (logo, impact-leaderboard trophy link, notification bell, AI plan-basket sparkle for Customer, orders link + trip-planner link + basket button/badge for Customer, dashboard link for staff, "list your business" link for Customer/BusinessManager, account-settings gear icon, `ThemeToggle` (Phase 2), logout), `@Body`, footer. Owns the `CartPanel` and (`AuthorizeView`-gated) `NotificationPanel`, and the cart's open/closed state |
| `MainLayout` | Dashboard, Businesses(+Form), Packages(+Form), PackageTemplates, OrderManagement, Payments, AccountSettingsDashboard, Users, Reports, AuditLog, Types, OrderScan, OrderValidate, OrderValidateLegacy | Fixed left sidebar (`NavMenu`) + `<main>` content area — the classic admin-panel shell. Also owns `NotificationPanel` |
| `EmptyLayout` | Login, Register, ForgotPassword, ResetPassword, ConfirmEmail, ResendConfirmation, PaymentReturn, PaymentCancel | Just `@Body` — no header, no sidebar, no footer; the login/register cards and the Stripe redirect landing pages all center themselves entirely via `app.css`'s `.login-page`/`.login-card`/`.cart-confirmation` (see §8) |

`MainLayout`'s `.page`/`.sidebar` (`MainLayout.razor.css`) are sized `height: 100dvh`, not `100vh` — `vh` is the *largest possible* viewport and ignores transient browser chrome (an address bar, a devtools/automation banner), so a `100vh`-tall sidebar can render a few px taller than what's actually visible, pushing its footer past the real bottom edge. `dvh` tracks the actual visible viewport instead. `.page` also carries `overflow-x: hidden` — a safety net, not the thing that actually scrolls a wide table: a mobile-width admin table was dragging the whole page (header/sidebar included) into horizontal scroll before this was added, even though the table already sits inside Bootstrap's own `.table-responsive` (`overflow-x: auto`). That one line stops the overflow from escaping upward past `.page`; `.table-responsive` still owns the actual scrolling.

**Both layouts render `<NotificationPanel/>` as a sibling of `.sidebar`/`.public-header`, never nested inside either.** `.sidebar` and `.public-header` are both `position: sticky`, and `position: sticky` *always* establishes a new CSS stacking context (unlike `position: relative`, which only does with a non-auto `z-index`) — regardless of `z-index`. A `position: fixed` popup nested inside one of those elements still computes its on-screen coordinates against the viewport correctly, but its paint order gets trapped *inside* that stacking context, so the whole sidebar/header subtree (popup included) paints as one atomic unit at its slot in the DOM — which is before `<main>`. `<main>`'s content then paints on top and visually covers the popup, even though every computed style (`z-index`, `opacity`, `display`) looks completely correct in devtools. This is why `NotificationBell` (the trigger, kept in the sidebar/header) and `NotificationPanel` (the actual popup) are two separate components sharing state through `NotificationPanelState` — see §5 — rather than one component like `ConfirmDialog`/`ReportDialog`, which get away with rendering inline because they're only ever used from page components inside `<main>`, which has no stacking-context-creating ancestor.

### PublicLayout — the customer chrome

```razor
@inject CartService CartService
@inject ClientTimeZoneService ClientTimeZoneService
...
<a href="/impact" class="public-cart-btn">...</a>          <!-- both AuthorizeView branches, see below -->
<NotificationBell TriggerClass="public-cart-btn"/>
@if (context.User.IsInRole(AppRoles.Customer))
{
    <a href="/plan-basket" class="public-cart-btn">...</a>  <!-- ✨ bi-stars, AI basket planner, §6 -->
    <a href="/orders" class="public-cart-btn">...</a>
    <button @onclick="ToggleCart">
        <i class="bi bi-basket2"></i>
        @if (CartService.TotalCount > 0) { <span class="public-cart-badge">@CartService.TotalCount</span> }
    </button>
}
<a href="/account/settings" class="public-cart-btn">...</a>
...
<CartPanel @bind-IsOpen="_cartOpen"/>
@* Gated the same as MainLayout's — an anonymous visitor has no bell to open it with, so
   there's no reason to start its 30s polling timer for them (§5, §12). *@
<AuthorizeView>
    <Authorized><NotificationPanel/></Authorized>
</AuthorizeView>

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await CartService.RestoreAsync();          // localStorage → live Package entities, needs JS interop
            await ClientTimeZoneService.InitializeAsync();  // Intl API, also needs JS interop
            StateHasChanged();
        }
    }
}
```
Both `RestoreAsync` and `InitializeAsync` are deferred to `OnAfterRenderAsync(firstRender)` rather than `OnInitializedAsync` for the same reason: JS interop isn't available until after the component has actually rendered once and the circuit's JS side is attached. `MainLayout` does the identical `ClientTimeZoneService.InitializeAsync()` dance in its own `OnAfterRenderAsync` (it doesn't need `CartService` — staff don't have a customer basket).

**"List your business" link (Phase 9)**: `<AuthorizeView>` around `context.User.IsInRole(AppRoles.Customer) || context.User.IsInRole(AppRoles.BusinessManager)` gates a small icon-only button next to the Dashboard link, pointing at `/businesses/apply` — deliberately excludes `Admin` (they create businesses directly via `/businesses/create`) and anonymous visitors (self-service signup requires being signed in, same as favoriting or leaving a review).

**Impact leaderboard trophy icon (Phase 11)**: unlike every other header control, this one is duplicated into *both* the `Authorized` and `NotAuthorized` branches of `PublicLayout`'s `<AuthorizeView>` rather than gated to one — `/impact` itself has no `[Authorize]` (§6 Impact leaderboard), so the link needs to be reachable by an anonymous visitor too, and `<AuthorizeView>`'s two branches are mutually exclusive, so there's no single place outside both that would render for everyone.

**AI basket planner sparkle icon**: `bi-stars`, gated to `context.User.IsInRole(AppRoles.Customer)` only (unlike the basket/orders icons, which share that same `if`) — points at `/plan-basket` (§6 AI budget/basket planner).

**Rescue Circles icon (Phase 13)**: `bi-people`, sitting right next to the orders receipt icon inside the same `context.User.IsInRole(AppRoles.Customer)` block — points at `/circles` (§8 Cart Panel & Checkout).

**Standing orders icon (Phase 14)**: `bi-arrow-repeat`, right next to the Rescue Circles icon in the same `Customer`-only block — points at `/standing-orders` (§7 StandingOrders).

**Trip planner icon (Phase 15)**: `bi-signpost-2`, sitting between the orders receipt icon and the Rescue Circles icon in the same `Customer`-only block — points at `/trip-planner` (§9 TripPlanner).

**Referrals icon (Phase 16)**: `bi-gift`, right next to the standing orders icon in the same `Customer`-only block — points at `/referrals` (§7 Referrals).

**Brands icon (Phase 17)**: `bi-diagram-3`, duplicated into both `AuthorizeView` branches like the impact trophy above — `/brands` is public, so an anonymous visitor needs the link too.

**Account settings icon**: `bi-person-gear`, links to `/account/settings` — the one header icon with no role gate beyond plain `<AuthorizeView>`, since every signed-in role (Customer, BusinessManager, Admin) has a name/password to manage. `NavMenu` links to a separate route for the same form (below), so staff get it inside the sidebar chrome instead of the public header.

**Mobile overflow**: 12 icons for a signed-in customer no longer fit a phone width even shrunk. The icon row scrolls horizontally *within itself* rather than the buttons keep shrinking or the whole header/page overflowing — `min-width: 0` on the row lets it shrink below its own content width instead of forcing `.public-header-inner` wider, `overflow-x: auto` gives it its own scrollbar (hidden, with a `mask-image` fade as the swipe hint, cleared once JS detects the row is scrolled all the way via a `.at-scroll-end` class), and the brand block gets `flex-shrink: 0` so it never gets squeezed to make room instead. This behavior is unconditional (not gated behind a breakpoint) precisely because it broke once already: the original fix only covered `@media (max-width: 576px)`, and two more icons shipped since then pushed the row past the viewport again at 768px with no scroll/fade escape hatch at that width. A no-op wherever the row already fits, so the next icon shouldn't need another breakpoint fire drill.

### AccountSettings / AccountSettingsDashboard — profile & password

The actual form — two cards, not one: a plain `EditForm`/`InputText` for the display name (`AuthController.UpdateNameAsync`, in-process like everywhere else), and — reusing the exact pattern `Login.razor` already established — a raw HTML `<form method="post" action="api/auth/change-password" data-enhance="false">` for the password, not an `EditForm`. `ChangePasswordFormAsync` needs a real HTTP response to refresh the auth cookie's security stamp, which an in-process Blazor circuit call can't provide; `data-enhance="false"` stops `blazor.web.js`'s enhanced navigation from swallowing the `?pwError=`/`?pwChanged=` redirect the endpoint responds with. This lives once in `Components/Shared/AccountSettingsPanel.razor`, with two thin `@page` hosts on top of it:

- `Components/Pages/AccountSettings.razor` — `@page "/account/settings"`, `@layout PublicLayout`, `[Authorize]` (any signed-in role) — reached via the header's gear icon, above.
- `Components/Pages/AccountSettingsDashboard.razor` — `@page "/account-settings"`, default `MainLayout`, `[Authorize(Roles = "Admin,BusinessManager")]` — reached via `NavMenu`'s "Account Settings" row (below), so staff get the sidebar instead of the public header.

Same panel either way, since a password belongs to the account, not the role or the chrome around it.

### NavMenu — role-aware sidebar

Plain `<AuthorizeView Roles="@AppRoles.Admin">` gates the "User Roles", "Reports", "Audit Log", and (Phase 11) "Types" nav links; every other link (Dashboard, Businesses, Packages, Orders, Payments, Account Settings) is visible to both `Admin` and `BusinessManager` — the actual data scoping (a manager only sees the business or businesses they're staff of) happens page-side, not by hiding nav links per role. The sidebar footer shows the signed-in user's initial-avatar, email, and a role label resolved by a local `DisplayRole` switch expression, plus the same `NotificationBell` and `ThemeToggle` (Phase 2) triggers the public header uses, both with `TriggerClass="sidebar-notif-btn"` for the icon — the notification popup itself renders from `MainLayout`, not from here (§4, §12).

**"Admin Panel" / "Manager Panel" subtitle**: this sidebar is the one `DefaultLayout` shared by both `Admin` and `BusinessManager` (§3), and the brand subtitle under the logo used to hardcode "Admin Panel" regardless of who was actually signed in — a plain manager saw a title implying admin privileges they don't have. Now a small `<AuthorizeView Roles="@AppRoles.Admin">` picks "Admin Panel" for the `Authorized` branch and "Manager Panel" for `NotAuthorized`, so the label always matches the viewer's real role.

**Business switcher**: for a `BusinessManager`, `AuthorizeView Roles="@AppRoles.BusinessManager"` wraps a block driven by `ManagedBusinessContext.MyBusinesses.Count` — zero businesses renders nothing, one renders a plain "current business" label, and more than one renders an `AnchoredDropdown` (`role-badge`-styled, matching the sidebar's other pill controls) listing every business the manager staffs, with a check mark on `ManagedBusinessContext.SelectedBusinessId`. `NavMenu.OnInitializedAsync` calls `ManagedBusinessContext.EnsureLoadedAsync()` and subscribes to its `OnChange` the same way every other consumer of a cross-cutting client service does (§5) — picking a different business calls `ManagedBusinessContext.SelectAsync`, which persists the choice and fires `OnChange`, and every business-scoped page (Dashboard, Businesses, Packages, PackageForm, PackageTemplates, OrderManagement) re-renders scoped to the new selection without a page navigation.

---

## 5. Cross-Cutting Client Services

Blazor Server has no React-style Context API, but four `Scoped` services fill the identical role — injected wherever needed, holding state for the circuit's lifetime, and exposing a plain C# `event Action? OnChange` that consuming components subscribe to in `OnInitialized`/unsubscribe in `Dispose` (`IDisposable`) so a mutation anywhere re-renders every interested component without prop-drilling.

### CartService

```csharp
public class CartItem { public required Package Package; public int Quantity; }

public class CartService(IJSRuntime jsRuntime, IDbContextFactory<EcoMealDbContext> contextFactory, CurrentUserAccessor currentUserAccessor)
{
    public string StorageKey => _userId is null ? BaseStorageKey : $"{BaseStorageKey}.{_userId}";  // set once in RestoreAsync
    public Guid? BusinessId { get; private set; }       // a basket holds items from exactly one business
    public IReadOnlyList<CartItem> Items => _items;
    public event Action? OnChange;

    public bool WouldReplaceCart(Package package) => BusinessId is not null && BusinessId != package.BusinessId;
    public int AvailableQuantity(Package package, int reservedElsewhere = 0) =>
        Math.Max(0, package.Quantity - reservedElsewhere - InCartQuantity(package.Id));
    public async Task RestoreAsync() { /* resolve _userId, then localStorage → JSON → own short-lived DbContext → live CartItems */ }
    public async Task AddAsync(Package package, int quantity = 1) { /* clamps to package.Quantity, clears cart on cross-business add */ }
}
```
- **Single-business invariant**: `AddInternal` clears every existing item the moment a package from a *different* business is added — `WouldReplaceCart` lets a caller (`BusinessDetail.razor`, `Orders.razor`'s reorder) detect this ahead of time and show a `ConfirmDialog` ("Start a new basket?") instead of silently wiping the customer's basket.
- **`AvailableQuantity`'s `reservedElsewhere` parameter**: purely local math — `package.Quantity` minus whatever's in *this browser's* cart — can't see Pending reservations sitting in the database, whether from other customers or the viewer's own just-placed order (Pending orders don't touch `Package.Quantity`; see `BACKEND_ARCHITECTURE.md` §5). Callers that need an accurate live count pass in a per-package reserved total fetched from `OrderController.GetPendingReservedQuantitiesAsync` (see §6, §7, §15); callers that don't (or can't cheaply) just omit it and get the old cart-only behavior, since the parameter defaults to `0`.
- **Persistence**: every mutation calls `PersistAsync`, which serializes `{PackageId, Quantity}` pairs to JSON and calls `EcoMeal.cart.save(key, json)` (see §14) — wrapped in a swallowed `try/catch` since `localStorage` can throw (private browsing, quota). The in-memory state stays authoritative even if the write silently fails.
- **Own `DbContext`, not the shared one**: `RestoreAsync` used to re-hydrate packages through `IPackageService` (the circuit-scoped `EcoMealDbContext` also used by whatever the routed page was loading), which could race that page's own first-render query on the same context and crash the circuit with `InvalidOperationException: A second operation was started on this context instance...`. It now opens its own short-lived context via `IDbContextFactory<EcoMealDbContext>` — same fix as `NotificationRepository`'s polling bell (`BACKEND_ARCHITECTURE.md` §4). Restore is still lossy by design: any package deleted since the cart was saved just doesn't come back, no "this item is no longer available" reconciliation UI.
- **Namespaced per signed-in user**: `StorageKey` resolves to `ecomeal.cart.{userId}` once `RestoreAsync` sets `_userId` (falling back to the bare `ecomeal.cart` before a user is known, or on `MainLayout`, which never restores a cart). Without this, a basket left over from a session that ended without an explicit Sign Out — closed tab, expired cookie, not just a shared computer — could be picked up by a different account signing in later on the same browser, since `localStorage` isn't otherwise scoped to who's authenticated.
- **Logout also clears it**: both layouts' logout `<button>` carries `onclick="EcoMeal.cart.clear('@CartService.StorageKey')"` — plain inline JS, fired client-side *before* the form's `POST /api/auth/logout` navigates away. Belt-and-braces on top of the per-user namespacing above, not the only thing preventing a leak.

### ClientTimeZoneService

```csharp
public class ClientTimeZoneService(IJSRuntime jsRuntime)
{
    public TimeZoneInfo TimeZone { get; private set; } = TimeZoneInfo.Utc;
    public event Action? OnChange;

    public async Task InitializeAsync() { /* EcoMeal.timeZone() → Intl.DateTimeFormat().resolvedOptions().timeZone */ }
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(...);
    public DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(...);
}
```
Every pickup-window display in the app (`Orders.razor`, `OrderManagement.razor`, `PackageForm.razor`, `Packages.razor`, `OrderPickupPass.razor`, `OrderValidate.razor`) goes through `ToLocal`/`ToUtc` rather than each page reading `DateTime.Now`/formatting UTC directly — this is what makes "Pickup 20:00–22:00" mean the *viewer's* local 8–10pm regardless of where the Postgres server or the Blazor host actually sit. Falls back to UTC silently (`try/catch` around `FindSystemTimeZoneById`) if the browser's Intl API is unavailable or returns an unrecognized zone ID — the app never blocks on this, it just displays UTC times labeled the same as any other time.

`PackageForm.razor` is the one place `ToLocal`/`ToUtc` round-trips in both directions within the same page: pickup times are edited entirely in the viewer's local time (`InputDate Type="InputDateType.DateTimeLocal"`), and only converted back to UTC (`ClientTimeZoneService.ToUtc(_model.PickupStart)`) at submit time. Because `InitializeAsync` resolves the timezone asynchronously (after first render), the form also subscribes to `OnChange` to re-derive its date fields (from cached raw-UTC values, `_existingPickupStartUtc`/`_existingPickupEndUtc`) if the real timezone arrives after the fields were first populated with a UTC-assumed default.

### ManagedBusinessContext

```csharp
public class ManagedBusinessContext(IJSRuntime jsRuntime, IDbContextFactory<EcoMealDbContext> contextFactory, CurrentUserAccessor currentUser)
{
    public List<Business> MyBusinesses { get; private set; } = [];
    public Guid? SelectedBusinessId { get; private set; }
    public event Action? OnChange;

    public Task EnsureLoadedAsync() { /* cached in-flight Task, see below */ }
    public async Task SelectAsync(Guid businessId) { /* persists to localStorage, fires OnChange */ }
}
```
Tracks "which of my businesses am I managing right now" for a `BusinessManager` who may staff more than one business (`BusinessStaff` — `BACKEND_ARCHITECTURE.md` §3). Same shape as `CartService`: `localStorage`-backed (`EcoMeal.managedBusiness.save`/`load`, §14.1), namespaced per user (`ecomeal.managedBusiness.{userId}`), and read/written by `NavMenu`'s business switcher (above) plus every business-scoped admin page (Dashboard, Packages, PackageForm, PackageTemplates, OrderManagement) rather than each page resolving "my business" on its own.

Two non-obvious bugs shaped this service, both worth knowing before adding another cross-cutting scoped service like it:
- **DbContext concurrency**: `NavMenu` (the layout) and the routed page both call `EnsureLoadedAsync`/load their own data from `OnInitializedAsync`, which can run concurrently. If `LoadAsync` queried through the shared per-circuit `EcoMealDbContext` the way most services do, it would race whatever query the routed page is running on that same context and crash the circuit (`InvalidOperationException: A second operation was started...`). It instead opens its own short-lived context via `IDbContextFactory<EcoMealDbContext>` — the identical fix `CartService.RestoreAsync` and `NotificationRepository`'s polling bell already use for the same reason.
- **Load-task caching, not a bool flag**: `EnsureLoadedAsync() => _loadTask ??= LoadAsync()` caches the in-flight `Task` itself rather than a `bool _loaded` flipped before the awaited load finishes. A bare bool would be a race in this exact layout-and-page-both-call-on-init scenario — if `NavMenu` starts the load and yields at an `await`, and the routed page checks the flag before `NavMenu` resumes, a bool would already read `true` and the page would read `MyBusinesses`/`SelectedBusinessId` before either was populated. Caching the `Task` means every caller, concurrent or not, awaits the same completion.

### NotificationPanelState

```csharp
public class NotificationPanelState(NotificationController notificationController) : IDisposable
{
    public bool IsOpen { get; private set; }
    public int UnreadCount { get; private set; }
    public List<Notification>? Notifications { get; private set; }
    public event Action? OnChange;

    public Task InitializeAsync() { /* cached in-flight Task, same pattern as ManagedBusinessContext */ }
    public async Task ToggleAsync() { /* flips IsOpen, fetches the list on open */ }
    public async Task MarkAllReadAsync() { /* ... */ }
    public async Task MarkAsReadAsync(Notification notification) { /* ... */ }
}
```
Exists to split `NotificationBell` (the trigger button, rendered inside the sidebar footer / public header) from `NotificationPanel` (the actual popup, rendered once at each layout's top level — §4) while keeping them driven by one source of truth. `NotificationBell.OnInitializedAsync` and `NotificationPanel.OnInitializedAsync` both subscribe to `OnChange` and both call `InitializeAsync()` — the cached-task pattern from `ManagedBusinessContext.EnsureLoadedAsync` means whichever runs second just awaits the first's poll-timer setup instead of starting a second timer. Marking a notification read, toggling open/closed, and mark-all-read all live on the service (not on either component) so the trigger's badge count and the panel's list/unread-accent state update in lockstep without a parent/child relationship between the two components — they aren't one, because they can't be (§4).

---

## 6. Home Page

**File:** `Components/Pages/Home.razor` — `@page "/"`, `@layout PublicLayout`

The storefront. Loads `_livePackages` (all packages with `PickupEnd > now`, for the hero's live stats) and the paginated business grid (`BusinessController.GetPagedAsync`) independently — search/filter/sort state (`_search`, `_businessTypeFilter`, `_dietaryTagFilter`, `_sortBy`, `_favoritesOnly`) all funnel through a single `Debouncer`-gated `ReloadAsync` (see §15) exactly like every other paginated admin list page in this app.

- **Search matches live packages, not just business fields** — see `BusinessRepository.GetPagedAsync` in `BACKEND_ARCHITECTURE.md` §4; the input has a 300ms debounce (`OnSearchInputAsync` passes `delayMs: 300` to the shared `Debouncer`) so it doesn't re-query on every keystroke.
- **Dietary/allergen filter** (Phase 10) — a second `<select>` next to the kitchen-type one, options built straight from `Constants.DietaryTags.All` (split into two `<optgroup>`s: dietary preference vs. the "Contains X" allergen warnings). Passed through as `dietaryTag` on `GetPagedAsync`; narrows to businesses with at least one live package carrying that tag, same server-side query shape as the kitchen-type filter.
- **"Closing soon" sort** — `BusinessSortOptions.ClosingSoon`, resolved server-side.
- **Favorites-only filter** — `AuthorizeView Roles="Customer"` around the toggle button; `ToggleFavoriteAsync` optimistically updates the local `_favoriteBusinessIds` set, then re-runs `ReloadAsync()` **only if** `_favoritesOnly` is currently active (unfavoriting a business while that filter is on would otherwise leave a stale card visible until the next unrelated reload).
- Ratings are batch-loaded per visible page (`ReviewController.GetByBusinessesAsync(businessIds)`, grouped into a `Dictionary<Guid, List<Review>>`) rather than one query per card — the same anti-N+1 shape `BACKEND_ARCHITECTURE.md` calls out for `GetByBusinessIdsAsync`.
- The hero's "portions to save" stat sums `CartService.AvailableQuantity(p, reservedByPackage.GetValueOrDefault(p.Id))` across `_livePackages`, where `_reservedByPackage` comes from one bulk `OrderController.GetPendingReservedQuantitiesAsync` call alongside the live-package fetch — without it the stat over-counted stock already tied up in other customers' Pending orders (see §15).
- Clicking a card calls `NavigationManager.NavigateTo($"/businesses/{id}")` — cards are rendered as `<button>` elements (not `<a>`) specifically so the per-card favorite-heart button can `@onclick:stopPropagation="true"` without fighting an anchor's default navigation.
- **"Closed now" badge** — `IsClosedNow(business)` calls `Models.BusinessHoursStatus.IsOpenNow(business.Hours, business.Closures, ClientTimeZoneService.ToLocal(DateTime.UtcNow))` (`BACKEND_ARCHITECTURE.md` §3) and only renders the badge when that's explicitly `false` — a `null` (hours never configured) or `true` result shows nothing, so a business that hasn't set hours yet never looks closed. `Home.razor` subscribes to `ClientTimeZoneService.OnChange` (same pattern `BusinessDetail.razor` already used, §7) so the badge re-evaluates once the browser's real timezone resolves via JS interop, not just on the initial UTC-default render.
- **Card photo placeholder** — `.biz-card-media`'s background-color fallback (`--em-forest`) has no failure event of its own, so a *set* `ImageUrl` that fails to load (unreachable host, no network egress) looked identical to a genuinely broken/empty card. `EcoMeal.media.checkPlaceholders` (§14) probes each card's `data-image-url` via a JS `Image()` and reveals a centered shop icon on failure; a `MutationObserver` re-runs it for cards that appear after pagination/filtering, since a one-time page-load scan would miss every page after the first.
- **Filters popover (UI revamp Phase 4)** — kitchen-type, sort, diet/allergen, Near me, Favorites, and the max-price chip all moved off the default-visible toolbar into an `.em-popover` (§13) gated by one `_filtersOpen` bool; the toolbar itself keeps only search, the Filters trigger, and Map view. `ActiveFilterCount` (a separate computed property from `HasActiveFilters`, which still gates "Clear all") deliberately excludes the search box from its count — the badge on the Filters button is meant to answer "how much is hidden behind this click," not "is anything non-default at all." None of the underlying filter state, bindings, or reload plumbing changed — every `@bind`/`@bind:after` call moved with its control, unmodified.

### AI search bar (Phase 2)

```csharp
private async Task RunAiSearchAsync()
{
    var result = await BusinessController.ParseSearchIntentAsync(_aiQuery, _lastSearchIntent);
    if (result.Result is ConflictObjectResult conflict)
        _aiSearchError = conflict.Value?.ToString() ?? "The AI search assistant isn't available right now.";
    else if (result.Value is not null)
        await ApplyIntentAsync(result.Value);
}

private async Task ApplyIntentAsync(SearchIntent intent)
{
    _lastSearchIntent = intent;
    _search = intent.Keywords ?? "";
    _dietaryTagFilter = intent.DietaryTag ?? "";
    _maxPriceFilter = intent.MaxPrice;
    if (intent.NearMe && !_customerLat.HasValue) await TryLocateAsync();
    _sortBy = intent.ClosingSoon ? BusinessSortOptions.ClosingSoon
        : intent.NearMe && _customerLat.HasValue ? BusinessSortOptions.Distance : BusinessSortOptions.Name;
    _pageIndex = 1;
    await ReloadAsync();
}
```
A second input above the literal search box (`.home-ai-search`), placeholder `"vegan dinner under 30 lei, closing soon"`, submitted on Enter (`OnAiSearchKeyDownAsync`) or the "Ask AI" button. `RunAiSearchAsync` calls `BusinessController.ParseSearchIntentAsync` in-process — same `ConflictObjectResult`-means-"AI not configured" convention `PackageForm.razor`'s `DraftDescriptionAsync` established (§ Packages) — then `ApplyIntentAsync` just **writes into the same filter fields the manual controls already bind to** (`_search`, `_dietaryTagFilter`, `_sortBy`), so the existing dropdowns visibly reflect what the AI understood and the "Clear" button already covers undoing it. `_maxPriceFilter` is new (no manual control sets it — only the AI search and its own dismissible chip, `.home-chip`, do) and threads through to `BusinessController.GetPagedAsync`'s new `maxPrice` parameter, filtering to businesses with a live package at or under that price (`BusinessRepository.GetPagedAsync`, `BACKEND_ARCHITECTURE.md` §4). `_lastSearchIntent` is fed back into the next `ParseSearchIntentAsync` call as refinement context, so "cheaper" or "gluten-free only" adjusts the prior turn instead of starting over (`BACKEND_ARCHITECTURE.md` §5). Only one sort slot exists in the UI, so when both `closingSoon` and `nearMe` are requested `closingSoon` wins it (no permission prompt needed, and it's the app's core food-waste signal) — `nearMe` still triggers a geolocation fetch either way, so the per-card distance badges (below) show up even when it loses that tie.

### AI budget/basket planner — `BasketPlanner.razor` (Phase 4)

A standalone page at `/plan-basket` rather than a widget bolted onto Home, since it's a
multi-step flow (form → AI proposal → per-item approve/decline → add to cart) rather than a
one-shot filter tweak like the AI search bar above. `@attribute [Authorize(Roles =
AppRoles.Customer)]`, `@layout PublicLayout`; linked from `PublicLayout.razor`'s header via a ✨
(`bi-stars`) icon shown only to a signed-in `Customer`, right next to the orders/basket icons.

```csharp
private async Task PlanAsync()
{
    if (_planning || _budget is not > 0 || _peopleCount < 1) return;

    _planning = true;
    _error = null;
    _plan = null;

    try
    {
        var result = await BasketPlannerController.ProposeBasketAsync(_peopleCount, _budget.Value,
            string.IsNullOrWhiteSpace(_dietaryTag) ? null : _dietaryTag);
        if (result.Result is ConflictObjectResult conflict)
            _error = conflict.Value?.ToString() ?? "The AI basket planner isn't available right now.";
        else if (result.Value is not null)
        {
            _plan = result.Value;
            _approved = _plan.Items.Select(i => i.Package.Id).ToHashSet();
        }
    }
    catch (Exception)
    {
        // Belt-and-braces alongside BasketPlannerController's own try/catch: a slow local model
        // can time out with a raw OperationCanceledException that isn't wrapped into a
        // ConflictObjectResult, which would otherwise leave _planning stuck true forever — spinner
        // spinning, button disabled, no feedback for the customer.
        _error = "The AI basket planner is taking too long to respond — please try again in a moment.";
    }
    finally
    {
        _planning = false;
    }
}
```

Same `ConflictObjectResult`-means-"AI not configured" convention as the AI search bar and
`PackageForm.razor`'s "Write it for me" button. The `try`/`catch`/`finally` is the belt-and-braces
half of that convention — `ConflictObjectResult` covers every failure `BasketPlannerAgent` itself
catches and reports, but a slow local Ollama model can time out with a raw, unwrapped exception
instead; without the wrapper here, that path left `_planning` stuck `true` with no error shown,
the spinner spinning forever. The form is just three inputs — headcount,
budget (RON), and an optional dietary/allergen `<select>` reusing the same two-`<optgroup>`
markup as Home's dietary filter — and `ProposeBasketAsync` (`BACKEND_ARCHITECTURE.md` §5/§6)
returns a fully-server-validated `BasketPlan`: every `Package` in it is real, so the page never
needs to re-fetch or re-check anything before rendering. Every item starts pre-checked in
`_approved` (a `HashSet<Guid>` of package IDs) — unchecking one is purely a client-side toggle,
recomputing `ApprovedTotal` — and "Add approved to basket" only then calls
`CartService.AddAsync(item.Package, item.Quantity)` per approved item, the same call
`BusinessDetail.razor`'s "Add" button makes. Since `BuildValidatedPlan` already guarantees every
surviving item shares one `BusinessId` (§5), the existing `CartService.WouldReplaceCart`/
`ConfirmDialog` "Start a new basket?" flow (§7) only ever needs to check the *first* approved
item, not one per item, before reusing the exact same dialog `BusinessDetail.razor` shows for a
manual add that would clear a basket from a different kitchen.

### "Near me" distance sort

```csharp
private async Task ToggleNearMeAsync()
{
    if (_sortBy == BusinessSortOptions.Distance) { _sortBy = BusinessSortOptions.Name; await FilterChangedAsync(); return; }

    _locatingNearMe = true;
    var located = await TryLocateAsync();
    _locatingNearMe = false;
    if (!located) return;

    _sortBy = BusinessSortOptions.Distance;
    await FilterChangedAsync();
}

private async Task<bool> TryLocateAsync()
{
    var position = await JSRuntime.InvokeAsync<GeoPosition?>("EcoMeal.geo.getPosition");
    if (position is null) { _locationError = "Couldn't get your location — check your browser's location permission."; return false; }
    (_customerLat, _customerLng) = (position.Lat, position.Lng);
    return true;
}
```
A toggle button, not a plain sort-dropdown option — selecting "distance" needs a customer coordinate the server doesn't have, so clicking it first requests browser geolocation (`EcoMeal.geo.getPosition`, §14) and only switches `_sortBy` once a position actually comes back. The geolocation request itself lives in `TryLocateAsync` (Phase 2 pulled it out of `ToggleNearMeAsync` so `ApplyIntentAsync` above could reuse it for a `nearMe` intent without duplicating the button's own toggle-off logic) — it resolves to `null` rather than throwing on denial/timeout/unsupported (§14), so the failure path here is an ordinary `if`, not a `try/catch` — the sort dropdown itself only shows a "Nearest" `<option>` once `_customerLat` is set, so there's no way to select a sort mode the page can't yet fulfill. `_customerLat`/`_customerLng` are passed straight through to `BusinessController.GetPagedAsync` on every subsequent reload while this sort is active — see `BusinessRepository.GetPagedAsync`'s in-memory Haversine branch in `BACKEND_ARCHITECTURE.md` §4. Each business card also shows its own "X km away"/"X m away" badge (`GeoDistance.Km`, computed client-side against `_customerLat`/`_customerLng` purely for display) whenever both the customer's position and that business's `Latitude`/`Longitude` are known — independent of which sort mode is active, so the badge can show up even while sorted by name (or while `nearMe` lost the AI search's sort tie-break above).

### Map view

```csharp
private async Task ToggleMapAsync()
{
    _showMap = !_showMap;
    if (!_showMap) return;
    _mapBusinesses ??= (await BusinessController.GetAllAsync()).Value?.Where(b => b.Latitude.HasValue && b.Longitude.HasValue).ToList() ?? [];
    _mapNeedsRender = _mapBusinesses.Count > 0;
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!_mapNeedsRender || _mapBusinesses is null) return;
    _mapNeedsRender = false;
    await JSRuntime.InvokeVoidAsync("EcoMeal.map.render", "home-map", _mapBusinesses.Select(b => new { id = b.Id, name = b.Name, lat = b.Latitude, lng = b.Longitude }));
}
```
A second view mode alongside the card grid, not a filter — toggling it on swaps the entire results section for a Leaflet map (`EcoMeal.map.render`, §14) and swaps back on toggle-off, rather than showing both at once. Deliberately plots **every** business with a saved location, ignoring the current search/type/favorites filters — a map is an overview tool, and a map that silently drops pins whenever a filter happens to be active would be more confusing than one that's always complete; it's fetched once (`_mapBusinesses ??= ...`) and cached for the rest of the page's lifetime rather than re-fetched per filter change like the card grid is. `OnAfterRenderAsync`'s `_mapNeedsRender` flag exists because the `<div id="home-map">` the JS call targets doesn't exist in the DOM until *after* the component re-renders with `_showMap = true` — calling the JS interop directly from `ToggleMapAsync` would run before that element is there. Each pin's popup is a plain `<a href="/businesses/{id}">` link (rendered by the JS side, §14) rather than a Blazor-circuit callback, since there's no benefit to routing a simple navigation back through C# just because the click originated inside a Leaflet-rendered marker.

### Impact leaderboard (Phase 11)

**File:** `Components/Pages/Impact.razor` — `@page "/impact"`, `@layout PublicLayout`, no `[Authorize]` (same "public page, role-gated fragments" shape as `Home.razor`/`BusinessDetail.razor` — see §3).

A second public page under `PublicLayout`, linked from a trophy icon in the header (both the `Authorized` and `NotAuthorized` branches of `PublicLayout.razor`, so it's reachable whether or not you're signed in — see §4). `OnInitializedAsync` fires two independent reads: `ImpactController.GetMonthlyLeaderboardAsync()` (always, anonymous or not) and, only `if (authState.User.IsInRole(AppRoles.Customer))`, `ImpactController.GetMyOptInStatusAsync()` for the opt-in toggle's initial state — an admin/manager/anonymous visitor sees the ranked list with no toggle at all, since only a customer can ever have a Completed order to rank in the first place.

The opt-in toggle itself is a plain Bootstrap `form-check form-switch` bound via `@onchange` (not `@bind`, since flipping it needs an immediate `ImpactController.SetMyOptInStatusAsync` round-trip before the UI reflects the new value) rather than an `EditForm`/`InputCheckbox` — there's exactly one field, so the extra ceremony wouldn't earn its keep. Toggling re-fetches the whole leaderboard afterward (not just flips a local flag) because opting in/out can change whether the viewer's own row should now appear or disappear, not just relabel an existing one.

Each row highlights the signed-in viewer's own entry (`entry.UserId == _currentUserId`, resolved the same `ClaimTypes.NameIdentifier`-off-the-auth-state way `Home.razor`'s `ToggleFavoriteAsync` and `Users.razor`'s self-row detection do) with a `.impact-row-me` outline and a small "You" pill — same visual language `Users.razor` uses for "this is you" in the admin user list. The top three ranks get a gold/silver/bronze color accent (`RankClass`, pure `index switch`, no server involvement) purely as a presentation nicety over the same ordered list `ImpactController` already returned sorted.

An opted-out user with real order history simply never appears in `_leaderboard` at all — `OrderRepository.GetTopRescuersAsync` filters server-side on `ApplicationUser.ShowOnLeaderboard` (`BACKEND_ARCHITECTURE.md` §4), so there's no client-side "hide anonymous entries" logic to get wrong; the row is never sent to the browser in the first place.

**Impact Phase 1** added a platform-wide kg-saved figure to the hero (`OrderController.GetTotalKgSavedAsync()`, the same call `Home.razor`'s hero already made) plus `<ImpactEquivalencyStats KgSaved="@_totalKgSaved.Value"/>` right underneath it — see below.

**Streak column (Phase 16)**: each row shows a small `bi-fire` pill next to the name whenever `entry.StreakWeeks > 0` — `ImpactController.GetMonthlyLeaderboardAsync` already returns it filled in (`BACKEND_ARCHITECTURE.md` §5 ImpactService), so this page just renders the field, no extra fetch.

### ImpactEquivalencyStats (Impact Phase 1)

**File:** `Components/Shared/ImpactEquivalencyStats.razor` — one `[Parameter, EditorRequired] decimal KgSaved`, renders nothing at all when `KgSaved <= 0`.

The one new shared component this phase added: turns a raw kg-saved number into two plain-English equivalencies — "~N km not driven" and "~N L of water saved" — computed via `Constants/ImpactEquivalency.cs`'s static helpers so every caller agrees on the same conversion. Two call sites: `Impact.razor`'s hero (platform-wide total, above) and `OrderDetailModal` (a specific order's own total, below) — deliberately *not* `PaymentReturn.razor`'s confirmation screen or `OrderPickupPass.razor`'s ticket, which already had their own single-line "~N kg saved" impact chip (`.cart-confirmation-impact`/`.order-ticket-impact`) from earlier phases; adding a second, more detailed component there would be redundant rather than additive. Styled as small pill chips (`.impact-equivalency`/`.impact-equivalency-item`, `wwwroot/app.css`) visually related to but distinct from those two older single-stat chips.

`OrderDetailModal` (§9 below) only renders it `@if (Order.Status.Name == OrderStatuses.Completed)` — a Pending/Confirmed/Cancelled/NoShow order hasn't actually saved anything yet (or, for NoShow, never will), so showing an equivalency for it would overstate real impact.

---

## 7. Business Detail Page

**File:** `Components/Pages/BusinessDetail.razor` — `@page "/businesses/{Id:guid}"`, `@layout PublicLayout`

Three independent concerns on one page: the business's live packages, its reviews, and (for customers) favoriting + cart actions.

- **Cross-business basket guard**: `AddToCart(package)` checks `CartService.WouldReplaceCart(package)` first; if true, it stashes the package in `_pendingSwitch` and shows a `ConfirmDialog` ("Start a new basket?") instead of adding immediately — confirming calls `CartService.AddAsync` and clears the old basket as a side effect (documented in `CartService.AddInternal`, §5). This exact pattern is reused verbatim by `Orders.razor`'s reorder feature (§9).
- **Review gating**: `_reviewContext.CanReview` (from `ReviewController.GetContextAsync`) controls whether the review form renders at all vs. a "order from X and come back to leave a review" hint — the *actual* enforcement is server-side in `ReviewService.SubmitAsync`; the client-side gate is purely to avoid showing a form that would just reject on submit.
- **Package-level review tag**: the form's package `<select>` only renders when `_reviewContext.ReviewablePackages.Count > 0`, bound through a plain `_reviewPackageIdText` string (parsed with `Guid.TryParse` on submit) rather than directly to a `Guid?` — same reason `Home.razor`'s business-type filter (§6) does this instead of binding a `Guid?` straight to a plain `<select>`. `ReloadReviewsAsync` prefills it from `_reviewContext.MyReview?.PackageId` the same way it already prefills `_reviewRating`/`_reviewComment`. Each review card shows `review.Package.Name` as a small pill when `review.PackageId` isn't null (the repository call already includes `Package`, §4), and `PackageReviews(packageId)`/`PackageRatingAverage(packageId)` filter the page's already-loaded `_reviews` client-side — no extra query — to feed `PackageDetailModal`'s own rating display (§12).
- **Package list is pre-filtered to live packages** client-side after the paginated fetch (`PickupEnd > DateTime.UtcNow`) and re-sorted by soonest `PickupEnd` — the backend's `GetPagedAsync` for packages doesn't have a "live only" filter of its own, so this page asks for a large page size (100) and filters in memory rather than adding a new backend parameter for a single call site.
- **"X left" accounts for pending reservations, not just the local cart** — right after loading `_packages`, the page fetches `_reservedByPackage` (one bulk `OrderController.GetPendingReservedQuantitiesAsync` call) and passes each package's reserved total into `CartService.AvailableQuantity(package, reserved)`, both in the package row and in the `PackageDetailModal` parameter. Before this existed, `AvailableQuantity` only knew about the *viewer's own* local cart contents, so a package's displayed count would revert to the full, un-reserved number the moment a Pending order's items left the local cart (e.g. right after checkout) — misleading regardless of whether the reservation was the viewer's own order or someone else's (see §15).
- **Live stock updates (Phase 12)**: a "LIVE" badge (`.biz-live-badge`, reusing `.biz-card-live-dot`'s pulse styling from the Home.razor kitchen cards) sits next to the "What's available" heading. The page subscribes to `PackageStockBroadcaster.BusinessStockChanged` in `OnInitialized` — same `OnChange`-event + `IDisposable`-unsubscribe idiom as the `CartService`/`ClientTimeZoneService` subscriptions right above it, except this one is a *singleton* event fired from `OrderService`/`PackageService` (`BACKEND_ARCHITECTURE.md` §5) whenever any circuit's action changes this business's effective stock — order placement/confirm/cancel/no-show, a manager editing/hiding/restocking a package, and both background-sweep expiries. The handler filters events to its own `Id`, then re-runs the same `LoadPackagesAsync()` the initial load uses (packages + `_reservedByPackage`) via `Task.Run(() => InvokeAsync(...))`, so a package selling out from under a viewer updates the row (and `PackageDetailModal` if it's the currently-open one — closes itself if the package dropped out of the live list entirely) with no page refresh and no polling. The `Task.Run` wrapper matters: the broadcast can be raised by *this same circuit* (starting a Rescue Circle fires it from underneath the cart panel, which overlays this very page) — `InvokeAsync` alone would then run inline instead of queuing, since it's already on the dispatcher thread, racing this reload's `DbContext` use against the order-placing code's own still-in-flight one (`BACKEND_ARCHITECTURE.md` §5 `PackageStockBroadcaster`).
- `PackageDetailModal` (§12) is the drill-down when a package row is clicked, reusing the same `AddToCart` handler.
- **Closing-soon badge (Phase 10)**: `ClosingSoonBadge(package)` returns `"Ends in N min"` whenever `PickupEnd - DateTime.UtcNow` is positive and under an hour — the same threshold `Home.razor`'s "Closing soon" sort cares about, just surfaced per package instead of only affecting order. Rendered inline in the package row's meta line, and passed through to `PackageDetailModal` as `ClosingSoonLabel` so the drill-down shows it too.
- **Phase 9 visibility gate**: `_business` is only assigned when the fetched business is `{ Status: Approved, IsHidden: false }` — a `PendingApproval`/`Rejected`/hidden business renders the identical `NotFoundPanel` a genuinely deleted one does, rather than a distinct "not available yet" state. This is a deliberate simplification: the applicant never gets a preview link at all (`BusinessApply.razor`, below, shows a plain confirmation instead of redirecting here), and staff/admin already have `/businesses`/`/businesses/edit/{id}` to inspect a business regardless of its status — so this page doesn't need to special-case either audience. The package list also filters out `p.IsHidden` alongside the existing `PickupEnd > now` check.
- **Report button (Phase 9)**: `AuthorizeView Roles="Customer"` gates a flag-icon button next to Favorite, opening `ReportDialog` (§12) and calling `ReportController.SubmitAsync(AuditTargetTypes.Business, Id, reason)` on submit — success shows the same inline toast (`_toastMessage`) the "added to basket"/"started a new basket" flows already use, rather than a separate confirmation UI. `PackageDetailModal` (§12) carries an identical report action for the package itself, independent of this one.
- **Opening hours panel**: renders only when `_business.Hours.Count > 0 || _business.Closures.Count > 0` — a business with neither gets no section at all rather than an empty one. `ActiveClosure` (same `BusinessHoursStatus.ActiveClosure` call as the `OpenNow` badge next to the address) drives a banner above the weekly list when a holiday closure currently covers today; the weekly list itself always shows all seven `WeekOrder` days regardless of whether every one has an hours row, with today's row (`day == ClientTimeZoneService.ToLocal(DateTime.UtcNow).DayOfWeek`) highlighted and a missing/`IsClosed` row rendered as "Closed" (`HoursLabel`).
- **Loyalty progress banner (Phase 14)**: `AuthorizeView Roles="Customer"` gates a small inline banner right under the description, populated by `LoyaltyController.GetMyProgressAsync(Id)` in `OnParametersSetAsync` — renders nothing when the controller returns `null` (anonymous, or the business has no punch card configured), otherwise "N more completed orders this month unlock X lei off your next order here!", singularized for `OrdersUntilReward == 1`. Purely informational — the actual discount is applied server-side at checkout (`CheckoutService`, `BACKEND_ARCHITECTURE.md` §5), this banner never itself triggers anything.
- **Standing order section (Phase 14)**: a customer-only section (`AuthorizeView Roles="Customer"`) between the package list and Reviews. `_myStandingOrders` (loaded once via `StandingOrderController.GetMyStandingOrdersAsync()` alongside the loyalty banner) is filtered client-side to `s.BusinessId == Id` — no per-business backend query exists or is needed, since a customer's total standing-order count is small. No existing standing order for this business renders a compact inline create form (optional package-type `<select>` sourced from `_businessPackageTypes` — the distinct `PackageType`s among `_packages`, not a fresh query — optional dietary-tag `<select>` from `Constants.DietaryTags.All`, and a weekly-budget number input); an existing one renders its summary plus Pause/Resume/Remove buttons instead. `CreateStandingOrderAsync`/`ToggleStandingOrderAsync`/`DeleteStandingOrderAsync` mutate `_myStandingOrders` in place after each call rather than re-fetching the whole list — `CreateStandingOrderAsync` also patches the freshly-created entity's `PackageType` nav from `_businessPackageTypes` first, since `CreateAsync`'s response doesn't eager-load it and the summary would otherwise show "Any package" until the next reload.
- **Kitchen tips section (Phase 16)**: right below Reviews, not gated on having ordered there (unlike the review form above) — any signed-in customer sees the submit form, everyone sees the list. `SubmitTipAsync` posts through `KitchenTipController.SubmitAsync` and prepends the result to `_kitchenTips` via a fresh `ReloadKitchenTipsAsync` call (the backend already returns newest-first). Each tip row carries its own flag icon reusing the *same* `ReportDialog`/`_reportPromptOpen` state the business-level report button (below) already owns — `RequestReportTip(tipId)` sets a `_reportTipId` field before opening the dialog, and `SubmitReportAsync` branches on whether it's set to call `ReportController.SubmitAsync` with `AuditTargetTypes.KitchenTip`/that tip's id instead of `AuditTargetTypes.Business`/`Id`, so a single dialog instance covers reporting the kitchen or any one of its tips without a second component.
- **Brand pill (Phase 17)**: renders next to the address whenever `_business.Brand is not null` (the repository include added for this — see `BACKEND_ARCHITECTURE.md` §4), linking to `/brands/{brandId}`.
- **Auto-reserve from a standing-order match**: `[SupplyParameterFromQuery(Name = "reserve")] Guid? ReservePackageId` — the query parameter a `StandingOrderService.MatchNewPackagesAsync` notification link (`/businesses/{id}?reserve={packageId}`) carries. `TryAutoReserveAsync`, called once packages and cart-reservation data are loaded, adds that exact package to `CartService` (the same `AddAsync` the "Add" button calls) provided it isn't already in the cart and wouldn't trigger the cross-business basket-replace guard above — silently does nothing otherwise, rather than surfacing an error for what's just a stale/already-acted-on link. Shows the same inline toast pattern as every other basket action ("Reserved your usual — ... is in your basket."). This is the client-side half of why standing orders never need a server-side "pending reservation" of their own — see `BACKEND_ARCHITECTURE.md` §3 StandingOrder for the full reasoning (`Order` only exists post-payment, so there's nothing to reserve server-side ahead of checkout).

### StandingOrders — every kitchen's saved "usuals" in one place (Phase 14)

**File:** `Components/Pages/StandingOrders.razor` — `@page "/standing-orders"`, `@layout PublicLayout`, `[Authorize(Roles = "Customer")]`

The management counterpart to Business Detail's inline create form — same table-with-Pause/Resume/Remove shape `PackageTemplates.razor` (§11) uses for recurring templates, just customer-facing and with no business-scoping (`StandingOrderController.GetMyStandingOrdersAsync()` already returns only the caller's own rows). Deleting asks for confirmation via the shared `ConfirmDialog` (§12) — pausing/resuming doesn't, since it's non-destructive and instantly reversible from the same row. Reachable from the public header's 🔁 icon (`PublicLayout.razor`, next to the Rescue Circles icon) for any signed-in customer, and from the notification link a standing-order match fires (which lands on Business Detail instead, per the bullet above, not here).

### Referrals — invite link, store credit, invite history (Phase 16)

**File:** `Components/Pages/Referrals.razor` — `@page "/referrals"`, `@layout PublicLayout`, `[Authorize(Roles = "Customer")]`

A single `ReferralController.GetMyReferralInfoAsync()` call in `OnInitializedAsync` backs the whole page — no per-section fetches. The referral link itself is built client-side (`NavigationManager.BaseUri.TrimEnd('/') + "/account/register?ref=" + code`, same "derive the origin from the manager's own request" trick `Dashboard.razor`'s "share your impact" snippet uses) rather than returned by the backend, since only the frontend knows what host/port it's actually being served from (a bare `dotnet run`, this docker-compose setup, or a real domain). The copy button reuses the exact inline `onclick` pattern (`select()` the input, `navigator.clipboard.writeText`, flip the button text to "Copied!" for 1.5s) `Dashboard.razor`'s widget-snippet copy button already established, rather than a new JS interop call. The invite table renders a `"Reward earned"`/`"Waiting on first order"` pill per row straight off `ReferralRow.Rewarded` — no separate polling or refresh needed, since a reward can only land between page loads (it's granted server-side the moment a referred friend's order completes, `BACKEND_ARCHITECTURE.md` §5 ReferralService).

### Brands / BrandDetail — chain grouping (Phase 17)

**Files:** `Components/Pages/Brands.razor` (`/brands`), `Components/Pages/BrandDetail.razor` (`/brands/{Id:guid}`) — both `@layout PublicLayout`, no `[Authorize]`.

`Brands.razor` is a plain card grid over `BrandController.GetAllAsync()`, filtered client-side to brands with at least one location — nothing worth clicking into otherwise. `BrandDetail.razor` does the real work: one `BrandController.GetPublicDetailAsync(Id)` call backs the header (aggregate `StarRating`, location count), and `OrderedLocations` sorts the location list by `(open-now, distance, name)` — `BusinessHoursStatus.IsOpenNow` (`BACKEND_ARCHITECTURE.md` §3) per location, plus `GeoDistance.Km` against the customer's own position if `EcoMeal.geo.getPosition()` resolved (same `OnAfterRenderAsync`-deferred, 10-second-timeout pattern `TripPlanner.razor` and `BusinessForm.razor`'s location picker already use). `NearestOpenLocation` is that same ordering's first `OpenNow == true` result, surfaced as a green "Nearest location open now" banner linking straight to `/businesses/{id}` — the page's one payoff, and the reason geolocation is asked for at all here. The favorite button reuses `BusinessDetail.razor`'s exact heart-icon toggle pattern, just against `BrandController.ToggleFavoriteAsync`/`GetMyFavoriteBrandIdsAsync` instead of the `Favorite` equivalents.

### BusinessApply — self-service business signup (Phase 9)

**File:** `Components/Pages/BusinessApply.razor` — `@page "/businesses/apply"`, `@layout PublicLayout`, `[Authorize(Roles = "Customer,BusinessManager")]`

A deliberately small form — Name/Description/Address/Type/ImageUrl, the same fields `BusinessForm.razor` exposes minus Location — that calls `BusinessController.ApplyAsync` (not `AddAsync`) on submit. There is **no redirect to a detail page and no "my applications" list** — on success the form is replaced in place by a plain confirmation card ("An admin will review 'X' and let you know once it's approved"), specifically so this page never needs to reason about rendering a `PendingApproval`/`Rejected` business's own detail view (see the Business Detail visibility gate above). The applicant instead learns the outcome via the in-app notification `BusinessService.ApproveAsync`/`RejectAsync` sends server-side. Reuses the same `EditForm`/`DataAnnotationsValidator`/private nested form-model pattern every other admin `EditForm` page in this app uses (§1), just under `PublicLayout` instead of `MainLayout` since the audience is a signed-in customer, not staff.

---

## 8. Cart Panel & Checkout

**File:** `Components/Shared/CartPanel.razor`

A slide-in `<aside>` rendered by `PublicLayout`, controlled by a two-way-bound `IsOpen` parameter (`@bind-IsOpen="_cartOpen"` in the layout, toggled by the header's basket button). Two states: empty basket, or an active basket (line items with `+`/`-`/remove, running total, "Pay & place order"). There's no order-confirmation state inside this component anymore — placing an order now means leaving the app entirely for Stripe's hosted Checkout page, so the confirmation screen lives on its own page after the redirect back (`PaymentReturn.razor`, below), not inside the slide-in panel.

```csharp
// Sends the browser off to Stripe's hosted Checkout page — the cart itself is only cleared
// once payment is confirmed on the /checkout/return page (see PaymentReturn.razor), since the
// Order (and this basket) shouldn't be considered placed until then.
private async Task StartCheckoutAsync()
{
    if (CartService.BusinessId is null || _placing) return;
    _placing = true;

    var lines = CartService.Items.Select(i => new OrderLineRequest(i.Package.Id, i.Quantity)).ToList();
    var result = await PaymentController.CreateCheckoutSessionAsync(CartService.BusinessId.Value, lines);

    if (result.Result is ConflictObjectResult conflict)
    {
        _placing = false;
        _error = conflict.Value?.ToString() ?? "We couldn't start checkout. Please try again.";
        return;
    }

    Navigation.NavigateTo(result.Value!, forceLoad: true);
}
```
The `ConflictObjectResult` check is exactly how the rate-limit error, stock-conflict errors, "payments aren't configured yet," and any other `CheckoutService.StartCheckoutAsync` exception surface to the customer — one `if`, no exception-type-specific handling needed client-side, because `PaymentController.CreateCheckoutSessionAsync` already collapsed every relevant exception into `Conflict(ex.Message)` (see `BACKEND_ARCHITECTURE.md` §6, §5 CheckoutService). On success, `result.Value` is Stripe's own hosted checkout URL — `forceLoad: true` is required here, not stylistic, since this is a genuine cross-origin navigation off the Blazor circuit entirely, not an in-app route a soft navigation could handle. The cart itself is left untouched at this point; it's only cleared once the customer actually comes back from Stripe having paid.

**Logistics note (Phase 15)**: a plain `<textarea @bind="_logisticsNote">` sits right above the Pay button, `maxlength` set from `Constants.OrderLogistics.MaxNoteLength` for an immediate browser-side cap ahead of the server's own trim/clamp. `_logisticsNote` is passed straight through as the last argument to both `PaymentController.CreateCheckoutSessionAsync` (above) and `RescueCircleController.StartCircleAsync` (below) — one field backs whichever button the customer actually presses, since a basket can only go through one checkout path at a time.

### PaymentReturn / PaymentCancel — the Stripe redirect landing pages

**Files:** `Components/Pages/PaymentReturn.razor` (`/checkout/return`), `Components/Pages/PaymentCancel.razor` (`/checkout/cancel`) — both `@layout EmptyLayout`, reusing the same `.login-page`/`.login-card`/`.cart-confirmation` CSS shapes the auth pages and the old in-panel confirmation used, so the visual language doesn't shift just because the flow moved pages.

`PaymentReturn.razor` is where `CheckoutService.CompleteCheckoutAsync` (`BACKEND_ARCHITECTURE.md` §5) actually gets called from, driven by the two query-string parameters Stripe's `successUrl` was built with:
```csharp
[SupplyParameterFromQuery(Name = "pc")] public Guid? Pc { get; set; }
[SupplyParameterFromQuery(Name = "session_id")] public string? SessionId { get; set; }

protected override async Task OnInitializedAsync()
{
    if (Pc is null || string.IsNullOrWhiteSpace(SessionId)) { _loading = false; return; }

    var result = await PaymentController.CompleteCheckoutAsync(Pc.Value, SessionId);
    if (result.Result is UnauthorizedResult) { _message = "This payment doesn't belong to your account."; }
    else if (result.Value is { Success: true } completion)
    {
        _success = true;
        _order = completion.Order;
        _kgSaved = completion.KgSaved;

        // The basket that was checked out is now a real order — clear it so it doesn't
        // linger and get re-submitted from a stale browser tab.
        await CartService.RestoreAsync();
        await CartService.ClearAsync();
    }
    else if (result.Value is not null) { _message = result.Value.Message; }

    _loading = false;
}
```
Three render states: a "Confirming your payment…" spinner while the `CompleteCheckoutAsync` round-trip is in flight, the success screen (order number + kg-saved impact stat, mirroring what used to live in `CartPanel`'s old confirmation state), or an error message with a "Back to browsing" link. `CartService.RestoreAsync()` is called before `ClearAsync()` — unlike every other `CartPanel`/`Home`/`BusinessDetail` call site, this page can be the very first one to render in a fresh circuit (the customer left the app entirely for Stripe and came back via a real HTTP redirect), so `CartService`'s in-memory state isn't already populated the way it would be on an in-app navigation; restoring first is what makes `ClearAsync()` actually clear something instead of a no-op. `_kgSaved` here **is** the authoritative figure by this point (`completion.KgSaved`, computed server-side by `CheckoutService` from the just-placed `Order`), unlike the old client-side-computed version this page replaced.

`PaymentCancel.razor` is comparatively trivial — no query params, no service calls, just a static "Payment cancelled, nothing was charged, your basket is still saved" message with a link back to `/`. Stripe redirects here when the customer backs out of the hosted Checkout page instead of completing it; the cart survives because nothing about it was ever touched — `StartCheckoutAsync` above never clears the basket itself, only a confirmed `PaymentReturn.razor` success does.

### Rescue Circles — splitting an order with friends (Phase 13)

**Files:** `Components/Shared/CartPanel.razor` (the starter), `Components/Pages/RescueCircleList.razor` (`/circles`), `Components/Pages/RescueCircleInvite.razor` (`/circles/{Id}`), `Components/Pages/RescueCircleReturn.razor` (`/circles/return`).

`CartPanel`'s "Pay & place order" button gets a sibling link, "Split this with friends instead" (`_splitOpen`), that swaps the button for a small `.rescue-circle-starter` panel: a people-count stepper clamped to `Constants.RescueCircles.MinParticipants`/`MaxParticipants` (2–6) with a live "≈ X each" estimate (`CartService.TotalPrice / _participantCount`, client-side only — the real per-share rounding happens server-side, §5 RescueCircle in `BACKEND_ARCHITECTURE.md`), and a "Start & pay my share" button calling `RescueCircleController.StartCircleAsync`. Unlike a solo checkout, this clears the basket **immediately** rather than waiting for a return-page confirmation:
```csharp
await CartService.ClearAsync();
Navigation.NavigateTo(result.Value!, forceLoad: true);
```
because the underlying `Order` is placed the moment the circle starts (`RescueCircleService.StartCircleAsync`) — the group hasn't finished paying yet, but the basket itself has already become a real (`Pending`) order, unlike a solo checkout where the `Order` doesn't exist at all until `PaymentReturn.razor` confirms payment.

`RescueCircleReturn.razor` (`@layout EmptyLayout`, same `.login-page`/`.cart-confirmation` shapes as `PaymentReturn.razor`) is where `RescueCircleService.CompleteShareCheckoutAsync` gets called from, driven by the `circle`/`participant`/`session_id` query params the success URL was built with. Same three render states as `PaymentReturn.razor` (confirming spinner / success / error), except the success copy branches on whether the *whole circle* is now fully paid ("Everyone's paid — the kitchen will confirm the order soon") or just this one share ("N of M people have paid so far — share the link so the rest can chip in"), and either way it links back to `/circles/{Id}` rather than an order-history page — there's no basket to clear here since `CartPanel` already did that when the circle started.

`RescueCircleInvite.razor` (`/circles/{Id}`) is the page the invite link itself points at, reachable by any signed-in customer — not just participants — since deciding "should I join?" needs a public-enough summary. It calls `RescueCircleController.GetDetailAsync` first (the full per-person paid/unpaid breakdown, organizer/participant-only) and falls back to `GetSummaryAsync` (participant count/total/status only, no names) on a `Conflict()` — the same shape `RescueCircleService`'s two read methods enforce server-side (`BACKEND_ARCHITECTURE.md` §5). The page then renders one of three states depending on the viewer's own relationship to the circle: an unpaid participant sees a "pay share" button, with a non-organizer also offered a "leave" option beside it (refunded if they'd already paid, simply removed if not — leaving while unpaid needs no refund but still frees the slot); a non-participant sees "join an open slot and pay" if the circle isn't full yet; and everyone else (full/cancelled/already fully paid) sees nothing actionable. An organizer can never leave — only cancel the whole order, which is how everyone who's paid gets refunded. `RescueCircleList.razor` (`/circles`) is the plain list of every circle the current customer organizes or has joined (`GetMyCirclesAsync`), rendered as the same `.order-ticket` cards `Orders.razor` uses, each linking into its own `RescueCircleInvite.razor`.

---

## 9. Orders Page & Reorder

**File:** `Components/Pages/Orders.razor` — `@page "/orders"`, `@layout PublicLayout`

Customer order history: a lifetime-stats hero (orders placed / portions rescued / kitchens visited / kg saved / week streak — computed from an **unfiltered** full order list loaded once, the streak the one exception since it comes from its own `StreakController.GetMyStreakWeeksAsync()` call instead of being derived from `_orders` client-side) above a paginated, status-filterable ticket list (a **separate**, server-paged query — `OrderController.GetMyOrdersPagedAsync`). Both loads are necessary because the hero's totals must reflect *all* of a customer's history regardless of which status filter chip is currently active on the visible list.

**Rescue-streak stat (Phase 16)**: a `bi-fire` icon appears next to the number only once it's non-zero — a `0`/`—` streak (loading) shows plain text with no flame, so the icon reads as "on a streak" rather than decoration on every possible value.

**Ticket total**: shows `order.Payment.Amount` (what Stripe actually charged, post loyalty/store-credit discount) rather than the raw `OrderPackages` subtotal, falling back to the subtotal only when there's no `Payment` yet (e.g. still-`Pending`). A small "(X before discount)" note appears alongside whenever the two differ, so a customer reconciling a card statement sees the number that actually matches it. `OrderDetailModal` (§12) shows the identical total for the same reason — its payment-status badge already surfaces `Payment.Amount`, so the modal's own total used to show a second, different figure whenever a discount applied.

### Reorder ("Order again")

Appears only on `Completed`/`Cancelled` tickets (mirroring the existing `Pending`/`Confirmed` action block, which shows Cancel/QR-code instead):

```csharp
private async Task RequestReorderAsync(Order order)
{
    var stillLive = order.OrderPackages.Any(op => op.Package.PickupEnd > DateTime.UtcNow && op.Package.Quantity > 0);
    if (!stillLive) { _reorderMessage = "None of the items from this order are available right now."; return; }

    if (CartService.TotalCount > 0 && CartService.BusinessId != order.BusinessId)
    {
        _pendingReorder = order;   // triggers the same "start a new basket?" ConfirmDialog as BusinessDetail
        return;
    }

    await AddReorderLinesAsync(order);
}

private async Task AddReorderLinesAsync(Order order)
{
    foreach (var line in order.OrderPackages)
    {
        if (line.Package.PickupEnd <= DateTime.UtcNow || line.Package.Quantity <= 0) { skippedCount++; continue; }
        await CartService.AddAsync(line.Package, line.Quantity);  // clamps to whatever's actually available
        addedCount++;
    }
    // message: "Added N item(s) ... — M no longer available" when a partial reorder happened
}
```
This is a **pure frontend feature** — no new `OrderService` method exists for it (see `BACKEND_ARCHITECTURE.md` §5). It works because `CartService.AddAsync` already clamps quantity to whatever's currently in stock, so re-adding a stale order's requested quantities is safe by construction; the page's own job is just deciding which lines are even worth attempting (still live, still in stock) and reporting how many were skipped. `addedCount` counts distinct **lines**, not total units — reordering one line of quantity 2 reports "Added 1 item," which is a deliberate simplification (line-level, not unit-level) rather than a bug.

### Cancel & pickup pass

Both `Pending` and `Confirmed` tickets show a "Cancel order" button (`ConfirmDialog`-gated); `Confirmed` additionally shows a "Show QR code" link to `/orders/pickup/{id}` (§10). Cancelling refreshes **both** the unfiltered hero stats and the currently-visible paged list, since a cancellation can move an order out of whatever status filter is active.

### TripPlanner — multi-stop pickup routing (Phase 15)

**File:** `Components/Pages/TripPlanner.razor` — `@page "/trip-planner"`, `@layout PublicLayout`.

Groups the customer's own `Pending`/`Confirmed` orders (loaded the same way `Orders.razor`'s hero stats are, via `OrderController.GetMyOrdersAsync`) into one stop per `BusinessId` — an active order can't span businesses — then hands each stop's `Business.Latitude`/`Longitude` to `Models.TripPlanner.PlanRoute` (`BACKEND_ARCHITECTURE.md` §5) for a nearest-neighbor walk order:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender && _stops.Count > 1)
    {
        var position = await JSRuntime.InvokeAsync<GeoPosition?>("EcoMeal.geo.getPosition");
        if (position is not null) { _lat = position.Lat; _lng = position.Lng; BuildStops(); }
        else { _locationDenied = true; }
    }

    if (_mapNeedsRender)
    {
        var markers = _stops.Select((s, i) => new { id = s.BusinessId, name = $"{i + 1}. {s.BusinessName}", lat = s.Lat, lng = s.Lng });
        await JSRuntime.InvokeVoidAsync("EcoMeal.map.render", "trip-map", markers);
    }
}
```
Geolocation is only requested once there's more than one stop to actually route — a single active order has nothing to plan a route between, so there's no reason to prompt for location permission (§15 "Geolocation always degrades, never blocks" applies here too: a denial just falls back to starting from the first stop). The resulting order renders as a numbered `<ol class="list-group-numbered">` next to a `home-map`-styled Leaflet map (§6 Map view) — reusing `EcoMeal.map.render` unmodified, since prefixing each marker's `name` with its stop number ("1. Stadionul de Gusturi") was enough to show the sequence without touching the shared JS at all. A stop with several orders against the same business lists each one's `#order-number`/status underneath, rather than the trip planner picking one to show.

---

## 10. Pickup Pass, QR Scan & Validate

A three-page flow that hands a physical pickup confirmation off to a QR code, deliberately designed so each page works correctly no matter how the visitor actually arrived at it. Since the group-pickup-pass rework (see `BACKEND_ARCHITECTURE.md` §3 OrderPickupPass), an order can have more than one pass — the QR payload and the validate route both carry a pass ID alongside the order ID, and redeeming *any one* pass completes the whole order.

```
Customer's phone                          Manager's phone/device
─────────────────                          ──────────────────────
OrderPickupPass.razor
  (Confirmed order only)
  tab-switcher if >1 pass, each generating
  its own SVG QR encoding
  {BaseUri}/orders/validate/{orderId}/{passId}
        │
        │  customer shows screen to counter
        ▼
                                           OrderScan.razor
                                             live camera → jsQR decode (client-side loop)
                                             successful decode → location.assign(url)
                                                   │
                                                   ▼
                                           OrderValidate.razor
                                             re-checks auth + order ownership itself
                                             "Confirm pickup" → redeems this pass, order → Completed
```

### OrderPickupPass — QR generation and the pass switcher

Server-side, via the `QRCoder` NuGet package, regenerated whenever the selected pass changes:
```csharp
var payloadUrl = $"{NavigationManager.BaseUri}orders/validate/{_order!.Id}/{pass.Id}";
var qrData = new QRCodeGenerator().CreateQrCode(payloadUrl, QRCodeGenerator.ECCLevel.Q);
_qrSvg = new SvgQRCode(qrData).GetGraphic(5, "#0b1f13", "#ffffff", true, SvgQRCode.SizingMode.ViewBoxAttribute);
```
Rendered inline via `@((MarkupString)_qrSvg!)`. Only shown when `Order.Status == Confirmed` — every other status renders an explanatory empty state instead (`StatusExplanation` switch) rather than a broken/stale QR code. A Confirmed order with more than one pass shows a row of `.pickup-pass-tab` buttons above the ticket (labeled "Pass 1", "Pass 2"...) — clicking one swaps `_selectedPass` and regenerates the QR client-side, no server round-trip needed since the SVG is cheap to recompute. Below the ticket, "Splitting with a group? Get separate passes" reveals a `<select>` (1–`PickupPasses.MaxPasses`) + "Update passes" button calling `OrderController.SplitPickupPassesAsync`, then reloads the order and resets the selected pass to the first one. **Phase 13**: a Rescue Circle order arrives already split — each tab is labeled with the participant's own name ("Alex's pass") rather than "Pass 1"/"Pass 2", since `OrderService.ApplyStatusChangeAsync` generated them per participant automatically on confirm; the organizer never needs the "Splitting with a group?" control for one, though it still works if they want a different split.

### OrderScan — the camera scanner

```csharp
private async Task StartScanningAsync()
{
    _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/OrderScan.razor.js");
    await _module.InvokeVoidAsync("startCamera", _videoElement, _canvasElement);
}
```
Camera access is gated behind an explicit "Start scanning" tap rather than auto-starting on render — required for reliable behavior on iOS Safari, and it also avoids prompting for camera permission before the page's own `[Authorize]` redirect chain has even settled. `DisposeAsync` always calls the JS module's `stopCamera()` (wrapped against `JSDisconnectedException` for the "tab already closed" case) — otherwise the OS's camera-in-use indicator would stay lit after navigating away.

**The entire capture → decode loop runs client-side in JS, with zero per-frame Blazor interop calls**:
```js
// OrderScan.razor.js
import "/lib/jsqr/jsQR.js";
const VALIDATE_PATH_PATTERN = /^\/orders\/validate\/[0-9a-fA-F-]{36}\/[0-9a-fA-F-]{36}$/;

function tryNavigate(decoded) {
    const url = new URL(decoded, location.origin);   // throws → not a URL at all, ignore
    if (url.origin !== location.origin || !VALIDATE_PATH_PATTERN.test(url.pathname)) return false;
    stopCamera();
    location.assign(url.href);
    return true;
}
```
This is a deliberate security boundary, not just parsing convenience: a maliciously crafted QR code (or a genuine QR from some other app pointed at this camera by mistake) **cannot** redirect an authenticated manager's session off-app, because `tryNavigate` only ever calls `location.assign` for same-origin URLs matching the exact `/orders/validate/{guid}/{guid}` shape (order ID, then pass ID) — anything else is silently ignored and the scan loop just keeps running. A successful match does a **real browser navigation** (`location.assign`), not a callback into the Blazor circuit — the scan result reaches `OrderValidate.razor` the same way a manually-typed URL would. Getting this pattern out of sync with the actual route shape is a real, silent failure mode worth watching for: it fails closed (the scan loop just keeps running instead of navigating) rather than throwing anywhere visible, which is exactly what happened here when the route grew a second `{PassId:guid}` segment and this pattern wasn't updated in the same change.

**Manual lookup fallback**: below the viewfinder, "No camera, or the code won't scan? Look the order up instead" — a plain text input (order number, e.g. "021") plus a "Find order" button, submittable on Enter. `ManualLookupAsync` calls the same `OrderController.GetOrdersForManagementPagedAsync` search every other manager list page uses, scoped the identical way (`null` for an admin — searches every business; `ManagedBusinessContext.SelectedBusinessId` for a manager — passing `null` there isn't "search all mine," it's rejected outright by `OrderService.ResolveManagerBusinessIdAsync`), hard-filtered to `OrderStatuses.Confirmed` since only a Confirmed order has a live, unredeemed pass to confirm. Each match renders as a row: a single-pass order gets one "Confirm pickup" button straight to `/orders/validate/{orderId}/{passId}`, a multi-pass order gets one button per pass (`pass.Label`) — same "don't guess which pass" principle `OrderValidateLegacy` applies below. This exists for the same reason a QR scanner needs a fallback at all: a torn/dirty screen, no camera hardware, or a customer who just reads their order number aloud at the counter.

### OrderValidate — the landing page

```razor
@page "/orders/validate/{Id:guid}/{PassId:guid}"
@* Reachable both from the in-app scanner and by any external QR reader opening this URL directly —
   must be fully self-sufficient and re-check authorization itself, never trust how the visitor arrived. *@
```
Calls `OrderController.GetOrderForManagementAsync(Id)` on load — the exact same ownership check (`OrderService.GetOwnedOrderAsync`) any other manager-facing order read uses, regardless of whether this page was reached via the in-app scanner, a manually typed URL, or a third-party QR scanner app opening the link directly. The matching pass is found client-side via `order.PickupPasses.FirstOrDefault(p => p.Id == PassId)` — a `PassId` that doesn't belong to this order renders a "This pickup pass doesn't exist" panel rather than falling through to a confusing state. "Confirm pickup" calls `OrderController.RedeemPickupPassAsync(Id, PassId)`, which redeems *this* pass and completes the whole order in one call; a `ConflictObjectResult` (already redeemed, a different pass on the same order already completed it, or the order was cancelled — a duplicate scan or a race with the manager dashboard) re-fetches the order rather than leaving a stale status badge on screen. `StatusExplanation` distinguishes "this pass was already used" from "the order was already picked up using a *different* pass" purely from `order.Status.Name` + `pass.RedeemedAt`.

### OrderValidateLegacy — the pre-multi-pass redirect

```razor
@page "/orders/validate/{Id:guid}"
```
Catches a QR code printed/saved before pickup passes gained their own route segment. Calls the same `GetOrderForManagementAsync(Id)` as the page above, then branches purely on how many passes the resolved order has (every Confirmed order is guaranteed at least one via `OrderService`'s backfill — see `BACKEND_ARCHITECTURE.md` §3 OrderPickupPass): exactly one forwards straight to `/orders/validate/{Id}/{that pass's Id}` (`NavigationManager.NavigateTo(..., replace: true)`, so the legacy URL doesn't linger in browser history); zero or more than one renders a "couldn't tell which pass this refers to" panel instead of guessing. Unauthorized/not-found render the same `ForbiddenPanel`/`NotFoundPanel` the page above uses.

---

## 11. Admin / Manager Pages

### Dashboard

**File:** `Components/Pages/Dashboard.razor` — `@page "/dashboard"`

Stat cards (Businesses/Users admin-only; Packages/Orders scoped per-role) plus a 14-day **Orders / Kg saved trend chart**, added as a Phase 3 feature. The chart is a hand-rolled bar chart — no charting library:

```csharp
private async Task LoadDailyStatsAsync(Guid? businessId)
{
    var since = DateTime.UtcNow.Date.AddDays(-13);
    var rangeOrders = (await OrderController.GetOrdersInRangeAsync(since, null, businessId)).Value ?? [];

    _dailyStats = Enumerable.Range(0, 14)
        .Select(i => since.AddDays(i))
        .Select(day => new DailyStat(
            DateOnly.FromDateTime(day),
            rangeOrders.Count(o => o.CreatedAt.Date == day),
            rangeOrders.Where(o => o.CreatedAt.Date == day && o.Status.Name == Completed)
                       .Sum(o => o.OrderPackages.Sum(op => op.Quantity * op.Package.WeightKg))))
        .ToList();
}

private static int BarHeightPx(decimal value, decimal max) =>
    value <= 0 ? 3 : (int)Math.Clamp(value / max * ChartHeightPx, 4, ChartHeightPx);
```
Reuses the exact same `OrderController.GetOrdersInRangeAsync` call the CSV export uses (`OrderManagement.razor`, below) — one backend query, two frontend consumers. Bucketed in **UTC**, not the viewer's local time — good enough for a trend *shape*, and it avoids depending on `ClientTimeZoneService`'s async JS-interop initialization inside a component whose other data (counts) doesn't need it. Bar heights are computed as **pixels**, not CSS percentages — percentage heights don't reliably resolve through a flex-item chain whose parent isn't itself stretched (`align-items: flex-end` on the row), so pixel math sidesteps that entirely rather than fighting the CSS.

A `BusinessManager`'s stat cards and trend chart are scoped to `ManagedBusinessContext.SelectedBusinessId`, not every business they staff — `LoadDashboardAsync` passes it straight through to `OrderController.GetOrdersForManagementAsync`/`GetOrdersInRangeAsync`, and `OnInitializedAsync` subscribes to `ManagedBusinessContext.OnChange` (`HandleManagedBusinessChanged` re-runs `LoadDashboardAsync`) so switching businesses in `NavMenu` reloads every card and the chart in place, no navigation needed. Admins skip `ManagedBusinessContext` entirely and always see platform-wide totals.

**Business tools accordion (UI revamp Phase 4)** — Share-your-impact and the POS/inventory webhook panel (both only rendered when `_myBusinessId is not null`, i.e. never for an admin) start collapsed behind `_impactPanelOpen`/`_webhookPanelOpen` bools gating each body's `@if`, under a page-only "Business tools" heading separate from the "Analytics" heading above the trend chart and Business Analytics card. Deliberately **not** a real Bootstrap accordion (`data-bs-toggle="collapse"`) — this app never loads Bootstrap's JS bundle (only `bootstrap.min.css`, `App.razor` §2), so the data-attribute markup would render but do nothing. The toggle button reuses the `card-header` class purely for its rounded-top-corner/padding styling (not for any `.accordion-button` behavior), composed with the same plain-Bootstrap utility classes (`btn`, `d-flex`, `bg-transparent`) the rest of this admin-only page already uses instead of any `--em-*` token (§13's "the admin section reskins entirely through `data-bs-theme`" finding).

**Business Analytics card (Phase 8)** — sell-through rate and a 24-bar "busiest pickup hours" chart, right below the trend chart, fed by one call to `PackageController.GetForAnalyticsAsync(businessId, since)` (same 14-day `since` as the trend chart, reused from `LoadDailyStatsAsync`) rather than a second backend round-trip per metric:
```csharp
private async Task LoadAnalyticsAsync(Guid? businessId)
{
    var since = DateTime.UtcNow.Date.AddDays(-13);
    _analyticsPackages = (await PackageController.GetForAnalyticsAsync(businessId, since)).Value ?? [];

    var closed = _analyticsPackages.Where(p => p.PickupEnd < DateTime.UtcNow).ToList();
    _soldQty = closed.Sum(p => p.OrderPackages.Where(op => op.Order.Status.Name == Completed).Sum(op => op.Quantity));
    _unsoldQty = closed.Sum(p => p.Quantity);
    _sellThroughRate = _soldQty + _unsoldQty > 0 ? (decimal)_soldQty / (_soldQty + _unsoldQty) : 0m;

    RecomputeHourlyPickupStats(); // buckets by ClientTimeZoneService.ToLocal(p.PickupStart).Hour
}
```
Sell-through only counts packages whose pickup window has **already closed** — an open package's remaining `Quantity` hasn't finished selling yet, so counting it as "unsold" would understate the rate. This falls out of `Package.Quantity`'s own lifecycle rather than needing a separate "original quantity" field: `Confirmed`/`Completed` decrement it and only `Completed` never restores it, so once every `Pending`/`Confirmed` order against a closed package has resolved, `Quantity` remaining *is* "never sold." The hourly chart is bucketed by the **viewer's local hour** (`ClientTimeZoneService.ToLocal`, not UTC like the trend chart above it) — split into its own `RecomputeHourlyPickupStats` method specifically so a timezone that resolves *after* the initial data fetch (`ClientTimeZoneService.OnChange`) can rebucket without refetching from the server. The progress-bar width is rendered from a plain `int` percentage, not the raw `decimal` rate — interpolating a `decimal` straight into inline CSS (`width: @(rate * 100)%`) renders through the app's fixed `ro-RO` culture (§10 `BACKEND_ARCHITECTURE.md`) as `70,0%`, a comma the browser silently can't parse as a CSS length, so the bar would render at 0 width even though the percentage text above it reads correctly.

**"Share your impact" panel (Impact Phase 1)** — a small card right below Business Analytics, shown only when `_myBusinessId is not null` (a `BusinessManager` with a business actually selected; admins, who manage no single business, never see it). Renders a read-only `<input>` containing:
```razor
<script src="@(NavigationManager.BaseUri.TrimEnd('/'))/js/impact-widget.js" data-business-id="@_myBusinessId"></script>
```
built from `NavigationManager.BaseUri` — not `IConfiguration`'s `App:BaseUrl` (the key `AuthService`/`PackageService`'s email links use) — deliberately, so the copy-pasted snippet is correct whether the manager reached this page via a bare `dotnet run`, this app's own `docker-compose.test.yml` port mapping, or a real production domain, without needing that config key kept in sync. The Copy button is plain vanilla `onclick="..."` (`navigator.clipboard.writeText(...)`, same "skip the Blazor circuit for a purely client-side action" convention `PublicLayout.razor`'s cart-clear button uses — see §4) rather than `@onclick`/`IJSRuntime` — a clipboard write needs no server round-trip at all. See `wwwroot/js/impact-widget.js` (§14) for what the embedded script itself does once it's live on a business's own site.

**POS/inventory webhook panel (Phase 17)** — right below "Share your impact", same `_myBusinessId is not null` gate. `GenerateApiKeyAsync`/`RevokeApiKeyAsync` call the matching `BusinessController` actions; a freshly-generated plaintext key is held in a local `_generatedApiKey` field and shown in the same reveal-once `<input readonly>` + Copy pattern as the impact snippet above, cleared on next reload so it's never recoverable from the page after that. The endpoint URL and a JSON payload example (built as a C# raw string interpolating today's date, so it's always a plausible pickup window) are static markup — no round-trip needed to show them. "Revoke key" goes through the same `ConfirmDialog` (§12) every other destructive action in the app uses (package delete, closure removal, brand delete) rather than firing immediately.

### Businesses / BusinessForm

`Businesses.razor` is the admin/manager list — admins see every business and can create new ones; a `BusinessManager` sees only the business(es) they're staff of (`staffUserId` passed to `BusinessController.GetPagedAsync`) and can edit any of them. Staff are shown as removable chips per row (`business.Staff.OrderBy(s => s.User.Name)`, each with a small `×` calling `BusinessController.RemoveStaffAsync`) plus an admin-only `AnchoredDropdown` "add staff" trigger listing every `BusinessManager` not already on that row — picking one calls `AddStaffAsync` and **deliberately doesn't close the dropdown**, so an admin can add several staff in one open. `BusinessForm.razor` serves both `/businesses/create` (admin-only) and `/businesses/edit/{id}` (admin or one of the business's own staff, via `IsStaffAsync`) behind one component, branching on whether `Id` was supplied (`IsEdit`) — staff assignment itself lives on `Businesses.razor`/`Users.razor`, not on this form.

**Photo upload**: the old bare `ImageUrl` text field is now a `Microsoft.AspNetCore.Components.Forms.InputFile` next to a live thumbnail (shown once `_model.ImageUrl` is non-empty) and a smaller text field underneath it for pasting a URL by hand — both write to the same `_model.ImageUrl`, so either path leaves the form in an identical state. `OnImageSelectedAsync` checks `e.File.Size` against `Constants.ImageUpload.MaxSizeBytes` before ever opening a stream (a fast client-visible rejection instead of a mid-upload failure), then streams `e.File.OpenReadStream(ImageUpload.MaxSizeBytes)` straight into `IImageUploadService.SaveAsync(..., "businesses")` — no intermediate buffering, no HTTP round-trip, since Blazor Server already has the byte stream in-process. A `_uploadingImage` flag disables the `InputFile` mid-upload and an inline `_imageUploadError` surfaces either an oversized-file message or `ImageUploadService`'s own "unsupported type" `InvalidOperationException` text. `PackageForm.razor` below has the exact same block, just uploading to `"packages"` — see `BACKEND_ARCHITECTURE.md` §5 `IImageUploadService` for the disk/URL side.

**Approval & moderation (Phase 9)**: an admin-only Status column/badge plus a status filter dropdown (All/Pending/Approved/Rejected/Hidden — the last two map to `Business.Status`/`Business.IsHidden` respectively, see `BACKEND_ARCHITECTURE.md` §4's `statusFilter` param) sit alongside the existing type filter. Row actions branch on status: a `PendingApproval` row gets Approve (instant, calls `BusinessController.ApproveAsync`) and Reject (opens a reason prompt); a `Rejected` row gets a "reconsider" button reusing the same Approve handler (`ApproveAsync` allows `Rejected → Approved`, not just `PendingApproval → Approved`); an `Approved` row gets Hide/Unhide (Hide opens the same reason prompt as Reject). The reason prompt is `ReportDialog` (§12) reused for both — a `ReasonPromptMode` enum (`Reject`/`Hide`) picks the `Title`/`Message`/`ConfirmLabel` it's given, so despite sharing one modal instance the copy is specific to the action ("Reject '{name}'?" vs. "Hide '{name}'?"), not generic "report" wording. Edit/staff-assignment/delete are unchanged, except Edit and the staff-add dropdown are hidden for anything not currently `Approved` (a pending/rejected business has no live storefront presence yet, so there's nothing to staff).

`BusinessForm.razor`'s optional Location fields (`Latitude`/`Longitude`, both plain `InputNumber`) can be typed in by hand or filled by a "use my location" button next to them:
```csharp
private async Task UseCurrentLocationAsync()
{
    _locating = true;
    var position = await JSRuntime.InvokeAsync<GeoPosition?>("EcoMeal.geo.getPosition");
    if (position is null) { _locationError = "Couldn't get your location — check your browser's location permission."; return; }
    (_model.Latitude, _model.Longitude) = (position.Lat, position.Lng);
}
```
Same `EcoMeal.geo.getPosition` interop `Home.razor`'s "near me" toggle uses (§6, §14) — resolves to `null` rather than throwing, so a denied/unsupported browser just shows the inline error text instead of an unhandled exception. This is the one place in the form that isn't purely declarative `EditForm`/`InputXyz` binding, since geolocation has no HTML input equivalent.

**Brand dropdown (Phase 17)**: a plain `InputSelect` bound to `_model.BrandId` (`Guid?`), options sourced from `BrandController.GetAllAsync()` (no auth gate — any staff/admin can see every brand name to pick from, same as the business-type dropdown) with a `""` → null "— Standalone business —" first option. Rides the same `Submit()` call as every other top-level field, no separate save action. The form-text hint below it only links to `/types` (admin-only, brand CRUD) for an actual admin — a manager gets a plain "ask an admin to add it" note instead, gated on an `_isAdmin` field captured from the same `AuthenticationStateProvider` call `IsEdit`'s own staff check already makes.

**Loyalty punch card fields (Phase 14)**: two plain `InputNumber`s (`_model.LoyaltyPunchThreshold`/`LoyaltyDiscountAmount`, both `int?`/`decimal?` with a `[Range]` validated by the form's `DataAnnotationsValidator` against the same `Constants.Loyalty` bounds `BusinessService.NormalizeLoyalty` enforces server-side, each with its own `<ValidationMessage>`) inline with the rest of the main `EditForm` — not a separate save action like Hours/Closures below, since unlike those it's a plain scalar pair on `Business` itself rather than a related collection, so it rides the same `Submit()`/`AddAsync`/`UpdateAsync` call every other top-level field already does. A form-text hint ("Leave both blank to turn the punch card off") is the only UI surfacing `NormalizeLoyalty`'s both-or-neither rule — the form itself doesn't block submitting just one filled in, since the server-side normalize already makes that safe rather than needing duplicated client-side validation for it.

**Opening Hours / Holiday Closures sections** (edit mode only, `IsEdit && !_forbidden && !_notFound`) are two separate cards below the main `EditForm`, each with their own save action rather than being part of the single `Submit()` above:
- `BuildWeekRows(existing.Hours)` always returns exactly 7 rows in `WeekOrder` (Monday–Sunday), filling in a `09:00`–`18:00` default for any day the business hasn't configured yet — so a business with zero `BusinessHours` rows starts from a pre-filled grid to edit, not a blank one. The native `<input type="time">`/`<input type="date">` elements bind directly to `TimeOnly?`/`DateOnly?` properties via plain `@bind` (no `InputBase` wrapper needed — same pattern `OrderManagement.razor`'s CSV export date range already uses for `DateOnly?`).
- **Save hours** client-side rejects (before calling the controller) any non-closed day missing either time, then posts the full 7-row list to `BusinessController.SetHoursAsync` — matching `IBusinessRepository.SetHoursAsync`'s all-or-nothing replace semantics (`BACKEND_ARCHITECTURE.md` §3), there's no per-day save.
- **Add closure**/the remove button per list item call `BusinessController.AddClosureAsync`/`RemoveClosureAsync` independently and splice the result into the local `_closures` list on success, rather than reloading the whole business — the same "mutate local state from the call's result instead of a full refetch" style `Home.razor`'s `ToggleFavoriteAsync` uses (§6).
- Both sections check the controller result for `UnauthorizedResult` and show an inline error rather than throwing — a `BusinessManager` who lost staff access to this business mid-session (e.g. an admin removed them in another tab) gets a message instead of an unhandled exception, same defensive shape the rest of this form already has for `_forbidden`/`_notFound`.

### Packages / PackageForm / PackageTemplates

Same list/form split as Businesses, plus:
- `Packages.razor`/`PackageTemplates.razor` scope a manager to `ManagedBusinessContext.SelectedBusinessId` (updated live via its `OnChange`, same subscription pattern as Dashboard) via a **sentinel `Guid.Empty`** fallback (`_myBusinessId ?? Guid.Empty`) when nothing's selected yet — passing `null` there would instead show *every* business's packages, which is the opposite of the intended scoping. `PackageForm.razor`'s create path uses the same selection to default `_model.BusinessId`, but its **edit** authorization check is deliberately broader: it calls `BusinessService.IsStaffAsync(existing.BusinessId, currentUserId)` against the package's *own* business, not just whichever one is currently selected in the switcher — so a manager can still open and edit a package that belongs to a different business they staff, without first switching to it.
- `PackageForm.razor`'s dietary-tag picker is a checkbox grid over `Constants.DietaryTags.All`, toggling membership in `_model.DietaryTags` (a plain `List<string>`, no multi-select `InputSelect` involved).
- `PackageForm.razor`'s photo field is the same upload-or-paste-a-URL block described under Businesses / BusinessForm above, uploading to the `"packages"` subfolder instead of `"businesses"`.
- Cross-field pickup-window validation lives on the private `PackageFormModel : IValidatableObject` (pickup end must be after pickup start **and** in the future) — compared against `NowLocal`, a field the parent component keeps in sync with `ClientTimeZoneService`'s resolved local time, specifically so the "must be in the future" check uses the *viewer's* clock rather than the server's.
- **"Write it for me" (Phase 1)**: a button next to the Description label, disabled while `_model.Name` is blank or a draft is already in flight. Calls `PackageController.DraftDescriptionAsync(_model.Name, packageTypeName, _model.DietaryTags)` in-process (same façade pattern as everything else on this page, `BACKEND_ARCHITECTURE.md` §6) and drops the result straight into `_model.Description` — the manager can still edit it before saving, nothing writes to `Package` until Submit. Since `ActionResult<string>` doesn't throw across an in-process call the way it would over real HTTP, the "AI not configured/available" case is read off `result.Result is ConflictObjectResult` rather than a caught exception, and shown as an inline `_aiDraftError` message under the field — same visual slot `_imageUploadError` uses above.
- `Packages.razor` shows a 🔁 "Daily" badge next to any package whose `TemplateId` is set (`BACKEND_ARCHITECTURE.md` §3), plus a "Recurring templates" link to `/packages/templates` alongside the existing "Add Package" button.
- **Hide/Unhide (Phase 9)**: a per-row toggle next to Edit/Delete — Hide opens `ReportDialog` (§12) with its own Hide-specific `Title`/`Message`/`ConfirmLabel`, not the default report copy; Unhide is instant. A hidden row shows a "Hidden" badge (hover for the reason) next to the Daily badge. Same authorization as every other write on this page — admin, or the package's own business's staff.
- **`@rendermode` opts out of prerendering** (`new InteractiveServerRenderMode(prerender: false)`, unlike every other page's plain `InteractiveServer`) — with prerendering on, a hard navigation here paints a static, non-interactive copy of the page before the real circuit swaps in, and a click landing in that window (e.g. a bulk-select checkbox, below) is silently lost.
- **Bulk-select**: a checkbox column (admin/manager rows only, driven by `SelectablePackagesOnPage`) feeds a `HashSet<Guid> _selectedIds` that persists across paging/filtering; a header checkbox reflects/toggles "all selectable on this page" via `AllSelectableOnPageSelected`. One or more selected reveals a toolbar (Duplicate/Adjust quantity/Extend pickup window/Clear selection) above the table, each action confirmed via its own small modal before calling the matching `PackageController.*ManyAsync`.
- **AI markdown-pricing suggestion (AI Phase 5)**: a 🏷️ badge appears next to the price of any manageable row whose id is in `_markdownCandidateIds` — a `HashSet<Guid>` fetched business-wide (not paginated, so the badge shows regardless of which page a candidate lands on) via `PackageController.GetMarkdownCandidatesAsync(businessId?)` in `LoadMarkdownCandidatesAsync`, called from `OnInitializedAsync` and again from `HandleManagedBusinessChanged` (same admin-sees-everything/manager-sees-`_myBusinessId ?? Guid.Empty` scoping `LoadPageAsync` already uses). The badge itself costs nothing to show — clicking it is what actually triggers the Ollama call (`OpenMarkdownSuggestionAsync` → `PackageController.GetMarkdownSuggestionAsync(package.Id)`), deliberately not an automatic background fetch, since calling a local model for every candidate on every page load doesn't scale the way a cheap candidate-list query does. Opens a small modal (same `.confirm-backdrop`/`.confirm-dialog` shell as the bulk-action modal above) showing a loading spinner, then either the struck-through current price next to the suggested one with the AI's one-sentence explanation, a "no suggestion right now" message (the model itself saw no reason to cut, or an invalid suggestion was discarded server-side — `MarkdownSuggestion?` came back `null`), or an error (`result.Result is ConflictObjectResult`, same "AI not configured/available" convention `DraftDescriptionAsync` above uses). Two actions, neither ever touches `Package.Price` directly: **Edit package** just links to `/packages/edit/{id}` so the manager applies the number themselves, and **Dismiss** calls `PackageController.DismissMarkdownSuggestionAsync` (persists `Package.MarkdownDismissedAt` so the badge doesn't reappear for that package) and removes the id from `_markdownCandidateIds` locally so the badge disappears immediately without a full reload.
- **"Mark as donated" (Impact Phase 1)**: the same "badge fetched business-wide via `HashSet<Guid>`" shape as the markdown badge above, but far simpler — no AI call, no modal content to load. A closed, completely-unsold, not-yet-donated package (`_donationCandidateIds`, from `PackageController.GetDonationCandidatesAsync(businessId?)` in `LoadDonationCandidatesAsync`, called from the same two places `LoadMarkdownCandidatesAsync` is) shows a small heart-outline button next to its name instead of the price-badge slot the markdown suggestion uses; clicking it opens a plain `ConfirmDialog` ("closed with none sold... counts toward your food-saved impact") and confirming calls `PackageController.MarkAsDonatedAsync(package.Id)`, then removes the id from `_donationCandidateIds` and reloads the page so the row's badge flips from the clickable heart-outline to a static green "Donated" pill (`package.DonatedAt is not null`) reflecting the real column. Unlike the markdown suggestion's non-destructive "just a suggestion" framing, this one genuinely changes the package (sets `DonatedAt`) — hence the confirm step, where the markdown flow only ever needed a "close" button.

**Recurring templates** — a "Repeat this every day" checkbox, shown only on **create** (not edit, since a template is derived from one specific package's fields at creation time):
```csharp
await PackageController.AddAsync(package);
if (_model.RepeatDaily)
    await PackageTemplateController.CreateFromPackageAsync(package.Id, pickupStart.TimeOfDay, pickupEnd.TimeOfDay);
```
The package is created first (same as the non-recurring path), then the template is derived from it in a second call — `package.Id` is already populated at that point because EF Core client-generates `Guid` primary keys when an entity enters the `Added` state, not deferred until `SaveChangesAsync` (see `BACKEND_ARCHITECTURE.md` §3 PackageTemplate). `PackageTemplates.razor` (`/packages/templates`) is the standalone management page for existing templates — a flat table (name, daily window converted through `ClientTimeZoneService` the same way `Packages.razor` does, qty/day, last-generated date, active/paused status) with Pause/Resume (`SetActiveAsync`) and a `ConfirmDialog`-gated "stop repeating" (`DeleteAsync`) per row. No create form of its own — a template can only be created by ticking the checkbox while creating a package, never edited or created standalone, since it's meant to always start from a real package's current values.

**Bulk-action toolbar (Phase 8)** — a checkbox column (only rendered per-row when the viewer can manage that package — admin, or manager on their currently-selected business) plus a header "select all on this page" checkbox drive a `_selectedIds` `HashSet<Guid>`. Selecting anything reveals a toolbar above the table (Duplicate / Adjust quantity / Extend pickup window / Clear selection), reusing the same `.confirm-backdrop`/`.confirm-dialog` shell `ConfirmDialog.razor` uses for the two modal-driven actions rather than a new component:
```csharp
private async Task ApplyBulkModalAsync()
{
    if (_bulkMode == BulkMode.AdjustQuantity)
        await PackageController.AdjustQuantityManyAsync(_selectedIds.ToList(), _bulkQuantityDelta);
    else
        await PackageController.ExtendPickupWindowManyAsync(_selectedIds.ToList(), _bulkExtendHours);

    _bulkMode = BulkMode.None;
    _selectedIds.Clear();
    await ReloadAsync();
}
```
The selection deliberately **isn't** scoped to the current page — it's a plain `HashSet<Guid>` that survives paging and filter changes, so a manager can build up a selection across several pages before acting on all of it in one call. It's cleared on a successful bulk action and on `HandleManagedBusinessChanged` (switching business in `NavMenu`), since a selection built up under the previous business no longer applies. An admin viewing "All businesses" isn't limited to one business per batch either — `PackageService.EnsureCanManageBusinessesAsync` (`BACKEND_ARCHITECTURE.md` §5) just loops the existing single-package ownership check across every distinct `BusinessId` in the selection.

### OrderManagement (+ CSV export)

`Components/Pages/OrderManagement.razor` — the manager/admin order queue, plus (Phase 3) a date-range CSV export:

```razor
<a class="btn btn-outline-secondary" href="@ExportHref" target="_blank">
    <i class="bi bi-download me-1"></i> Export CSV
</a>

@code {
    private string ExportHref
    {
        get
        {
            var query = new List<string>();
            if (_exportFrom is { } from) query.Add($"from={from:yyyy-MM-dd}");
            if (_exportTo is { } to) query.Add($"to={to.AddDays(1):yyyy-MM-dd}");   // inclusive of the whole "to" day
            if (_isAdmin && Guid.TryParse(_businessFilter, out var businessId)) query.Add($"businessId={businessId}");
            return "/api/orders/export" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        }
    }
}
```
This is a **plain `<a href>`**, not a button wired to `OrderController` — it has to be, since `OrderExportController` is a real HTTP endpoint and the browser needs to treat the response as a downloadable file (see `BACKEND_ARCHITECTURE.md` §6). The rest of the page (search, status/business filters, confirm/complete actions) follows the identical `Debouncer`-gated paged-list pattern every other admin list page uses. Cancel is the one action that's `ConfirmDialog`-gated rather than instant (`_pendingCancel` holds the order awaiting confirmation) — a manager's misclick here refunds a paying customer, so it gets the same confirm-before-mutate treatment as the customer-facing cancel on `Orders.razor` (§9).

**Export popover (UI revamp Phase 4)** — the from/to date pickers used to sit permanently in their own row below the filters; they now live inside an `.em-popover` (§13) gated by `_exportOpen`, opened by an "Export CSV" button next to the filters instead. `ExportHref` itself is unchanged — still the same plain `<a href target="_blank">` computed property above, just rendered inside the popover's footer, with `@onclick="() => _exportOpen = false"` closing the panel alongside the browser's own download-tab navigation.

A manager's `businessId` (both for the paged list and for `ExportHref` above) comes from `ManagedBusinessContext.SelectedBusinessId`, not a page-local dropdown — an admin instead picks from `_businessFilter`, a plain `<select>` over every business. `OnInitializedAsync` subscribes to `ManagedBusinessContext.OnChange` (`HandleManagedBusinessChanged`, resetting `_pageIndex` back to 1 before reloading) so switching businesses in `NavMenu` refreshes the order queue in place. If a manager staffs zero businesses, `GetOrdersForManagementPagedAsync` returns `UnauthorizedResult` and the page renders `ForbiddenPanel` ("You don't manage a business yet…") instead of an empty table.

**Logistics note hint (Phase 15)**: a small `bi-chat-left-text` icon next to the order number, `@if (!string.IsNullOrWhiteSpace(order.LogisticsNote))`, its `title` attribute carrying the note text for an at-a-glance hover — the full note itself only renders in `OrderDetailModal` (§12) once the row is clicked open.

### Payments

**File:** `Components/Pages/Payments.razor` — `@page "/payments"`, manager/admin payout ledger.

Deliberately **not** a new backend surface — it calls the exact same `OrderController.GetOrdersForManagementPagedAsync` `OrderManagement.razor` does (same `ManagedBusinessContext`/`_businessFilter` scoping split, same `Debouncer`-gated reload, same `ForbiddenPanel` fallback for a staffless manager), just reading `order.Payment` off each row instead of rendering the status-change action buttons. Stat cards up top — "Collected (this page)" / "Refunded (this page)", plus a third "Refund failed (this page)" count that only renders when non-zero — are computed client-side from the currently-loaded page (`_paged.Items.Where(...).Sum(...)`/`.Count(...)`), not a separate aggregate query; "this page" in the label is accurate, not a rounding shortcut — switching pages recomputes all three from whatever page just loaded. No write actions live here at all — refunds (and the occasional `RefundFailed`) only ever happen as a side effect of `OrderService.ApplyStatusChangeAsync`'s Cancelled transition (`BACKEND_ARCHITECTURE.md` §5), never a button on this page.

**Its own CSV export**: a date-range picker (`_exportFrom`/`_exportTo`) plus a plain `<a href>` "Export CSV" button, the identical `<a href target="_blank">`-not-a-controller-call shape `OrderManagement.razor` uses (above) and for the same reason — a file download is an HTTP-level concept, not something an in-process controller call can hand back. It hits a *different* route on the same controller, though: `OrderExportController`'s `/api/payments/export` rather than `/api/orders/export`. Business scoping mirrors the page itself rather than duplicating `_businessFilter`/`ManagedBusinessContext` logic inline: an admin's export takes `businessId` from the same `_businessFilter` dropdown the table already uses, a manager's takes it from `ManagedBusinessContext.SelectedBusinessId` directly (not a page-local filter — managers don't get one here, same as the table above it).

### Users

Admin-only role management. Two independent `AnchoredDropdown`s per row (role, and — only for `BusinessManager` rows — business assignment), deliberately mutually exclusive (closing one when the other opens, so only one is ever open at a time). The business column is the same removable-chips-plus-add-dropdown shape `Businesses.razor`'s Staff column uses (§11 above), just inverted — one row per user, chips for every business they're staff of (`AssignedBusinesses(user.Id)`), and an "add business" `AnchoredDropdown` listing every business not already assigned that **stays open across multiple picks**, same as the Staff column. Both call the same `BusinessController.AddStaffAsync`/`RemoveStaffAsync` `Businesses.razor` does — there's exactly one staff-assignment backend surface, just two admin entry points into it (by business, or by user). Role changes and business (re)assignments both re-fetch the user list afterward, since `UserService.UpdateRoleAsync` can auto-release every business a user staffed server-side as a side effect of a role change away from `BusinessManager` (see `BACKEND_ARCHITECTURE.md` §7) — the UI has no way to know that happened without asking again.

### Reports (Phase 9)

**File:** `Components/Pages/Reports.razor` — `@page "/reports"`, admin-only, `MainLayout`.

Lists only `Report`s with `Status == Open` (`ReportController.GetOpenAsync`) — resolved ones simply drop off the list rather than being shown greyed-out, since there's no "resolved reports" view in this app; the audit log (below) is where a resolved report's outcome lives on afterward. Two actions per row, both `ConfirmDialog`-gated rather than instant, since both are effectively irreversible from this page (dismissing closes the report for good; taking action hides a live business/package/kitchen tip): Dismiss just closes the report, "Hide target" calls `ReportController.TakeActionAsync(reportId, report.Reason)` — **reusing the report's own `Reason` as the hide reason** rather than prompting the admin for a second one, since re-typing the same text they're already reading in the row would be pure friction. The list is reloaded (not patched in place) after either action, matching the pattern every other admin list page in this app uses after a mutation. **Phase 16**: the target-type pill is a three-way `switch` (`Business`/`Kitchen tip`/`Package`) instead of the earlier `Business`/`Package` ternary — `view.TargetName` for a kitchen-tip report is already a truncated snippet of the tip text (`ReportService.ResolveTargetNameAsync`, `BACKEND_ARCHITECTURE.md` §5), so the row needs no tip-specific rendering of its own beyond the pill label.

### Audit Log (Phase 9)

**File:** `Components/Pages/AuditLog.razor` — `@page "/audit-log"`, admin-only, `MainLayout`.

The same paginated-list shape as every other admin table page (`Debouncer`-gated search + filter reload, `Pagination` component) — search matches actor/target name, plus dropdown filters for action and target type. `DisplayAction` is the one page-local formatting helper: a regex (`(?<!^)([A-Z])` → `" $1"`) splits a PascalCase `Constants.AuditActions` value like `BusinessStaffAdded` into "Business Staff Added" for display, rather than maintaining a second parallel list of display strings alongside the constants. Purely a read-only view — there is no way to create, edit, or delete an entry from the UI, matching `IAuditLogService.LogAsync` never being exposed on a controller as a standalone write (`BACKEND_ARCHITECTURE.md` §3/§5).

### Types (Phase 11)

**File:** `Components/Pages/Types.razor` — `@page "/types"`, `@attribute [Authorize(Roles = AppRoles.Admin)]`.

Three side-by-side `card`s (Kitchen types / Package types / Brands) in one page rather than separate routes — each is a `list-group` with an inline rename-in-place row (click the pencil icon, the `<span>` swaps for a bound `<input>`, check/x icons save or cancel) and a delete button, plus a small add form (`<input>` + a button that doubles as an Enter-to-submit handler via `@onkeyup`) at the bottom. No separate create/edit page or `ConfirmDialog`-free inline delete — unlike `BusinessForm.razor`, a lookup row is just a `Name`, so the full `EditForm`/`DataAnnotationsValidator` machinery other admin forms use would be pure ceremony here; this instead mirrors the compact "list + inline add form" shape `BusinessForm.razor`'s holiday-closures section already uses (§11 Businesses / BusinessForm).

Delete still goes through `ConfirmDialog` (§12) — a single shared dialog instance for all three sections, keyed by a `(string Kind, Guid Id, string Name)?` tuple rather than separate dialog instances, since only one can ever be open at a time. `BusinessTypeController`/`PackageTypeController`'s `AddAsync`/`UpdateAsync`/`DeleteAsync` and (Phase 17) `BrandController`'s counterparts can come back `UnauthorizedObjectResult` (shouldn't normally happen given the page's own `[Authorize]`, but the service enforces it independently too — see `BACKEND_ARCHITECTURE.md` §5) or, for delete, `ConflictObjectResult` when the row is still referenced by a `Business`/`Package` — pattern-matched into a section-scoped `_businessTypeError`/`_packageTypeError`/`_brandError` string, so a mistake in one column never clobbers a success message in another. The Brands card also shows each row's current location count and links its name to `/brands/{id}` (§7 Brands / BrandDetail) — the only one of the three with anywhere public to link to.

---

## 12. Shared Components

| Component | Role |
|---|---|
| `AnchoredDropdown` | Generic trigger + floating panel, for triggers whose on-screen position genuinely varies (a table row that can be anywhere depending on scroll/paging). `OnAfterRenderAsync` calls `EcoMeal.positionDropdown(anchorRef, panelRef)` on **every** render while open, not just the first — content that loads in async can grow the panel past its first, smaller measurement, and a stale position clips it against the viewport edge; repositioning is idempotent (no visible jump) since nothing else re-renders this subtree on a bare scroll. `positionDropdown` itself clamps against both the top and bottom edges (not just top), since a `100dvh`-sized anchor (§4) can still measure a few px past the visible viewport on some browsers. See §14 for the JS side. Used for the Businesses/Users manager-assignment pickers. Not suitable for the notification popup even with perfect positioning math, since its trigger lives inside a `position: sticky` ancestor — see `NotificationBell`/`NotificationPanel` below and §4 |
| `ConfirmDialog` | Generic confirm/cancel modal — delete confirmations, the cross-business "start a new basket?" prompt, cancel-order confirmations. `Busy` disables both buttons and swaps the confirm label for a spinner mid-request |
| `ReportDialog` (Phase 9) | Same `.confirm-backdrop`/`.confirm-dialog` shell as `ConfirmDialog` (both capped at `max-height: calc(100vh - 3rem)` with `overflow-y: auto`, so a tall message/reason can't push the buttons off a short viewport), plus a required reason `<textarea>` — the Submit button stays disabled until non-whitespace text is entered. Optional `Title`/`Message`/`Placeholder`/`ConfirmLabel` parameters default to the customer-facing report copy ("Report {TargetLabel}" / "Submit report"), used as-is by the report action on `BusinessDetail.razor`/`PackageDetailModal.razor` (submits via `ReportController.SubmitAsync`) — **Phase 16**: `BusinessDetail.razor` reuses the same single dialog instance for its per-tip flag icons too, just with a dynamic `TargetLabel` ("this tip" vs. the business's own name) picked by whether a `_reportTipId` is set. The admin-facing Reject/Hide actions on `Businesses.razor`/`Packages.razor` (§11) pass their own copy instead ("Hide '{name}'?" / "Hide package", etc.) — those two call sites never touch `ReportController` at all, they just borrow the modal shape for its `EventCallback<string>` |
| `ForbiddenPanel` / `NotFoundPanel` | Inline empty-state panels — the former for "wrong role," the latter for "this specific entity no longer exists" (distinct from the global 404 route, used by edit pages when a fetched-by-ID entity comes back null). Both render the shared `.em-empty-icon` badge (§13, UI revamp Phase 6); `RescueCircleInvite.razor` is `NotFoundPanel`'s one non-edit-page caller |
| `NotificationBell` / `NotificationPanel` | Split into a trigger (`NotificationBell`, rendered inside the sidebar footer / public header) and the popup itself (`NotificationPanel`, rendered once from each layout's top level, outside the sidebar/header entirely) sharing state through `NotificationPanelState` (§5) — not one component, because one component can't render in two DOM locations at once, and the popup *has* to live outside the sidebar/header's subtree (§4). Styled as a centered modal (`.notif-panel`, `position: fixed; top/left: 50%; transform: translate(-50%,-50%)`, `max-height: calc(100vh - 3rem)` with internal scroll) — the same family as `ConfirmDialog`/`ReportDialog`, chosen after a corner-pinned/`AnchoredDropdown`-based panel kept re-clipping against the sidebar's edge across several earlier fixes; centering plus a viewport-fraction max-height can't clip against any edge, on any screen size. A `System.Threading.Timer` on `NotificationPanelState` polls `GetMyUnreadCountAsync` every 30 seconds regardless of whether the panel is open, so the badge count stays fresh for e.g. a manager waiting on new orders; opening the panel separately fetches the actual list (`GetMyNotificationsAsync(20)`) on demand rather than keeping 20 rows in memory at all times. Unread items get a `--em-rescue` left accent bar rather than the generic dot the shared dropdowns use. `NotificationPanel`'s header also carries a bell-icon toggle for **web push** — `OnAfterRenderAsync(firstRender)` calls `PushSubscriptionController.GetPublicKey()` (hides the toggle entirely when `null`, i.e. `WebPush:*` isn't configured server-side — `BACKEND_ARCHITECTURE.md` §10) and `EcoMeal.push.getSubscriptionEndpoint()` (§14) to seed its on/off state without prompting for permission; clicking it calls `EcoMeal.push.subscribe`/`unsubscribe` then mirrors the result to `PushSubscriptionController.SubscribeAsync`/`UnsubscribeAsync`. Gated behind a bare `<AuthorizeView>` (any signed-in role, not just `Customer` — managers/admins get order-lifecycle pushes too) since subscribing needs a real `userId` to attach the row to |
| `OrderDetailModal` / `PackageDetailModal` | Drill-down modals from a ticket/row click — same visual shell (`biz-modal-*`/`pkg-modal-*` CSS classes), one shows order line items + status, the other a package's full description/tags/price with an "Add to basket" action. Its total shows `Order.Payment.Amount` (§9), not the raw line-item subtotal. **Impact Phase 1**: `OrderDetailModal` renders `<ImpactEquivalencyStats>` under the total, only for a `Completed` order. **Phase 15**: a "Customer note" fact row renders `@if (!string.IsNullOrWhiteSpace(Order.LogisticsNote))` — shared by both `Orders.razor` (the customer's own ticket) and `OrderManagement.razor` (the business side), so no separate note UI was needed for either. `PackageDetailModal`'s `RatingAverage`/`ReviewCount` parameters are plain caller-computed numbers, not a fetch of its own — `BusinessDetail.razor` passes them in from `_reviews` (above); the `StarRating` only renders when `ReviewCount > 0`, so a never-reviewed package shows no rating rather than a misleading 0-star one. Its `.pkg-modal-hero` image banner only renders `@if (!string.IsNullOrWhiteSpace(Package.ImageUrl))` — a package with no photo used to still get the hero block, just empty; now it gets no hero section at all, same "nothing configured → no section" rule the business hours panel (§7) follows |
| `Pagination` | Renders nothing at all when `TotalPages <= 1` — every paged list page is written to just drop the component in unconditionally rather than wrapping it in its own visibility check |
| `StarRating` | One component, two modes: `Editable=false` renders a fractional-fill overlay (two stacked 5-star rows, the top one clipped to `Value/5 * 100%` width) for display; `Editable=true` renders a real 1-5 click/hover picker. Both business cards and the review form use the same component, just with different parameters |
| `ThemeToggle` (UI revamp Phase 2/3) | One icon button, dropped into both the sidebar footer and the public header, just like `NotificationBell` above. Doesn't own any state itself — `OnAfterRenderAsync(firstRender)` reads the real DOM via `EcoMeal.theme.get()` (§14) to seed its icon (moon = currently light, sun = currently dark, i.e. the icon shows what clicking it switches *to*), and every click calls `EcoMeal.theme.toggle()` and mirrors its return value. The actual `data-theme`/`data-bs-theme` attribute writes and `localStorage` persistence all happen in JS, not here — see §13/§14 for why (the first-paint case has to run before any Blazor circuit exists) |

---

## 13. CSS Design System

**File:** `wwwroot/app.css` — a single ~4300-line stylesheet, no Sass/Less, no CSS-in-JS, no Tailwind. Organized into clearly delimited sections (`/* ── Section name ── */`), roughly in the order features were added:

```
Design tokens · Dark palette (UI revamp Phase 3) · Shared card shell (UI revamp Phase 5) · Shared pill shape (UI revamp Phase 5)
Empty-state icon (UI revamp Phase 6) · Buttons · Sidebar nav · Content · Stat card accents · Table · Badges · Role badges
Form focus · Login page · Sidebar user footer · Blazor error banner
Misc · Public shell · Home hero · Home browse · Impact leaderboard · Sign in / Create account (logged-out header)
Cart button + badge · Cart panel · Toast · Orders hero · Orders list
Order ticket (signature element) · Pickup pass (single-order QR page) · Account settings
Multiple pickup passes · Packages bulk-action toolbar · Confirm dialog · Filter / export popover (UI revamp Phase 4)
Manager order actions · Dashboard trend chart (+ axis row) · Star rating
Business grid (home) · Business detail page · Packages inside the business modal
Reviews inside the business modal · Package detail modal · Order detail modal
Manager pickup scanner · Pickup validation page · Notification bell · Favorites
Dietary tag badges · AI budget/basket planner
```
**UI revamp Phase 5** deleted the old "Package grid"/"Add to basket" sections outright rather than
migrating them — `.package-card`/`.home-grid`/`.package-card-add-btn` turned out to be dead CSS,
orphaned when `Home.razor` moved to the `.biz-card` business grid (browse kitchens, not raw
packages); grepping the whole `Components/` tree found zero `.razor` references to any of them.

### Design tokens

```css
:root {
    /* Two brand hues doing real work — herb-green for "fresh/rescued," rescue-orange for
       "reduced, closing soon" — on a warm paper ground instead of a cool mint one. --em-ink
       is tinted toward forest rather than neutral gray, so body copy reads as part of the
       same family as the headlines instead of generic UI gray. */
    --em-forest:     #0e2117;
    --em-forest-mid: #1c3f28;
    --em-leaf:       #2f9e5c;
    --em-leaf-mid:   #1f8a4c;   /* == --bs-primary, so Bootstrap's own utility classes match */
    --em-leaf-dark:  #1b7a44;
    --em-paper:      #f5efe2;   /* warm page background, not the old cool mint */
    --em-surface:    var(--em-paper);
    --em-ink:        #1b2a1f;
    --em-text:       var(--em-ink);
    --em-muted:      #6f6656;
    --em-leaf-darker: #166534;  /* pressed/active button state, one step past --em-leaf-dark */
    --em-cream:      #fffaf2;   /* the hero "rescued" stamp's own ink color */

    /* Semantic layer — components reach for these instead of a hardcoded hex, so a future
       dark theme only needs to redefine tokens here, not be hunted through the whole file. */
    --em-bg:             var(--em-paper);
    --em-surface-raised: #ffffff;
    --em-border:         #e3ddd0;
    --em-border-strong:  #cbc2ac;
    --em-text-muted:     var(--em-muted);
    --em-text-inverse:   #ffffff;

    --bs-primary: #1f8a4c;      /* + -rgb, --bs-link-color(-hover), all retuned to match */
    ...

    /* Rescue-orange — a "reduced for quick sale" sticker color, not a decorative accent:
       urgency badges, the hero stamp, and anything meaning "closing soon." */
    --em-rescue:      #d9702f;
    --em-rescue-dark: #b85a22;
    --em-rescue-soft: rgba(217, 112, 47, 0.16);

    /* Gold — reserved for "you earned this" moments only (loyalty, streaks, leaderboard
       rank #1): darkened off the raw sampled logo pixel to actually clear 4.5:1 on --em-paper. */
    --em-gold:      #a3811a;
    --em-gold-dark: #7a5f12;
    --em-gold-soft: rgba(163, 129, 26, 0.16);
    --em-bronze:    #92400e;    /* leaderboard's #3 rank — its own hue, unrelated to gold */

    /* Status accents — still need distinct hues for multi-state badges/stat cards, but
       hand-warmed off the raw Tailwind defaults that used to clash with the palette above. */
    --em-info:         #3f6d99;
    --em-info-soft:    rgba(63, 109, 153, 0.1);
    --em-violet:       #7d5fb0;
    --em-violet-soft:  rgba(125, 95, 176, 0.1);
    --em-danger:       #c9503f;
    --em-danger-dark:  #9c3a2c;
    --em-danger-soft:  #fbe9e6;
    --em-warning:      #f59e0b;
    --em-warning-dark: #b45309;
    --em-warning-soft: #fef3c7;

    --em-font-display: 'Fraunces', Georgia, 'Iowan Old Style', serif;
    --em-font-body:    'Hanken Grotesk', system-ui, -apple-system, sans-serif;
    --em-font-mono:    'JetBrains Mono', ui-monospace, 'SF Mono', Menlo, monospace;

    --em-shadow-sm: ...; --em-shadow-md: ...; --em-shadow-lg: ...;
}
```
`--bs-primary`/`--bs-link-color` are overridden to the same `#1f8a4c` as `--em-leaf-mid`, so Bootstrap's own `.btn-primary`/link-color utilities blend in with the custom design language instead of clashing with it — the app doesn't fight Bootstrap, it retunes it.

**UI revamp Phase 1 (design tokens)** eliminated all 170 raw hex literals that used to sit outside this block (confirmed by grep, zero remain), across `app.css` and all three `*.razor.css` isolation files. A near-duplicate of an existing brand color (`#1f8a4c`, `#1b7a44`, `#22c55e` in the reconnect-modal scaffold, `#1a7540` in the public header) just became a `var()` reference to the token it had drifted from. A genuinely off-brand, never-tuned Tailwind default (`#2563eb`, `#8b5cf6`, `#0ea5e9`, `#64748b`, `#e5e7eb`, `#d1d5db`, and the four separate reds `#ef4444`/`#f87171`/`#dc2626`/`#b91c1c`) was consolidated into the new `--em-info`/`--em-violet`/`--em-text-muted`/`--em-border(-strong)`/`--em-danger(-dark/-soft)` tokens above instead, warmed enough to sit next to `--em-leaf`/`--em-rescue` without clashing. Amber/warning hexes (`#f59e0b`, `#b45309`, `#fef3c7`) were already warm-compatible, so `--em-warning*` just tokenizes them as-is. One accidental collision the pass turned up: `.impact-rank-gold` and the allergen-warning tag both hardcoded the same `#b45309` by coincidence — now split into `--em-gold-dark` and `--em-warning-dark`.

### Theme switching (UI revamp Phase 2/3)

`<html>` carries a `data-theme="light"`/`"dark"` attribute, decided by a synchronous inline `<script>` at the very top of `App.razor`'s `<head>` — before the font/Bootstrap/`app.css` `<link>` tags, so the attribute exists before the browser has anything to paint. Resolution order: an explicit stored choice (`localStorage["em-theme"]`) wins; otherwise it falls back to `prefers-color-scheme: dark`. This has to be inline rather than in `site.js` because `site.js` loads at the bottom of `<body>` — by then the browser has already painted once, and swapping the attribute after that first paint is the exact flash the phase exists to avoid. `ThemeToggle` (§12) is the one component that touches this after load, via `EcoMeal.theme` (§14). The same script also sets `data-bs-theme` to the identical value — see below.

**The dark palette (Phase 3)** redefines the token layer inside `:root[data-theme="dark"]`, immediately after the base `:root` block, rather than as one block at the end of the file or scattered per-component — tokens are the one thing that legitimately belongs centralized, since every component already reaches for them by name. Only `--em-ink`, `--em-muted`, the surface/border tokens, the accent hues used as *text* (`--em-leaf(-mid/-dark)`, `--em-rescue(-dark)`, `--em-info`, `--em-violet`, `--em-danger(-dark)`, `--em-warning-dark`, `--em-gold(-dark)`, `--em-bronze`), the two rgba `-soft` pairs that were flat hex before, and the shadow tokens get redefined; `--em-forest`/`--em-paper` themselves are untouched, since two dozen components already use `--em-forest` as a fixed decorative fill (sidebar, order-ticket stub) that shouldn't shift with the reader's theme. Every value was reached by actually computing WCAG contrast against both `--em-bg` (`#0e2117`) and `--em-surface-raised` (`#1c3f28`) — not by inverting or applying a flat percentage. Most accents needed 20-45% blended toward white to clear 4.5:1, and the two that also sit on their own `-soft` tinted pill background (`--em-rescue-dark` in `.notif-push-error`, `--em-danger-dark`/`--em-warning-dark` on their own now-translucent `-soft`) needed 45-60%, because contrast against a translucent badge composited over the dark surface is a strictly harder bar than contrast against the plain surface alone.

That brightening is why the base `:root` also gained seven `--em-*-solid` tokens (`leaf`, `leaf-mid`, `leaf-dark`, `rescue`, `info`, `danger`, `muted`) holding the original light-mode hex, unconditionally, in both themes. A handful of buttons/badges fill solid with an accent and lay white text on top (`.package-card-add-btn`, `.notif-badge`, `.home-chip-active`, the `order-action-*:hover` states...) — that white-on-solid-fill contrast pair doesn't depend on page theme at all, so brightening the *same* token for its more common on-surface-text role would have quietly broken every one of those buttons in dark mode. The ~10 call sites that fill solid now point at the `-solid` variant instead; nothing else changed. Two more spots needed a real per-component override, not a token flip, because they're fixed dark-forest ink on a surface that's now also dark: `.order-action-qr`'s base (non-hover) state and `.order-ticket-seam`'s dashed divider (the signature ticket element, §13 below) — both get a small `:root[data-theme="dark"] .selector { ... }` rule right next to their light-mode rule, same placement convention this file already uses for `@media (prefers-reduced-motion: reduce)` blocks. `#blazor-error-ui`'s old `color-scheme: light only` (flagged as a real gap before this phase) is now `light`/`dark` per theme the same way.

**`data-bs-theme` rides alongside `data-theme`, set by the same two places** (`App.razor`'s inline script, `EcoMeal.theme.set`) — Bootstrap 5.3 ships its own complete `[data-bs-theme=dark]` palette that reskins every plain, un-customized Bootstrap primitive (`.card`, `.table`, `.form-control`/`.form-select`, `.dropdown-menu`, `.modal-content`...) at once. That matters more than it sounds: the entire admin section (§11 — Dashboard's stat tiles, every list/form page) is built from bare Bootstrap `.card`s that no `--em-*` token ever touches, and discovering that gap live (the sidebar went dark, the cards next to it stayed white) is what added this line — restyling every admin `.card` by hand would have been its own phase. `--bs-primary`/`--bs-link-color`/`--bs-btn-*` stay pinned to the brand green regardless of which Bootstrap theme is active, since `app.css` loads after `bootstrap.min.css` and wins on equal specificity — Bootstrap's own dark-theme values for those specific properties never apply.

**Three typefaces, one job each** (§2 for how they're loaded): `--em-font-display` (Fraunces, a serif with real optical-size variation) on every heading and `.font-display` element — the one place the design language departs from Bootstrap's default sans, giving the brand a distinct voice on page titles, the hero, and the order-ticket stub. `--em-font-body` (Hanken Grotesk) is the default for `html, body` — everything not explicitly opted into the display or mono face falls back to it. `--em-font-mono` (JetBrains Mono) is reserved for `.font-mono` call sites — order numbers, QR-adjacent labels — where a fixed-width face reads as "a code/reference," not body copy.

### Shared card & pill shapes (UI revamp Phase 5)

Two consolidations, both landing as CSS-only changes with zero `.razor` markup churn for the
existing call sites, because both use a **grouped selector** instead of a rename: `.order-ticket`,
`.biz-card`, `.impact-row`, and `.impact-optin-card` are listed together on one rule defining
`background`/`border-radius`/`box-shadow` (`.em-card`'s own selector joins the same list, for any
*new* card-shaped block going forward), and a second rule handles the hover-lift + `card-rise`
entrance animation the same way. `.impact-row`/`.impact-optin-card` get a second, denser rule
(12px radius, `--em-shadow-sm`) layered on top, since a leaderboard row is a list item, not a
clickable grid tile. `.order-ticket-clickable:hover`'s own bespoke `translateY(-1px)` + hand-rolled
rgba shadow was deleted in favor of the same `translateY(-3px)` + `--em-shadow-lg` `.biz-card:hover`
already used — a deliberate normalization, not an oversight; the two were only ever different
because nobody had gone back to reconcile them. **`.cart-line`** — named in the original redesign
plan as a fourth hand-rolled card shell — turned out on inspection to have no `background`/
`border-radius`/`box-shadow` of its own at all, just a `border-bottom` divider between rows in a
list; it was left alone rather than forced into a card shape that would have changed the cart
panel's compact-list feel for no real consolidation gain.

The pill/badge side works the same way: `.em-pill`'s selector is grouped with `.role-badge`,
`.business-type-badge`, `.package-type-badge`, `.order-status-badge`, `.biz-card-live-badge`,
`.biz-card-rating-badge`, `.biz-card-closed-badge`, `.biz-modal-open-badge`, and `.diet-tag` for
the base shape (`display`/`gap`/`padding`/`border-radius`/`font-size`/`font-weight`), with
`.em-pill--sm`/`--uppercase`/`--interactive` modifier rules doing the same grouped-selector trick
for the handful of classes that need a smaller size, uppercase+letter-spacing, or the
cursor/hover-filter/disabled treatment a clickable badge (`.role-badge`, used as an
`AnchoredDropdown` trigger) needs but a static label doesn't. `.em-pill--leaf/info/violet/rescue/
danger/warning/muted/dark/surface/glass` are the color modifiers a genuinely *new* badge reaches
for going forward — the existing families keep their own semantic modifier class names
(`.role-badge-admin`, `.order-status-cancelled`, `.diet-tag-allergen`...) rather than being renamed
to the generic ones, since `.role-badge-admin` already documents what it's for better than
`.em-pill--violet` would at the call site; only the *shape* needed to stop being redeclared
per-class. Two genuinely duplicate badges got merged outright instead of just re-shaped:
`.biz-pkg-closing-badge` (the "Ends in N min" countdown) turned out byte-for-byte identical to
`.biz-card-closed-badge` ("Closed now") once both were reduced to just their color, so
`PackageDetailModal.razor`/`BusinessDetail.razor` now render the countdown with
`.biz-card-closed-badge` directly — the one visible side effect is the countdown text now
renders uppercase, matching every other status pill in the app, where it used to be sentence case
purely because nobody had unified it before. `.biz-review-package-tag` was identically merged into
`.diet-tag` (same padding/font-size/color, just a different call site). Along the way, three color
rules that had drifted onto a raw, never-updated Tailwind hex instead of the Phase 1 token
(`role-badge-businessmanager`/`business-type-badge`'s blue, `role-badge-admin`'s violet,
`order-status-cancelled`'s red — all in an `rgba(...)` literal, which Phase 1's hex-literal grep
never caught since it only matched bare `#` hexes) now point at `--em-info-soft`/`--em-violet-soft`/
`--em-danger-soft` like every other badge in the same color family already did.

### Empty-state icon (UI revamp Phase 6)

`.em-empty-icon` is one shared "nothing here yet" treatment — a 64px soft-tinted circle
(`background: var(--em-muted-soft)`, a warm-tinted token off `--em-muted`'s own RGB, not
Bootstrap's cold `#6c757d`) wrapping the glyph, reusing `card-rise` for its entrance so empty
states aren't the one spot in the app with zero motion (same `prefers-reduced-motion` guard as
everything else). It replaced a copy-pasted `<i class="bi bi-X fs-1 text-muted d-block mb-3">`
fingerprint at 20 call sites — `home-empty`/`orders-empty`/`cart-empty`, `NotFoundPanel`/
`ForbiddenPanel`/the global `NotFound.razor` 404 page, and eleven admin list pages' "no results"
states (`Users`, `Packages`, `Businesses`, `Payments`, `Reports`, `AuditLog`, `OrderManagement`,
`PackageTemplates`, `StandingOrders`, `Referrals`, `Home.razor`'s own map-view-empty state). Same
sweep also caught `.em-pill--muted` still carrying a raw Tailwind slate (`rgba(107, 114, 128,
0.1)`) instead of a token — now `var(--em-muted-soft)` like the rest of that pill family — plus
two adjacent correctness fixes: `RescueCircleInvite.razor`'s bespoke "Rescue Circle no longer
exists" block now uses `NotFoundPanel` (§12) like every other such page, and
`PaymentCancel.razor`'s cancelled-state icon (was rendering leaf-green — `.cart-confirmation > i`'s
color rule outranks the `text-muted` class on the same element by specificity) got its own
`.cart-confirmation > i.text-muted` override so a cancellation no longer reads as a success.

The animation half of this phase was a confirm-don't-change pass: `card-rise` (0.35s ease-out) and
`.biz-card`/`.order-ticket-clickable`'s hover-lift (`translateY(-3px)` + `--em-shadow-lg`) from
Phase 5 were checked live in a real browser session, both themes, and still read as intentional
against the surfaces underneath them — no changes were needed there.

### The "order ticket" — the signature visual element

`Order` rendering (`Orders.razor`, `OrderManagement.razor`, `OrderPickupPass.razor`, `OrderDetailModal`, `OrderValidate.razor`) all share one CSS shape: a perforated-ticket card with a dashed "seam" divider and a torn-stub side panel showing the order number — deliberately evoking a physical pickup receipt rather than a generic table row, reinforcing the app's "go pick this up in person" mental model. `OrderPickupPass.razor` reuses the identical `.order-ticket`/`.order-ticket-stub` classes at a larger scale (`--pass` modifier classes) rather than inventing a new component, so the QR-code pass reads as "the same ticket, just the one you're holding" instead of a different UI. The stub itself (`--em-forest` fill, white text) and the seam's punch-hole circles (`var(--em-surface)`, always matching whatever's behind the ticket) were already theme-safe by construction; only the seam's own dashed line — forest-tinted to read against light paper — needed a dark-mode override (previous section) once the surface behind it could also be dark.

### Modal family

`PackageDetailModal`/`OrderDetailModal`/`BusinessDetail`'s own inline modal-like sections all share a `biz-modal-*`/`pkg-modal-*` class vocabulary (hero image banner, eyebrow label, fact-grid rows with an icon + label + value) — one visual grammar reused across three different data shapes rather than three bespoke modal designs. `ConfirmDialog`, `ReportDialog`, and the notification popup (`NotificationPanel`) form a second, simpler family: a `position: fixed`, viewport-centered card over a click-to-dismiss backdrop, capped at `max-height: calc(100vh - 3rem)` with internal scroll so tall content can't push controls off a short viewport.

**`.em-popover` (UI revamp Phase 4)** generalizes that second family into a reusable form-field sheet rather than a fixed message/confirmation shape: same centered-overlay mechanics (`.em-popover-backdrop` reuses `cart-backdrop-in`, `.em-popover` reuses `confirm-dialog-in`) plus a header/body/footer skeleton (`.em-popover-header`, `.em-popover-close`, `.em-popover-body`, `.em-popover-toggle-row`, `.em-popover-footer`) any page can drop a handful of `<select>`/`<input>` fields into. `Home.razor`'s Filters button opens one (`.home-filters-panel` — kitchen type, sort, diet/allergen, Near me, Favorites, the max-price chip) and `OrderManagement.razor`'s Export CSV button opens a second, narrower one (`.orders-export-panel` — from/to date pickers) — both instances of the same shell rather than each page hand-rolling its own popover.

---

## 14. JS Interop Patterns

Three distinct patterns, escalating in complexity:

### 14.1 `window.EcoMeal` — the shared global namespace (`wwwroot/js/site.js`)

```js
window.EcoMeal = {
    positionDropdown(anchorEl, dropdownEl) { /* fixed-position math, flips upward if not enough room below, clamps both edges */ },
    timeZone() { try { return Intl.DateTimeFormat().resolvedOptions().timeZone; } catch { return null; } },
    geo: { getPosition() { /* navigator.geolocation → {lat, lng} Promise; resolves null, never rejects */ } },
    map: {
        render(elementId, markers) { /* creates/replaces a Leaflet map in #elementId, one pin per marker */ },
        destroy(elementId) { /* removes a previously rendered map instance, if any */ }
    },
    cart: { save(key, json), load(key), clear(key) },   // thin localStorage wrapper, every call swallows exceptions
    managedBusiness: { save(key, businessId), load(key) },   // same wrapper shape, backs ManagedBusinessContext (§5)
    theme: {
        get() { /* reads the live data-theme attribute — always set by the inline head script by now */ },
        set(theme) { /* writes the attribute + persists to localStorage, swallows storage exceptions */ },
        toggle() { /* flips light/dark, calls set(), returns the new value */ }
    },
    push: {
        isSupported() { /* "serviceWorker" in navigator && "PushManager" in window && "Notification" in window */ },
        registerAsync() { /* navigator.serviceWorker.register("/service-worker.js") — called once, below */ },
        getSubscriptionEndpoint() { /* current subscription's endpoint, or null — never prompts for permission */ },
        subscribe(publicKeyBase64) { /* Notification.requestPermission() then pushManager.subscribe(...); resolves {endpoint, p256dh, auth} or null */ },
        unsubscribe() { /* pushManager.getSubscription().unsubscribe(); resolves the endpoint that was removed, or null */ }
    },
    media: {
        checkPlaceholders() { /* probes each unchecked .biz-card-media[data-image-url] via new Image(), adds .biz-image-failed on failure */ }
    }
};

EcoMeal.push.registerAsync();   // fire-and-forget, runs on every page load regardless of auth state
```
Called via plain `IJSRuntime.InvokeAsync("EcoMeal.xyz", ...)` from C# (`AnchoredDropdown`, `ClientTimeZoneService`, `ManagedBusinessContext`, `Home.razor`'s "near me"/map view, `BusinessForm.razor`'s "use my location", `NotificationPanel`'s push toggle — §12) or inline `onclick="EcoMeal.cart.clear(...)"` HTML attributes (both layouts' logout buttons). No JS module isolation here — it's genuinely global, loaded once via a plain `<script>` tag, because every consumer needs it available synchronously from inline HTML attributes as well as from C#.

`geo.getPosition` wraps `navigator.geolocation.getCurrentPosition` in a `Promise` that always **resolves** (`null` on denial, timeout, or an unsupported browser) rather than ever rejecting — every C# caller (§6, §11) can `await` it and branch on a plain `null` check instead of wrapping the call in `try/catch`, and the UI degrades to an inline error message on any failure path instead of an unhandled exception. `map.render` lazily creates a `L.map(...)` the first time it's called for a given element ID and removes any previous instance first (`_instances[elementId]`), so repeatedly toggling `Home.razor`'s map view on/off doesn't leak Leaflet instances or double-initialize the same `<div>`. Each marker's popup HTML is built with a small hand-rolled `escapeHtml` — business names come from the database, not user input, but nothing stops an admin from typing HTML-significant characters into one, so the popup still escapes them rather than trusting the value.

`push.subscribe`/`unsubscribe` follow the same "never reject, resolve to `null` on any failure" convention as `geo.getPosition` — denied permission, no service worker, or an unsupported browser all just mean `NotificationPanel`'s toggle shows its error text instead of an unhandled promise rejection. `push.registerAsync()` runs once at the bottom of `site.js` itself (not from a component), since service-worker registration needs no permission and has nothing to do with whether the viewer ever opens the notification panel — `wwwroot/service-worker.js` (a separate, un-bundled script, registered at scope `/`) owns the actual `push`/`notificationclick` event handling once registered; see `BACKEND_ARCHITECTURE.md` §3 PushSubscription for the server side that triggers it.

`media.checkPlaceholders` is called once at the bottom of `site.js` and again from a `MutationObserver` on `document.body`, rather than from any C# component — CSS `background-image` has no failure event, so revealing `Home.razor`'s (§6) card placeholder icon needs an actual JS load attempt, and re-running it on DOM mutations is what catches cards rendered after the initial page load (pagination, search/filter) without Blazor needing its own callback into JS.

`theme` (UI revamp Phase 2) only has to *stay in sync* with the DOM after first paint — the actual light/dark decision on a fresh page load is made by a separate, un-bundled inline `<script>` right in `App.razor`'s `<head>` (before `site.js` has even loaded), since deciding before first paint is the entire point and this file loads at the bottom of `<body>`, too late to prevent a flash. Both scripts agree on the same `localStorage` key (`em-theme`) and the same resolution order (explicit stored choice, else `prefers-color-scheme`), so `theme.get()` can stay a one-line attribute read instead of re-running that fallback logic a second time.

### 14.2 Stock framework template — `ReconnectModal.razor.js`

The default ASP.NET Core Blazor Web App reconnect-UI script, imported as a JS **module** (`<script type="module" src="...">`) per the framework's own convention. Unmodified — included here only because it's the only *other* JS module in the project besides the custom one below, and it's worth knowing it's stock rather than assuming it was hand-written for this app.

### 14.3 Custom lifecycle module — `OrderScan.razor.js`

The one genuinely custom JS module, and the first "start/stop lifecycle" one in the project (as opposed to `ReconnectModal`'s static event-listener style). Imported dynamically, on demand, from C#:
```csharp
_module ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/OrderScan.razor.js");
await _module.InvokeVoidAsync("startCamera", _videoElement, _canvasElement);
...
await _module.InvokeVoidAsync("stopCamera");
await _module.DisposeAsync();
```
Runs the entire `getUserMedia` → `<canvas>` frame grab → `jsQR` decode loop via `requestAnimationFrame`, entirely in JS, with **zero per-frame calls back into the Blazor Server circuit** — a deliberate performance/latency choice, since round-tripping every decoded video frame over SignalR would be both slow and pointless when the only thing that ever needs to reach the server is the final decoded URL, delivered via a real browser navigation instead (see §10).

### 14.4 The one script that never runs on this app's own pages — `impact-widget.js` (Impact Phase 1)

Every script above is loaded by *this* app's own `.razor` pages and talks to a live Blazor circuit (or, for `OrderScan.razor.js`, to media APIs on behalf of one). `wwwroot/js/impact-widget.js` is the opposite: it's never referenced by anything under `Components/`, and it isn't part of the `window.EcoMeal` namespace (§14.1) at all — it's a standalone IIFE meant to be copy-pasted onto a **business's own website** (the snippet `/dashboard`'s "Share your impact" panel hands out, §11), running there with no Blazor circuit, no `IJSRuntime`, and no shared state with this app whatsoever.

```javascript
var businessId = scriptTag.getAttribute('data-business-id');
var baseUrl = scriptTag.getAttribute('data-base-url') || new URL(scriptTag.src).origin;
fetch(baseUrl + '/api/businesses/' + encodeURIComponent(businessId) + '/impact')
    .then(res => res.json())
    .then(data => { card.innerHTML = /* kg saved + CO2e/water equivalency, built with textContent-escaped values */ });
```

Reads its target business off its own `<script>` tag's `data-business-id` attribute (`document.currentScript`) rather than a global config object, since there's no shared page state to read it from on an arbitrary third-party site. Defaults `data-base-url` to `new URL(scriptTag.src).origin` — the same origin the script itself was loaded from — so the snippet Dashboard.razor generates just works without the manager having to fill in a second attribute; it's an explicit override only for an unusual hosting setup (script served from a CDN in front of the app, say). All styling is inline (`card.setAttribute('style', ...)`) rather than a linked stylesheet, since the host page's own CSS is completely unknown and unpredictable. User-supplied text (the business name) is escaped through a scratch `<div>`'s `.textContent`/`.innerHTML` round-trip before being interpolated into the card's HTML — the same escaping idiom, just inlined here since there's no framework helper available in a bare script. The fetch call is exactly the cross-origin request `Program.cs`'s one CORS policy (`BACKEND_ARCHITECTURE.md` §6) exists for — every other fetch/JS-interop call anywhere else in this app is same-origin.

---

## 15. Key Frontend Design Decisions

### Debouncer — working around one DbContext per circuit

```csharp
public class Debouncer
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cts;

    public async Task DebounceAsync(Func<Task> action, int delayMs = 0)
    {
        var previous = _cts;
        _cts = new CancellationTokenSource();
        previous?.Cancel();
        ...
        await _gate.WaitAsync();
        try { if (!cts.IsCancellationRequested) await action(); }
        finally { _gate.Release(); }
    }
}
```
Every paginated list page in the app (`Home`, `Orders`, `Businesses`, `Packages`, `Users`, `OrderManagement`) owns a `private readonly Debouncer _reloadGate = new();` and routes **every** reload trigger — search input, filter dropdowns, pagination clicks, post-mutation refreshes — through it. This exists because Blazor Server shares **one `EcoMealDbContext` instance per circuit**: two overlapping queries against it throw `InvalidOperationException` ("a second operation was started..."). Cancelling a superseded delay alone isn't sufficient — two triggers both fired with `delayMs: 0` (e.g. rapid pagination clicks) could still race each other's actual DB call, so `Debouncer` also serializes the `action()` calls themselves via the semaphore, on top of cancelling stale pending ones.

### QR validate route hardening

Covered in depth in §10 — worth restating here as a general principle: any code path that turns *untrusted decoded input* (a QR code's payload) into a navigation target must validate both origin and exact path shape before navigating, never just "looks like a URL." `OrderScan.razor.js`'s `tryNavigate` is the concrete instance of this in the codebase.

### Date formatting must opt out of the server's own culture

`Program.cs` sets the process-wide culture to `ro-RO`, needed so `.ToString("C")` renders prices as `lei` currency (`BACKEND_ARCHITECTURE.md` §10) — but that same culture setting silently leaks into every *other* `.ToString()` call on the same thread, including dates. A bare `pickupStart.ToString("MMM d, HH:mm")` renders through `ro-RO` as a lowercase, Romanian-abbreviated month ("sept." instead of "Sep") — technically correct for the culture, wrong for an app whose UI copy is otherwise all English. Every `MMM`/`HH:mm`-style date format across the page set (`Orders.razor`, `OrderManagement.razor`, `OrderPickupPass.razor`, `OrderValidate.razor`, `Dashboard.razor`, `Payments.razor`, `Reports.razor`, `AuditLog.razor`, `PackageTemplates.razor`, `BusinessDetail.razor`, `BusinessForm.razor`, `StarRating.razor`) now passes `CultureInfo.InvariantCulture` explicitly, or — for interpolated strings, where there's no single `.ToString(culture)` call to pass it to — wraps the whole expression in `FormattableString.Invariant($"...")`. `Components/_Imports.razor` carries a global `@using System.Globalization` so every `.razor` file can reach `CultureInfo` without its own `@using`. The Dashboard's `70,0%` progress-bar-width bug (§11 Business Analytics card) is the sibling failure mode of this same root cause — a decimal, not a date, hitting the same culture leak.

### Reorder needed (almost) no new backend surface

Worth calling out as a general lesson: because `CartService.AddAsync` already clamped quantity to live stock, and `Order.OrderPackages` was already eagerly loaded everywhere an `Order` is fetched, the "Order again" feature (§9) shipped almost entirely as frontend orchestration over existing primitives — no new `OrderService` method. Not every feature needs a symmetric backend addition; check what already exists before assuming a new endpoint is required.

That said, "eagerly loaded" turned out to have a gap: `OrderRepository.OrdersWithIncludes()` (`BACKEND_ARCHITECTURE.md` §4) included `OrderPackages.Package` but not `Package.Business`, so `CartService.AddInternal`'s `package.Business.Name` read could `NullReferenceException` on reorder — intermittently, since EF's change tracker sometimes backfilled it anyway from the same query's `Order.Business` include. The lesson generalizes: "this data is already loaded" is a claim about the top-level query, not proof that every nested navigation property a *different* consumer (`CartService`, written with `BusinessDetail.razor`'s fully-loaded `Package` in mind) expects is actually populated. Fixed with one added `.ThenInclude(p => p.Business)`, not a new query.

### CSV export had to bypass the in-process controller pattern

The one place in the whole app where the frontend can't just `@inject` a controller and call a method directly — `OrderManagement.razor`'s export link has to be a real `<a href>` to a real HTTP `GET`, because file downloads are a browser/HTTP-level concept with no in-process equivalent. See `BACKEND_ARCHITECTURE.md` §6 for the backend-side consequence (a controller that resolves identity from `HttpContext.User` instead of the usual `CurrentUserAccessor`).

### Geolocation always degrades, never blocks

`EcoMeal.geo.getPosition` resolving to `null` instead of rejecting (§14) is a deliberate contract, not an implementation shortcut: browser geolocation can fail for reasons entirely outside the app's control (permission denied, no hardware, a slow/absent GPS fix, a corporate policy blocking it outright), and none of those should be able to break the page it's called from. `Home.razor`'s "near me" toggle and `BusinessForm.razor`'s "use my location" both treat a `null` result identically — show an inline error string, leave everything else exactly as it was before the click. `TripPlanner.razor` (Phase 15) is the first caller with an actual functional fallback rather than just an error message: a `null` result still produces a usable route, just starting from the first stop instead of the customer's own position. Contrast with a typical SPA pattern of a rejected promise bubbling into an error boundary; here there's no boundary to catch it; the JS side absorbs the failure so the C# side almost never has to. The one place it still can: the JS-side 8s timeout only bounds `getCurrentPosition` itself, not the JS-interop round-trip carrying its result back over the SignalR circuit, which was found to occasionally hang indefinitely. `TripPlanner.razor` wraps that await in `Task.WhenAny(..., Task.Delay(10s))` as a hard server-side ceiling, so the "trying to start the route" spinner always resolves — to a real position, or the same first-stop fallback as an outright JS-side failure — either way.

### A framework component can race the user it's meant to help

`<FocusOnNavigate>` calls `.focus()` unconditionally on every route change to move focus to the page heading for screen readers — reasonable in isolation, but that call is itself a JS-interop round-trip over Blazor Server's SignalR circuit, so it can land a second or two after the page "looks" ready. A user who clicks straight into a form field and starts typing in that window has their input silently redirected to the (non-editable) heading, losing keystrokes with no visible cause. `SafeFocusOnNavigate` (§3) fixes this the cheap way — `EcoMeal.a11y.focusIfIdle` in `site.js` only moves focus if `document.activeElement` is still `<body>`/`<html>`, i.e. nothing else has claimed it yet — rather than trying to time or cancel the original call. The general lesson: a framework convenience that reaches into shared browser state (focus, scroll position, selection) on a timer or a round-trip you don't control needs the same "did something else already happen" guard you'd write by hand.

### "Available" quantity needed a live/local split, not just a local calculation

`CartService.AvailableQuantity` originally computed everything from data already in memory — `package.Quantity` minus the viewer's own cart contents — which is correct for "don't let this browser add more than it can check out" but wrong for "how much is actually left," since it has no way to see Pending reservations sitting in the database (this browser's just-placed order included, once it clears the cart on checkout, or any other customer's). Fixing the *display* meant adding a genuinely new read path — `OrderController.GetPendingReservedQuantitiesAsync`, bulk-fetched once per page load alongside the package list (§6, §7) — rather than something derivable from state the page already had. Contrast with reorder (above), which needed nothing new precisely because the local state already had everything required; the lesson generalizes in both directions — check what's genuinely knowable client-side before reaching for a new endpoint, but don't force a client-only fix onto a value that depends on server state no client-side signal can substitute for.
