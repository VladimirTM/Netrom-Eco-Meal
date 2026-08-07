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
│   ├── MainLayout.razor          # Sidebar shell for /dashboard, /businesses, /packages, /orders/manage, /users
│   ├── NavMenu.razor             # The sidebar itself — role-aware nav links + user footer
│   ├── PublicLayout.razor        # Header + footer shell for the customer storefront
│   ├── EmptyLayout.razor         # Chrome-free — Login/Register only
│   └── ReconnectModal.razor(.css/.js)  # Stock Blazor Server reconnect UI (framework template, not customized)
│
├── Pages/
│   ├── Home.razor                # / — storefront browse/search/filter
│   ├── BusinessDetail.razor      # /businesses/{Id} — packages + reviews + favorite + add to cart
│   ├── Orders.razor              # /orders — customer order history + cancel + reorder
│   ├── OrderPickupPass.razor     # /orders/pickup/{Id} — QR code for a Confirmed order
│   ├── OrderScan.razor(.js)      # /orders/scan — manager camera scanner
│   ├── OrderValidate.razor       # /orders/validate/{Id} — confirm-pickup landing page (scanned or typed)
│   ├── Login.razor / Register.razor
│   ├── ForgotPassword.razor / ResetPassword.razor / ConfirmEmail.razor
│   ├── AccessDenied.razor / NotFound.razor / Error.razor
│   ├── Dashboard.razor           # /dashboard — stat cards + 14-day trend chart
│   ├── Businesses.razor / BusinessForm.razor
│   ├── Packages.razor / PackageForm.razor
│   ├── PackageTemplates.razor    # /packages/templates — recurring "repeat daily" template management
│   ├── OrderManagement.razor     # /orders/manage — confirm/complete/cancel + CSV export
│   └── Users.razor               # /users — role + business-manager assignment
│
└── Shared/
    ├── AnchoredDropdown.razor    # Generic trigger+panel dropdown, JS-positioned to escape overflow clipping
    ├── ConfirmDialog.razor       # Generic confirm/cancel modal
    ├── ForbiddenPanel.razor / NotFoundPanel.razor
    ├── NotificationBell.razor   # Polling bell dropdown, used in both layouts
    ├── OrderDetailModal.razor / PackageDetailModal.razor
    ├── Pagination.razor
    ├── StarRating.razor         # Read-only fractional-fill display + editable 1-5 picker, same component
    └── CartPanel.razor          # Slide-in basket + checkout

Constants/  Models/               # Debouncer, PaginatedList<T>, GeoDistance — see below and BACKEND_ARCHITECTURE.md
wwwroot/
├── app.css                       # ~3100 lines, one file, no preprocessor — see §13
├── js/site.js                    # window.EcoMeal namespace — see §14
└── lib/{bootstrap, jsqr}/        # Vendored, checked-in dependencies (no package.json) — Leaflet is the one
                                   # exception, loaded from a CDN instead (§2), since it's used on one page
```

---

## 2. Bootstrap & Entry Point

**File:** `Components/App.razor`

```razor
<head>
    ...
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

`@rendermode="InteractiveServer"` on `<Routes>` is what turns the *entire* app interactive over one persistent SignalR circuit — every `@page` in this project also carries its own explicit `@rendermode InteractiveServer`, which is redundant given the ancestor already set it, but keeps each page's rendering mode visible and self-documenting at the point of use rather than requiring a reader to know it's inherited from `App.razor`. `Netrom-Eco-Meal.styles.css` is the auto-generated bundle of every `*.razor.css` CSS-isolation file in the project (currently just `MainLayout.razor.css`, `NavMenu.razor.css`, `ReconnectModal.razor.css`) — everything else styles through the single global `app.css`, deliberately, so the visual language stays in one place instead of fragmenting across dozens of scoped stylesheets (see §13).

Leaflet is the one third-party JS dependency in the whole app, loaded the same way `bootstrap-icons` already was — a plain CDN `<link>`/`<script>` pair with a real `integrity` hash (SRI), no npm install, no bundler step. It's only ever exercised by `Home.razor`'s map view toggle (§6), so pulling it in via `wwwroot/lib/` like Bootstrap/jsQR would mean vendoring a dependency the rest of the app never touches for no real benefit.

**`<HeadOutlet>` needs its own `@rendermode`, matching `<Routes>`'s.** `<Routes @rendermode="InteractiveServer">` only makes the `<Routes>` subtree interactive — `<HeadOutlet>` lives in `<head>`, outside that subtree, as a *separate* render tree that `<PageTitle>`/`<HeadContent>` write into. Without its own render mode it stays static, rendered once from the initial prerender and never again: the tab title (and anything else pushed through `<PageTitle>`) would freeze at whatever the very first page loaded, silently going stale on every subsequent in-app navigation. Both call sites need to agree on the render mode for the header to stay live.

`ResourcePreloader`/`ImportMap`/`HeadOutlet` are stock ASP.NET Core 10 Blazor Web App template tags, unmodified.

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
        <FocusOnNavigate RouteData="routeData" Selector="h1"/>
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

`DefaultLayout="typeof(Layout.MainLayout)"` only applies when a page doesn't specify its own `@layout` — every customer-facing page overrides it with `@layout PublicLayout`, and the two auth pages with `@layout EmptyLayout` (see §4).

`NotFoundPage="typeof(Pages.NotFound)"` handles client-side "no route matched." Separately, `Program.cs` wires `app.UseStatusCodePagesWithReExecute("/not-found", ...)` so a raw HTTP 404 (a bad static-asset path, etc.) re-executes the same page rather than showing a different one — the two 404 paths (router-level vs status-code-level) converge on identical UI.

One consequence worth knowing: when `NotFoundPage` renders via the Router's in-circuit fallback (no fresh HTTP request backing it), any `<AntiforgeryToken/>` on that render — e.g. the header's Sign Out form, present on every layout — has no request-scoped `HttpContext` to mint a token against, and renders no hidden field at all. See `BACKEND_ARCHITECTURE.md` §7 for why that's handled by not requiring antiforgery on logout, rather than by fighting this rendering path.

---

## 4. Layouts

| Layout | Used by | Shell |
|---|---|---|
| `PublicLayout` | Home, BusinessDetail, Orders, OrderPickupPass, AccessDenied, NotFound | Sticky header (logo, notification bell, orders link, basket button + badge, dashboard link if staff, logout), `@Body`, footer. Owns the `CartPanel` and its open/closed state |
| `MainLayout` | Dashboard, Businesses(+Form), Packages(+Form), OrderManagement, Users, OrderScan, OrderValidate | Fixed left sidebar (`NavMenu`) + `<main>` content area — the classic admin-panel shell |
| `EmptyLayout` | Login, Register | Just `@Body` — no header, no sidebar, no footer; the login/register cards center themselves entirely via `app.css`'s `.login-page`/`.login-card` |

### PublicLayout — the customer chrome

```razor
@inject CartService CartService
@inject ClientTimeZoneService ClientTimeZoneService
...
<NotificationBell TriggerClass="public-cart-btn"/>
<a href="/orders" class="public-cart-btn">...</a>
<button @onclick="ToggleCart">
    <i class="bi bi-basket2"></i>
    @if (CartService.TotalCount > 0) { <span class="public-cart-badge">@CartService.TotalCount</span> }
</button>
...
<CartPanel @bind-IsOpen="_cartOpen"/>

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

### NavMenu — role-aware sidebar

Plain `<AuthorizeView Roles="@AppRoles.Admin">` gates the "User Roles" nav link; every other link (Dashboard, Businesses, Packages, Orders) is visible to both `Admin` and `BusinessManager` — the actual data scoping (a manager only sees the business or businesses they're staff of) happens page-side, not by hiding nav links per role. The sidebar footer shows the signed-in user's initial-avatar, email, and a role label resolved by a local `DisplayRole` switch expression, plus the same `NotificationBell` the public header uses (`TriggerClass="sidebar-notif-btn"` swaps only the CSS class, same component).

**Business switcher**: for a `BusinessManager`, `AuthorizeView Roles="@AppRoles.BusinessManager"` wraps a block driven by `ManagedBusinessContext.MyBusinesses.Count` — zero businesses renders nothing, one renders a plain "current business" label, and more than one renders an `AnchoredDropdown` (`role-badge`-styled, matching the sidebar's other pill controls) listing every business the manager staffs, with a check mark on `ManagedBusinessContext.SelectedBusinessId`. `NavMenu.OnInitializedAsync` calls `ManagedBusinessContext.EnsureLoadedAsync()` and subscribes to its `OnChange` the same way every other consumer of a cross-cutting client service does (§5) — picking a different business calls `ManagedBusinessContext.SelectAsync`, which persists the choice and fires `OnChange`, and every business-scoped page (Dashboard, Businesses, Packages, PackageForm, PackageTemplates, OrderManagement) re-renders scoped to the new selection without a page navigation.

---

## 5. Cross-Cutting Client Services

Blazor Server has no React-style Context API, but three `Scoped` services fill the identical role — injected wherever needed, holding state for the circuit's lifetime, and exposing a plain C# `event Action? OnChange` that consuming components subscribe to in `OnInitialized`/unsubscribe in `Dispose` (`IDisposable`) so a mutation anywhere re-renders every interested component without prop-drilling.

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

---

## 6. Home Page

**File:** `Components/Pages/Home.razor` — `@page "/"`, `@layout PublicLayout`

The storefront. Loads `_livePackages` (all packages with `PickupEnd > now`, for the hero's live stats) and the paginated business grid (`BusinessController.GetPagedAsync`) independently — search/filter/sort state (`_search`, `_businessTypeFilter`, `_sortBy`, `_favoritesOnly`) all funnel through a single `Debouncer`-gated `ReloadAsync` (see §15) exactly like every other paginated admin list page in this app.

- **Search matches live packages, not just business fields** — see `BusinessRepository.GetPagedAsync` in `BACKEND_ARCHITECTURE.md` §4; the input has a 300ms debounce (`OnSearchInputAsync` passes `delayMs: 300` to the shared `Debouncer`) so it doesn't re-query on every keystroke.
- **"Closing soon" sort** — `BusinessSortOptions.ClosingSoon`, resolved server-side.
- **Favorites-only filter** — `AuthorizeView Roles="Customer"` around the toggle button; `ToggleFavoriteAsync` optimistically updates the local `_favoriteBusinessIds` set, then re-runs `ReloadAsync()` **only if** `_favoritesOnly` is currently active (unfavoriting a business while that filter is on would otherwise leave a stale card visible until the next unrelated reload).
- Ratings are batch-loaded per visible page (`ReviewController.GetByBusinessesAsync(businessIds)`, grouped into a `Dictionary<Guid, List<Review>>`) rather than one query per card — the same anti-N+1 shape `BACKEND_ARCHITECTURE.md` calls out for `GetByBusinessIdsAsync`.
- The hero's "portions to save" stat sums `CartService.AvailableQuantity(p, reservedByPackage.GetValueOrDefault(p.Id))` across `_livePackages`, where `_reservedByPackage` comes from one bulk `OrderController.GetPendingReservedQuantitiesAsync` call alongside the live-package fetch — without it the stat over-counted stock already tied up in other customers' Pending orders (see §15).
- Clicking a card calls `NavigationManager.NavigateTo($"/businesses/{id}")` — cards are rendered as `<button>` elements (not `<a>`) specifically so the per-card favorite-heart button can `@onclick:stopPropagation="true"` without fighting an anchor's default navigation.

### "Near me" distance sort

```csharp
private async Task ToggleNearMeAsync()
{
    if (_sortBy == BusinessSortOptions.Distance) { _sortBy = BusinessSortOptions.Name; await FilterChangedAsync(); return; }

    _locatingNearMe = true;
    var position = await JSRuntime.InvokeAsync<GeoPosition?>("EcoMeal.geo.getPosition");
    if (position is null) { _locationError = "Couldn't get your location — check your browser's location permission."; return; }

    (_customerLat, _customerLng) = (position.Lat, position.Lng);
    _sortBy = BusinessSortOptions.Distance;
    await FilterChangedAsync();
}
```
A toggle button, not a plain sort-dropdown option — selecting "distance" needs a customer coordinate the server doesn't have, so clicking it first requests browser geolocation (`EcoMeal.geo.getPosition`, §14) and only switches `_sortBy` once a position actually comes back. `EcoMeal.geo.getPosition` resolves to `null` rather than throwing on denial/timeout/unsupported (§14), so the failure path here is an ordinary `if`, not a `try/catch` — the sort dropdown itself only shows a "Nearest" `<option>` once `_customerLat` is set, so there's no way to select a sort mode the page can't yet fulfill. `_customerLat`/`_customerLng` are passed straight through to `BusinessController.GetPagedAsync` on every subsequent reload while this sort is active — see `BusinessRepository.GetPagedAsync`'s in-memory Haversine branch in `BACKEND_ARCHITECTURE.md` §4. Each business card also shows its own "X km away"/"X m away" badge (`GeoDistance.Km`, computed client-side against `_customerLat`/`_customerLng` purely for display) whenever both the customer's position and that business's `Latitude`/`Longitude` are known — independent of which sort mode is active, so the badge can show up even while sorted by name.

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

---

## 7. Business Detail Page

**File:** `Components/Pages/BusinessDetail.razor` — `@page "/businesses/{Id:guid}"`, `@layout PublicLayout`

Three independent concerns on one page: the business's live packages, its reviews, and (for customers) favoriting + cart actions.

- **Cross-business basket guard**: `AddToCart(package)` checks `CartService.WouldReplaceCart(package)` first; if true, it stashes the package in `_pendingSwitch` and shows a `ConfirmDialog` ("Start a new basket?") instead of adding immediately — confirming calls `CartService.AddAsync` and clears the old basket as a side effect (documented in `CartService.AddInternal`, §5). This exact pattern is reused verbatim by `Orders.razor`'s reorder feature (§9).
- **Review gating**: `_reviewContext.CanReview` (from `ReviewController.GetContextAsync`) controls whether the review form renders at all vs. a "order from X and come back to leave a review" hint — the *actual* enforcement is server-side in `ReviewService.SubmitAsync`; the client-side gate is purely to avoid showing a form that would just reject on submit.
- **Package list is pre-filtered to live packages** client-side after the paginated fetch (`PickupEnd > DateTime.UtcNow`) and re-sorted by soonest `PickupEnd` — the backend's `GetPagedAsync` for packages doesn't have a "live only" filter of its own, so this page asks for a large page size (100) and filters in memory rather than adding a new backend parameter for a single call site.
- **"X left" accounts for pending reservations, not just the local cart** — right after loading `_packages`, the page fetches `_reservedByPackage` (one bulk `OrderController.GetPendingReservedQuantitiesAsync` call) and passes each package's reserved total into `CartService.AvailableQuantity(package, reserved)`, both in the package row and in the `PackageDetailModal` parameter. Before this existed, `AvailableQuantity` only knew about the *viewer's own* local cart contents, so a package's displayed count would revert to the full, un-reserved number the moment a Pending order's items left the local cart (e.g. right after checkout) — misleading regardless of whether the reservation was the viewer's own order or someone else's (see §15).
- `PackageDetailModal` (§12) is the drill-down when a package row is clicked, reusing the same `AddToCart` handler.

---

## 8. Cart Panel & Checkout

**File:** `Components/Shared/CartPanel.razor`

A slide-in `<aside>` rendered by `PublicLayout`, controlled by a two-way-bound `IsOpen` parameter (`@bind-IsOpen="_cartOpen"` in the layout, toggled by the header's basket button). Three states in one component: empty basket, active basket (line items with `+`/`-`/remove, running total, "Place order"), and a post-order confirmation screen (order number + kg-saved impact stat) shown in place of the basket. `Close()` — wired to "Done," the X icon, *and* the backdrop click — always clears the three `_placed*` confirmation fields before closing, not just on "Done": leaving them set after an X/backdrop dismiss meant reopening the panel later (even with a fresh, non-empty cart) re-rendered the *old* confirmation instead of the live basket, since the confirmation branch is checked first in the render tree. One method now, not two (`CloseAndReset()` was folded into `Close()` — there was never a real reason for the two dismiss paths to behave differently).

```csharp
private async Task PlaceOrderAsync()
{
    var lines = CartService.Items.Select(i => new OrderLineRequest(i.Package.Id, i.Quantity)).ToList();
    var result = await OrderController.PlaceOrderAsync(businessId, lines);

    if (result.Result is ConflictObjectResult conflict)
    {
        _error = conflict.Value?.ToString() ?? "We couldn't place your order. Please try again.";
        return;
    }

    _placedOrderNumber = result.Value?.OrderNumber;
    _placedKgSaved = CartService.Items.Sum(i => i.Quantity * i.Package.WeightKg);
    await CartService.ClearAsync();
}
```
The `ConflictObjectResult` check is exactly how the rate-limit error, stock-conflict errors, and any other `OrderService.PlaceOrderAsync` exception surface to the customer — one `if`, no exception-type-specific handling needed client-side, because `OrderController.PlaceOrderAsync` already collapsed every relevant exception into `Conflict(ex.Message)` (see `BACKEND_ARCHITECTURE.md` §6). `_placedKgSaved` is computed **client-side** from the cart items about to be cleared, purely for instant display — the authoritative, persisted "kg saved" stat only actually counts once the order reaches `Completed` (see `OrderService.GetTotalKgSavedAsync`).

---

## 9. Orders Page & Reorder

**File:** `Components/Pages/Orders.razor` — `@page "/orders"`, `@layout PublicLayout`

Customer order history: a lifetime-stats hero (orders placed / portions rescued / kitchens visited / kg saved — computed from an **unfiltered** full order list loaded once) above a paginated, status-filterable ticket list (a **separate**, server-paged query — `OrderController.GetMyOrdersPagedAsync`). Both loads are necessary because the hero's totals must reflect *all* of a customer's history regardless of which status filter chip is currently active on the visible list.

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

---

## 10. Pickup Pass, QR Scan & Validate

A three-page flow that hands a physical pickup confirmation off to a QR code, deliberately designed so each page works correctly no matter how the visitor actually arrived at it.

```
Customer's phone                          Manager's phone/device
─────────────────                          ──────────────────────
OrderPickupPass.razor
  (Confirmed order only)
  generates an SVG QR encoding
  {BaseUri}/orders/validate/{orderId}
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
                                             "Confirm pickup" → Completed
```

### OrderPickupPass — QR generation

Server-side, via the `QRCoder` NuGet package:
```csharp
var payloadUrl = $"{NavigationManager.BaseUri}orders/validate/{_order.Id}";
var qrData = new QRCodeGenerator().CreateQrCode(payloadUrl, QRCodeGenerator.ECCLevel.Q);
_qrSvg = new SvgQRCode(qrData).GetGraphic(5, "#0b1f13", "#ffffff", true, SvgQRCode.SizingMode.ViewBoxAttribute);
```
Rendered inline via `@((MarkupString)_qrSvg!)`. Only shown when `Order.Status == Confirmed` — every other status renders an explanatory empty state instead (`StatusExplanation` switch) rather than a broken/stale QR code.

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
const VALIDATE_PATH_PATTERN = /^\/orders\/validate\/[0-9a-fA-F-]{36}$/;

function tryNavigate(decoded) {
    const url = new URL(decoded, location.origin);   // throws → not a URL at all, ignore
    if (url.origin !== location.origin || !VALIDATE_PATH_PATTERN.test(url.pathname)) return false;
    stopCamera();
    location.assign(url.href);
    return true;
}
```
This is a deliberate security boundary, not just parsing convenience: a maliciously crafted QR code (or a genuine QR from some other app pointed at this camera by mistake) **cannot** redirect an authenticated manager's session off-app, because `tryNavigate` only ever calls `location.assign` for same-origin URLs matching the exact `/orders/validate/{guid}` shape — anything else is silently ignored and the scan loop just keeps running. A successful match does a **real browser navigation** (`location.assign`), not a callback into the Blazor circuit — the scan result reaches `OrderValidate.razor` the same way a manually-typed URL would.

### OrderValidate — the landing page

```razor
@* Reachable both from the in-app scanner and by any external QR reader opening this URL directly —
   must be fully self-sufficient and re-check authorization itself, never trust how the visitor arrived. *@
```
Calls `OrderController.GetOrderForManagementAsync(Id)` on load — the exact same ownership check (`OrderService.GetOwnedOrderAsync`) any other manager-facing order read uses, regardless of whether this page was reached via the in-app scanner, a manually typed URL, or a third-party QR scanner app opening the link directly. "Confirm pickup" transitions the order to `Completed`; a `ConflictObjectResult` (someone else already completed/cancelled it — a duplicate scan, a race with the manager dashboard) re-fetches the order rather than leaving a stale status badge on screen.

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

### Businesses / BusinessForm

`Businesses.razor` is the admin/manager list — admins see every business and can create new ones; a `BusinessManager` sees only the business(es) they're staff of (`staffUserId` passed to `BusinessController.GetPagedAsync`) and can edit any of them. Staff are shown as removable chips per row (`business.Staff.OrderBy(s => s.User.Name)`, each with a small `×` calling `BusinessController.RemoveStaffAsync`) plus an admin-only `AnchoredDropdown` "add staff" trigger listing every `BusinessManager` not already on that row — picking one calls `AddStaffAsync` and **deliberately doesn't close the dropdown**, so an admin can add several staff in one open. `BusinessForm.razor` serves both `/businesses/create` (admin-only) and `/businesses/edit/{id}` (admin or one of the business's own staff, via `IsStaffAsync`) behind one component, branching on whether `Id` was supplied (`IsEdit`) — staff assignment itself lives on `Businesses.razor`/`Users.razor`, not on this form.

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

### Packages / PackageForm / PackageTemplates

Same list/form split as Businesses, plus:
- `Packages.razor`/`PackageTemplates.razor` scope a manager to `ManagedBusinessContext.SelectedBusinessId` (updated live via its `OnChange`, same subscription pattern as Dashboard) via a **sentinel `Guid.Empty`** fallback (`_myBusinessId ?? Guid.Empty`) when nothing's selected yet — passing `null` there would instead show *every* business's packages, which is the opposite of the intended scoping. `PackageForm.razor`'s create path uses the same selection to default `_model.BusinessId`, but its **edit** authorization check is deliberately broader: it calls `BusinessService.IsStaffAsync(existing.BusinessId, currentUserId)` against the package's *own* business, not just whichever one is currently selected in the switcher — so a manager can still open and edit a package that belongs to a different business they staff, without first switching to it.
- `PackageForm.razor`'s dietary-tag picker is a checkbox grid over `Constants.DietaryTags.All`, toggling membership in `_model.DietaryTags` (a plain `List<string>`, no multi-select `InputSelect` involved).
- Cross-field pickup-window validation lives on the private `PackageFormModel : IValidatableObject` (pickup end must be after pickup start **and** in the future) — compared against `NowLocal`, a field the parent component keeps in sync with `ClientTimeZoneService`'s resolved local time, specifically so the "must be in the future" check uses the *viewer's* clock rather than the server's.
- `Packages.razor` shows a 🔁 "Daily" badge next to any package whose `TemplateId` is set (`BACKEND_ARCHITECTURE.md` §3), plus a "Recurring templates" link to `/packages/templates` alongside the existing "Add Package" button.

**Recurring templates** — a "Repeat this every day" checkbox, shown only on **create** (not edit, since a template is derived from one specific package's fields at creation time):
```csharp
await PackageController.AddAsync(package);
if (_model.RepeatDaily)
    await PackageTemplateController.CreateFromPackageAsync(package.Id, pickupStart.TimeOfDay, pickupEnd.TimeOfDay);
```
The package is created first (same as the non-recurring path), then the template is derived from it in a second call — `package.Id` is already populated at that point because EF Core client-generates `Guid` primary keys when an entity enters the `Added` state, not deferred until `SaveChangesAsync` (see `BACKEND_ARCHITECTURE.md` §3 PackageTemplate). `PackageTemplates.razor` (`/packages/templates`) is the standalone management page for existing templates — a flat table (name, daily window converted through `ClientTimeZoneService` the same way `Packages.razor` does, qty/day, last-generated date, active/paused status) with Pause/Resume (`SetActiveAsync`) and a `ConfirmDialog`-gated "stop repeating" (`DeleteAsync`) per row. No create form of its own — a template can only be created by ticking the checkbox while creating a package, never edited or created standalone, since it's meant to always start from a real package's current values.

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
This is a **plain `<a href>`**, not a button wired to `OrderController` — it has to be, since `OrderExportController` is a real HTTP endpoint and the browser needs to treat the response as a downloadable file (see `BACKEND_ARCHITECTURE.md` §6). The rest of the page (search, status/business filters, confirm/complete/cancel actions) follows the identical `Debouncer`-gated paged-list pattern every other admin list page uses.

A manager's `businessId` (both for the paged list and for `ExportHref` above) comes from `ManagedBusinessContext.SelectedBusinessId`, not a page-local dropdown — an admin instead picks from `_businessFilter`, a plain `<select>` over every business. `OnInitializedAsync` subscribes to `ManagedBusinessContext.OnChange` (`HandleManagedBusinessChanged`, resetting `_pageIndex` back to 1 before reloading) so switching businesses in `NavMenu` refreshes the order queue in place. If a manager staffs zero businesses, `GetOrdersForManagementPagedAsync` returns `UnauthorizedResult` and the page renders `ForbiddenPanel` ("You don't manage a business yet…") instead of an empty table.

### Users

Admin-only role management. Two independent `AnchoredDropdown`s per row (role, and — only for `BusinessManager` rows — business assignment), deliberately mutually exclusive (closing one when the other opens, so only one is ever open at a time). The business column is the same removable-chips-plus-add-dropdown shape `Businesses.razor`'s Staff column uses (§11 above), just inverted — one row per user, chips for every business they're staff of (`AssignedBusinesses(user.Id)`), and an "add business" `AnchoredDropdown` listing every business not already assigned that **stays open across multiple picks**, same as the Staff column. Both call the same `BusinessController.AddStaffAsync`/`RemoveStaffAsync` `Businesses.razor` does — there's exactly one staff-assignment backend surface, just two admin entry points into it (by business, or by user). Role changes and business (re)assignments both re-fetch the user list afterward, since `UserService.UpdateRoleAsync` can auto-release every business a user staffed server-side as a side effect of a role change away from `BusinessManager` (see `BACKEND_ARCHITECTURE.md` §7) — the UI has no way to know that happened without asking again.

---

## 12. Shared Components

| Component | Role |
|---|---|
| `AnchoredDropdown` | Generic trigger + floating panel. `OnAfterRenderAsync` calls `EcoMeal.positionDropdown(anchorRef, panelRef)` **once per open** (`_positionedForCurrentOpen` guard prevents repositioning on every re-render, which would make it visibly jump while scrolling) — see §14 for the JS side. Used for both the Businesses/Users manager-assignment pickers and `NotificationBell` |
| `ConfirmDialog` | Generic confirm/cancel modal — delete confirmations, the cross-business "start a new basket?" prompt, cancel-order confirmations. `Busy` disables both buttons and swaps the confirm label for a spinner mid-request |
| `ForbiddenPanel` / `NotFoundPanel` | Inline empty-state panels — the former for "wrong role," the latter for "this specific entity no longer exists" (distinct from the global 404 route, used by edit pages when a fetched-by-ID entity comes back null) |
| `NotificationBell` | `AnchoredDropdown`-based. A `System.Threading.Timer` polls `GetMyUnreadCountAsync` every 30 seconds regardless of whether the dropdown is open, so the badge count stays fresh for e.g. a manager waiting on new orders; opening the dropdown separately fetches the actual list (`GetMyNotificationsAsync(20)`) on demand rather than keeping 20 rows in memory at all times |
| `OrderDetailModal` / `PackageDetailModal` | Drill-down modals from a ticket/row click — same visual shell (`biz-modal-*`/`pkg-modal-*` CSS classes), one shows order line items + status, the other a package's full description/tags/price with an "Add to basket" action |
| `Pagination` | Renders nothing at all when `TotalPages <= 1` — every paged list page is written to just drop the component in unconditionally rather than wrapping it in its own visibility check |
| `StarRating` | One component, two modes: `Editable=false` renders a fractional-fill overlay (two stacked 5-star rows, the top one clipped to `Value/5 * 100%` width) for display; `Editable=true` renders a real 1-5 click/hover picker. Both business cards and the review form use the same component, just with different parameters |

---

## 13. CSS Design System

**File:** `wwwroot/app.css` — a single ~3100-line stylesheet, no Sass/Less, no CSS-in-JS, no Tailwind. Organized into clearly delimited sections (`/* ── Section name ── */`), roughly in the order features were added:

```
Design tokens · Buttons · Sidebar nav · Content · Stat card accents · Table · Badges
Role badges · Form focus · Login page · Sidebar user footer · Blazor error banner
Misc · Public shell · Home hero · Home browse · Package grid · Add to basket
Cart button + badge · Cart panel · Toast · Orders hero · Orders list
Order ticket (signature element) · Pickup pass · Confirm dialog · Manager order actions
Dashboard trend chart · Star rating · Business grid (home) · Business detail page
Packages inside the business modal · Reviews inside the business modal
Package detail modal · Order detail modal · Manager pickup scanner
Pickup validation page · Notification bell · Favorites · Dietary tag badges
```

### Design tokens

```css
:root {
    --em-forest:     #0b1f13;   /* darkest brand green — hero backgrounds, QR foreground */
    --em-forest-mid: #1a3a22;
    --em-leaf:       #22c55e;   /* primary accent */
    --em-leaf-mid:   #16a34a;   /* == --bs-primary, so Bootstrap's own utility classes match */
    --em-leaf-dark:  #15803d;
    --em-surface:    #f4f9f5;   /* page background */
    --em-text:       #111827;
    --em-muted:      #6b7280;

    --em-rescue:      #c9971f;  /* amber accent lifted from the logo mark — used sparingly */
    --em-rescue-soft: rgba(201, 151, 31, 0.22);
}
```
`--bs-primary`/`--bs-link-color` are overridden to the same `#16a34a` as `--em-leaf-mid`, so Bootstrap's own `.btn-primary`/link-color utilities blend in with the custom design language instead of clashing with it — the app doesn't fight Bootstrap, it retunes it.

### The "order ticket" — the signature visual element

`Order` rendering (`Orders.razor`, `OrderManagement.razor`, `OrderPickupPass.razor`, `OrderDetailModal`, `OrderValidate.razor`) all share one CSS shape: a perforated-ticket card with a dashed "seam" divider and a torn-stub side panel showing the order number — deliberately evoking a physical pickup receipt rather than a generic table row, reinforcing the app's "go pick this up in person" mental model. `OrderPickupPass.razor` reuses the identical `.order-ticket`/`.order-ticket-stub` classes at a larger scale (`--pass` modifier classes) rather than inventing a new component, so the QR-code pass reads as "the same ticket, just the one you're holding" instead of a different UI.

### Modal family

`PackageDetailModal`/`OrderDetailModal`/`BusinessDetail`'s own inline modal-like sections all share a `biz-modal-*`/`pkg-modal-*` class vocabulary (hero image banner, eyebrow label, fact-grid rows with an icon + label + value) — one visual grammar reused across three different data shapes rather than three bespoke modal designs.

---

## 14. JS Interop Patterns

Three distinct patterns, escalating in complexity:

### 14.1 `window.EcoMeal` — the shared global namespace (`wwwroot/js/site.js`)

```js
window.EcoMeal = {
    positionDropdown(anchorEl, dropdownEl) { /* fixed-position math, flips upward if not enough room below */ },
    timeZone() { try { return Intl.DateTimeFormat().resolvedOptions().timeZone; } catch { return null; } },
    geo: { getPosition() { /* navigator.geolocation → {lat, lng} Promise; resolves null, never rejects */ } },
    map: {
        render(elementId, markers) { /* creates/replaces a Leaflet map in #elementId, one pin per marker */ },
        destroy(elementId) { /* removes a previously rendered map instance, if any */ }
    },
    cart: { save(key, json), load(key), clear(key) },   // thin localStorage wrapper, every call swallows exceptions
    managedBusiness: { save(key, businessId), load(key) }   // same wrapper shape, backs ManagedBusinessContext (§5)
};
```
Called via plain `IJSRuntime.InvokeAsync("EcoMeal.xyz", ...)` from C# (`AnchoredDropdown`, `ClientTimeZoneService`, `ManagedBusinessContext`, `Home.razor`'s "near me"/map view, `BusinessForm.razor`'s "use my location") or inline `onclick="EcoMeal.cart.clear(...)"` HTML attributes (both layouts' logout buttons). No JS module isolation here — it's genuinely global, loaded once via a plain `<script>` tag, because every consumer needs it available synchronously from inline HTML attributes as well as from C#.

`geo.getPosition` wraps `navigator.geolocation.getCurrentPosition` in a `Promise` that always **resolves** (`null` on denial, timeout, or an unsupported browser) rather than ever rejecting — every C# caller (§6, §11) can `await` it and branch on a plain `null` check instead of wrapping the call in `try/catch`, and the UI degrades to an inline error message on any failure path instead of an unhandled exception. `map.render` lazily creates a `L.map(...)` the first time it's called for a given element ID and removes any previous instance first (`_instances[elementId]`), so repeatedly toggling `Home.razor`'s map view on/off doesn't leak Leaflet instances or double-initialize the same `<div>`. Each marker's popup HTML is built with a small hand-rolled `escapeHtml` — business names come from the database, not user input, but nothing stops an admin from typing HTML-significant characters into one, so the popup still escapes them rather than trusting the value.

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

### Reorder needed (almost) no new backend surface

Worth calling out as a general lesson: because `CartService.AddAsync` already clamped quantity to live stock, and `Order.OrderPackages` was already eagerly loaded everywhere an `Order` is fetched, the "Order again" feature (§9) shipped almost entirely as frontend orchestration over existing primitives — no new `OrderService` method. Not every feature needs a symmetric backend addition; check what already exists before assuming a new endpoint is required.

That said, "eagerly loaded" turned out to have a gap: `OrderRepository.OrdersWithIncludes()` (`BACKEND_ARCHITECTURE.md` §4) included `OrderPackages.Package` but not `Package.Business`, so `CartService.AddInternal`'s `package.Business.Name` read could `NullReferenceException` on reorder — intermittently, since EF's change tracker sometimes backfilled it anyway from the same query's `Order.Business` include. The lesson generalizes: "this data is already loaded" is a claim about the top-level query, not proof that every nested navigation property a *different* consumer (`CartService`, written with `BusinessDetail.razor`'s fully-loaded `Package` in mind) expects is actually populated. Fixed with one added `.ThenInclude(p => p.Business)`, not a new query.

### CSV export had to bypass the in-process controller pattern

The one place in the whole app where the frontend can't just `@inject` a controller and call a method directly — `OrderManagement.razor`'s export link has to be a real `<a href>` to a real HTTP `GET`, because file downloads are a browser/HTTP-level concept with no in-process equivalent. See `BACKEND_ARCHITECTURE.md` §6 for the backend-side consequence (a controller that resolves identity from `HttpContext.User` instead of the usual `CurrentUserAccessor`).

### Geolocation always degrades, never blocks

`EcoMeal.geo.getPosition` resolving to `null` instead of rejecting (§14) is a deliberate contract, not an implementation shortcut: browser geolocation can fail for reasons entirely outside the app's control (permission denied, no hardware, a slow/absent GPS fix, a corporate policy blocking it outright), and none of those should be able to break the page it's called from. Both call sites (`Home.razor`'s "near me" toggle, `BusinessForm.razor`'s "use my location") treat a `null` result identically — show an inline error string, leave everything else exactly as it was before the click. Contrast with a typical SPA pattern of a rejected promise bubbling into an error boundary; here there's no boundary to catch it; the JS side absorbs the failure so the C# side never has to.

### "Available" quantity needed a live/local split, not just a local calculation

`CartService.AvailableQuantity` originally computed everything from data already in memory — `package.Quantity` minus the viewer's own cart contents — which is correct for "don't let this browser add more than it can check out" but wrong for "how much is actually left," since it has no way to see Pending reservations sitting in the database (this browser's just-placed order included, once it clears the cart on checkout, or any other customer's). Fixing the *display* meant adding a genuinely new read path — `OrderController.GetPendingReservedQuantitiesAsync`, bulk-fetched once per page load alongside the package list (§6, §7) — rather than something derivable from state the page already had. Contrast with reorder (above), which needed nothing new precisely because the local state already had everything required; the lesson generalizes in both directions — check what's genuinely knowable client-side before reaching for a new endpoint, but don't force a client-only fix onto a value that depends on server state no client-side signal can substitute for.
