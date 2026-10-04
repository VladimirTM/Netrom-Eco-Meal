# Frontend Architecture — Netrom Eco Meal

**Stack:** React 19 + TypeScript, Vite, React Router v7, Axios, `@microsoft/signalr`, Leaflet, `qrcode`/`jsqr` · a hand-written CSS design system (`index.css`, ported from the old `app.css`) · Bootstrap's CSS vendored as a static file (its JS bundle is never loaded) · `oxlint` for linting, `vitest`/`@testing-library/react`/`msw` for tests
**Location:** `Frontend/eco-meal-frontend/` — a standalone SPA, served by its own nginx container, talking to `Backend/NetromEcoMeal.Api` over plain HTTP/JSON and one SignalR hub. See `BACKEND_ARCHITECTURE.md` for the Api side.

The app was migrated from a Blazor Server UI to this React SPA. This document describes the frontend as it exists today, as if it had always been built this way — no migration framing below.

---

## Table of Contents

1. [Project Structure](#1-project-structure)
2. [Bootstrap & Entry Point](#2-bootstrap--entry-point)
3. [Routing & Route Guards](#3-routing--route-guards)
4. [Layouts](#4-layouts)
5. [Cross-Cutting Contexts](#5-cross-cutting-contexts)
6. [The API Layer](#6-the-api-layer)
7. [Public & Customer Pages](#7-public--customer-pages)
8. [Business-Manager & Admin Pages](#8-business-manager--admin-pages)
9. [Shared Components](#9-shared-components)
10. [Custom Hooks](#10-custom-hooks)
11. [Live Stock Over SignalR](#11-live-stock-over-signalr)
12. [CSS Design System](#12-css-design-system)
13. [Build, Test, Lint & Docker](#13-build-test-lint--docker)

---

## 1. Project Structure

```
Frontend/eco-meal-frontend/
├── index.html                    # Vite entry HTML — no-flash theme script, fonts, vendored Bootstrap CSS
├── vite.config.ts                 # React plugin + vitest config (jsdom, src/test/setup.ts)
├── nginx.conf                     # SPA fallback for the production container
├── Dockerfile                     # two-stage build (see §13)
├── .env.example                   # VITE_API_URL=http://localhost:5080/api
├── public/
│   ├── logo.png, favicon.png, manifest.webmanifest, service-worker.js
│   └── vendor/bootstrap/          # vendored bootstrap.min.css only — no bootstrap.bundle.js anywhere
└── src/
    ├── main.tsx                   # createRoot + StrictMode + BrowserRouter, registerServiceWorker()
    ├── App.tsx                    # provider stack + <Routes>
    ├── index.css                  # the whole design system — one file, no preprocessor (§12)
    │
    ├── api/
    │   ├── base/http.ts           # axios instance, bearer interceptor, ApiError, global 401 handler
    │   ├── clients/                # one *ApiClient.ts per backend controller (26 of them)
    │   └── models/                 # TS types mirroring backend DTOs
    │
    ├── context/                    # cross-cutting state — one folder per concern (§5)
    │   ├── AuthContext/
    │   ├── CartContext/
    │   ├── ManagedBusinessContext/
    │   ├── NotificationContext/
    │   ├── ThemeContext/
    │   ├── TimeZoneContext/
    │   └── ToastContext/
    │
    ├── routes/
    │   ├── RequireRole.tsx         # route guard (+ RequireRole.test.tsx)
    │   └── ScrollAndFocusManager.tsx
    │
    ├── layouts/
    │   ├── PublicLayout.tsx        # customer-facing header/footer shell
    │   ├── DashboardLayout.tsx     # Admin/BusinessManager sidebar shell
    │   ├── EmptyLayout.tsx         # chrome-free — auth pages, Stripe redirect landings
    │   └── NavMenu.tsx             # the sidebar itself, used by DashboardLayout
    │
    ├── components/                 # one folder per page, plus common/ for shared pieces (§7-§9)
    │   ├── Home/, BusinessDetail/, Brands/, BrandDetail/, Impact/, BasketPlanner/,
    │   │   Orders/, OrderPickupPass/, TripPlanner/, RescueCircleList/, RescueCircleInvite/,
    │   │   RescueCircleReturn/, StandingOrders/, Referrals/, BusinessApply/,
    │   │   PaymentReturn/, PaymentCancel/, Account/{Login,Register,ConfirmEmail,
    │   │   ResendConfirmation,ForgotPassword,ResetPassword,AccountSettings,AccessDenied}/, NotFound/
    │   ├── Dashboard/, Businesses/, BusinessForm/, Packages/, PackageForm/, PackageTemplates/,
    │   │   OrderManagement/, OrderScan/, OrderValidate/, OrderValidateLegacy/, Payments/,
    │   │   Users/, Reports/, AuditLog/, Types/
    │   └── common/                 # AnchoredDropdown, CartPanel, ConfirmDialog, EmptyState,
    │                                # ForbiddenPanel, ImpactEquivalencyStats, LoadingSpinner,
    │                                # NotificationBell, NotificationPanel, NotFoundPanel,
    │                                # OrderDetailModal, PackageDetailModal, Pagination,
    │                                # ReportDialog, Skeleton, StarRating, ThemeToggle
    │
    ├── hooks/                       # one *.ts per concern — replaces the old JS-interop layer (§10)
    ├── utils/                       # pure formatting/calculation helpers, several with their own *.test.ts
    └── test/setup.ts                # imports @testing-library/jest-dom/vitest
```

Every page component lives at `components/<PageName>/index.tsx` — a flat, one-level convention; nothing nests a page inside another page's folder. `common/` is the only folder that doesn't correspond 1:1 to a route.

---

## 2. Bootstrap & Entry Point

**File:** `index.html`

```html
<script>
  // Sets data-theme before first paint to avoid a flash — kept in sync after load by ThemeContext.
  (function () {
    try {
      var stored = localStorage.getItem("em-theme");
      var theme = stored === "dark" || stored === "light" ? stored
        : (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
      document.documentElement.setAttribute("data-theme", theme);
      document.documentElement.setAttribute("data-bs-theme", theme);
    } catch { }
  })();
</script>
<link rel="stylesheet" href="/vendor/bootstrap/bootstrap.min.css" />
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" />
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" integrity="sha256-..." crossorigin="" />
...
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js" integrity="sha256-..." crossorigin=""></script>
<script type="module" src="/src/main.tsx"></script>
```

The inline script is what keeps dark mode from flashing light-then-dark on every hard reload — `ThemeProvider` (§5) picks up the same `localStorage` value on mount so React's state matches the DOM from its very first render, rather than re-deciding the theme itself.

Bootstrap's **CSS** is vendored under `public/vendor/bootstrap/` (a static file with no `package.json` dependency) so the app keeps Bootstrap's utility classes (`d-flex`, `gap-2`, `.card`, `.table`, …) without pulling in a build step for it. Bootstrap's **JS bundle is never loaded anywhere** — there is no bootstrap.js, no `data-bs-toggle` wiring, nothing. Every place the old Blazor UI would have used a Bootstrap dropdown/accordion/offcanvas, this app uses plain React state instead (see `AnchoredDropdown`, `ConfirmDialog`, the Dashboard's expandable panels, etc. in §9).

Leaflet is loaded two ways at once, which is worth knowing rather than treating as a bug to "fix" blindly: `index.html` still carries a CDN `<link>`/`<script>` pair for it (a leftover from matching the old app's loading style), **and** `leaflet` is also a real `npm` dependency that `useLeafletMap` (§10) imports directly, including its own CSS and marker icon assets. The hook's own import is what the map pages actually run against; the CDN tags are redundant rather than load-bearing.

**File:** `src/main.tsx`

```tsx
registerServiceWorker();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
);
```

`registerServiceWorker()` (from `hooks/usePushSubscription.ts`) is called once, outside React entirely, on every app load — registering the service worker needs no permission and backs the web-push "enable browser alerts" toggle (§9 `NotificationPanel`) whenever it's later used.

**File:** `src/App.tsx`

```tsx
<AuthProvider>
  <ThemeProvider>
    <TimeZoneProvider>
      <ToastProvider>
        <CartProvider>
          <NotificationProvider>
            <ManagedBusinessProvider>
              <ScrollAndFocusManager />
              <AppRoutes />
            </ManagedBusinessProvider>
          </NotificationProvider>
        </CartProvider>
      </ToastProvider>
    </TimeZoneProvider>
  </ThemeProvider>
</AuthProvider>
```

That nesting order is deliberate: `CartProvider`, `NotificationProvider`, and `ManagedBusinessProvider` all read `useAuth()` internally to key their own per-user state (localStorage keys namespaced by user id, resetting on logout), so they have to sit inside `AuthProvider`. `ScrollAndFocusManager` renders `null` — it exists purely for its `useLocation()`-driven side effect (§3) — and is mounted once, above the routes, rather than per-page.

---

## 3. Routing & Route Guards

**File:** `src/App.tsx`'s `AppRoutes()`, using React Router v7's `<Routes>`/`<Route>` with layout routes (a parent `<Route element={<SomeLayout/>}>` wrapping child routes that render into its `<Outlet/>`).

Three layout groups:

```tsx
<Route element={<EmptyLayout />}>
  <Route path="/account/login" element={<Login />} />
  <Route path="/account/register" element={<Register />} />
  ...
  {/* Stripe redirect targets — CheckoutService.cs/RescueCircleService.cs build these exact
      paths into their success_url/cancel_url, so they must not change. */}
  <Route path="/checkout/return" element={<RequireRole roles={["Customer"]}><PaymentReturn /></RequireRole>} />
  <Route path="/checkout/cancel" element={<RequireRole roles={["Customer"]}><PaymentCancel /></RequireRole>} />
  <Route path="/circles/return" element={<RequireRole roles={["Customer"]}><RescueCircleReturn /></RequireRole>} />
</Route>

<Route element={<PublicLayout />}>
  <Route path="/" element={<Home />} />
  <Route path="/businesses/:id" element={<BusinessDetail />} />
  <Route path="/brands" element={<Brands />} />
  <Route path="/brands/:id" element={<BrandDetail />} />
  <Route path="/impact" element={<Impact />} />
  <Route path="/plan-basket" element={<RequireRole roles={["Customer"]}><BasketPlanner /></RequireRole>} />
  <Route path="/account/settings" element={<RequireRole><AccountSettings /></RequireRole>} />
  <Route path="/orders" element={<RequireRole roles={["Customer"]}><Orders /></RequireRole>} />
  <Route path="/orders/pickup/:id" element={<RequireRole roles={["Customer"]}><OrderPickupPass /></RequireRole>} />
  <Route path="/trip-planner" element={<RequireRole roles={["Customer"]}><TripPlanner /></RequireRole>} />
  <Route path="/circles" element={<RequireRole roles={["Customer"]}><RescueCircleList /></RequireRole>} />
  <Route path="/circles/:circleId" element={<RequireRole roles={["Customer"]}><RescueCircleInvite /></RequireRole>} />
  <Route path="/standing-orders" element={<RequireRole roles={["Customer"]}><StandingOrders /></RequireRole>} />
  <Route path="/referrals" element={<RequireRole roles={["Customer"]}><Referrals /></RequireRole>} />
  <Route path="/businesses/apply" element={<RequireRole roles={["Customer","BusinessManager"]}><BusinessApply /></RequireRole>} />
  <Route path="/not-found" element={<NotFound />} />
  <Route path="*" element={<NotFound />} />
</Route>

<Route element={<RequireRole roles={["Admin","BusinessManager"]}><DashboardLayout /></RequireRole>}>
  <Route path="/dashboard" element={<Dashboard />} />
  <Route path="/businesses" element={<Businesses />} />
  <Route path="/businesses/create" element={<BusinessForm />} />
  <Route path="/businesses/edit/:id" element={<BusinessForm />} />
  <Route path="/packages" element={<Packages />} />
  <Route path="/packages/create" element={<PackageForm />} />
  <Route path="/packages/edit/:id" element={<PackageForm />} />
  <Route path="/packages/templates" element={<PackageTemplates />} />
  <Route path="/orders/manage" element={<OrderManagement />} />
  <Route path="/orders/scan" element={<OrderScan />} />
  <Route path="/orders/validate/:id/:passId" element={<OrderValidate />} />
  <Route path="/orders/validate/:id" element={<OrderValidateLegacy />} />
  <Route path="/payments" element={<Payments />} />
  {/* Admin-only — nested RequireRole is strictly narrower than the shell's own Admin-or-
      BusinessManager gate, so a manager hitting one of these URLs directly sees
      ForbiddenPanel inline, still inside the sidebar shell. */}
  <Route path="/users" element={<RequireRole roles={["Admin"]}><Users /></RequireRole>} />
  <Route path="/reports" element={<RequireRole roles={["Admin"]}><Reports /></RequireRole>} />
  <Route path="/audit-log" element={<RequireRole roles={["Admin"]}><AuditLog /></RequireRole>} />
  <Route path="/types" element={<RequireRole roles={["Admin"]}><Types /></RequireRole>} />
</Route>
```

### `RequireRole` — the route guard

**File:** `src/routes/RequireRole.tsx` (tested by `RequireRole.test.tsx`)

```tsx
function RequireRole({ roles, children }: RequireRoleProps) {
  const { isAuthenticated, loading, user } = useAuth();
  const location = useLocation();

  if (loading) return <LoadingSpinner />;

  if (!isAuthenticated) {
    const returnUrl = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/account/login?returnUrl=${returnUrl}`} replace />;
  }

  if (roles && roles.length > 0 && !roles.includes(user!.role)) {
    return <ForbiddenPanel message="Your account doesn't have permission to view this page." backHref="/" backLabel="Back to home" />;
  }

  return <>{children}</>;
}
```

This single component covers both branches the old Blazor `NotAuthorized` template split into two: **not signed in at all** gets redirected to `/account/login?returnUrl=...` (picked up by `Login`'s own `?returnUrl=` handling, §7); **signed in with the wrong role** renders `ForbiddenPanel` *inline*, in whatever position the route occupies — when it wraps a `DashboardLayout` route it still renders inside that layout's own chrome (sidebar and all), and when it wraps a bare page element (`/plan-basket`, `/orders`, …) it renders with no layout at all around it. Calling it with no `roles` prop at all (`/account/settings`) means "any authenticated role" — there's no public/anonymous case to handle here since an unauthenticated visitor already got redirected above.

### `ScrollAndFocusManager`

**File:** `src/routes/ScrollAndFocusManager.tsx`

```tsx
useEffect(() => {
  window.scrollTo(0, 0);
  const active = document.activeElement;
  const isIdle = !active || active === document.body || active === document.documentElement;
  if (isIdle) {
    const heading = document.querySelector<HTMLElement>("h1");
    if (heading) {
      if (!heading.hasAttribute("tabindex")) heading.setAttribute("tabindex", "-1");
      heading.focus();
    }
  }
}, [location.pathname, location.search]);
```

React Router's client-side navigation never triggers the browser's own "new page starts at the top" behavior, so both the scroll reset and the focus-to-heading move (for screen readers) have to happen explicitly here. The "only focus if nothing else already has focus" guard exists because a naive version of this once stole focus from a field the user was mid-typing in on navigation — a real regression this code is written specifically to avoid repeating.

---

## 4. Layouts

| Layout | Wraps | Shell |
|---|---|---|
| `PublicLayout` | Home, BusinessDetail, Brands(+Detail), Impact, BasketPlanner, AccountSettings, AccessDenied, Orders, OrderPickupPass, TripPlanner, RescueCircle\*, StandingOrders, Referrals, BusinessApply, NotFound | Sticky header (logo, trophy/impact link, brands link, `NotificationBell`, and — for a signed-in Customer — basket planner/orders/trip-planner/circles/standing-orders/referrals icons plus the basket button+badge; a staff icon to `/dashboard`; "list your business" for Customer/BusinessManager; account-settings gear; `ThemeToggle`; sign-out), `<Outlet/>`, footer. Renders `CartPanel` (customers only) and `NotificationPanel` (any signed-in user) as **siblings of `.public-shell`**, not nested inside the header |
| `DashboardLayout` | Dashboard, Businesses(+Form), Packages(+Form), PackageTemplates, OrderManagement, OrderScan, OrderValidate(Legacy), Payments, Users, Reports, AuditLog, Types | Fixed sidebar (`NavMenu`) + `<main><article class="content">` holding `<Outlet/>` — the classic admin-panel shell. Renders `NotificationPanel` outside `.page` |
| `EmptyLayout` | Login, Register, ConfirmEmail, ResendConfirmation, ForgotPassword, ResetPassword, PaymentReturn, PaymentCancel, RescueCircleReturn | Just `<Outlet/>` — no header, no sidebar, no footer. The login/register cards and the Stripe-redirect landing pages all center themselves via `index.css`'s `.login-page`/`.login-card`/`.cart-confirmation` classes |

**Why `CartPanel`/`NotificationPanel` render outside the header's own DOM subtree:** both `.public-header` and `.sidebar` are `position: sticky`, and `position: sticky` always creates a new CSS stacking context (unlike `position: relative`, which only does with a non-`auto` z-index) regardless of `z-index`. A `position: fixed` popup nested inside one of those elements computes its on-screen coordinates against the viewport correctly, but its paint order gets trapped inside that stacking context — the whole header/sidebar subtree (popup included) paints as one atomic unit at its slot in the DOM, before `<main>`, which then visually covers the popup even though every computed style looks correct in devtools. This is exactly why `NotificationBell` (the trigger, kept inside the header/sidebar) and `NotificationPanel` (the actual popup) are two separate components sharing state through `NotificationContext` (§5) rather than one component — and why both layouts render the panel as a direct sibling of their own shell `<div>`.

### `NavMenu` — the sidebar

**File:** `src/layouts/NavMenu.tsx`

Admin-only nav links (User Roles, Reports, Audit Log, Types) are gated behind `user?.role === "Admin"`; every other link (Dashboard, Businesses, Packages, Orders, Payments, Account Settings) is visible to both Admin and BusinessManager — the actual data scoping happens page-side via `ManagedBusinessContext`, not by hiding nav links per role. The brand subtitle under the logo reads "Admin Panel" for an Admin and "Manager Panel" for a BusinessManager, so the label always matches the viewer's real role.

**Business switcher:** when `myBusinesses.length > 1` (a BusinessManager staffed at more than one business), an `AnchoredDropdown` (§9) lists every staffed business with a check mark on the currently-selected one; exactly one business renders a plain label instead of a dropdown; zero renders nothing. Picking a different business calls `ManagedBusinessContext.select`, which persists the choice to `localStorage` and re-renders every business-scoped page (Dashboard, Packages, PackageForm, PackageTemplates, OrderManagement, Payments) without a navigation.

---

## 5. Cross-Cutting Contexts

Seven React contexts, each in its own `context/<Name>Context/` folder split into a `<Name>Provider.tsx` (the implementation) and a `<name>-context.ts` (the `createContext`/`use<Name>()` hook pair, importable without pulling in the provider's own dependency tree). These are what the old Blazor app put in circuit-scoped C# services with an `OnChange` event — here they're just ordinary React context, re-rendering subscribers on state change for free.

### AuthContext

**Files:** `AuthContext/AuthProvider.tsx`, `auth-context.ts`

Holds the bearer token (`localStorage["em_auth_token"]`, the same `TOKEN_KEY` constant `api/base/http.ts` reads/writes) and the decoded `UserDto` (`{id, name, email, role}`, `role: "Admin" | "Customer" | "BusinessManager"`). `login(token, user)` and `logout()` write/clear the token and update state; mounting with a token already in storage triggers a `GET /auth/me` to refresh the user and set `loading` accordingly. A `useRef` to the latest `logout` is registered once, on mount, as the app's **global 401 handler** via `setUnauthorizedHandler` (exported from `api/base/http.ts`) — any response interceptor 401 while a token is present calls it, which logs out and redirects to `/account/login`, regardless of which component triggered the failing request.

### CartContext

**Files:** `CartContext/CartProvider.tsx` (+ `CartProvider.test.tsx`), `cart-context.ts`

The basket. Persisted to `localStorage["ecomeal.cart.{userId}"]`, reloaded (or cleared, for a logged-out viewer) the instant the signed-in user id changes — computed at render time via React's "adjusting state when a prop changes" pattern rather than a `useEffect` round trip, so there's no frame where a new user could see the previous user's basket. Enforces the **single-business-per-basket rule**: `addItem` silently drops every existing line the moment a package from a different business is added, and `wouldReplaceCart(businessId)` lets a caller (`BusinessDetail`, `Orders`' reorder, `BasketPlanner`) check that ahead of time and show a `ConfirmDialog` ("Start a new basket?") instead of silently wiping the basket. `inBasketQuantity(packageId)` backs every "N left" calculation that needs to subtract what's already sitting in the viewer's own basket (`utils/packageAvailability.ts`'s `availableQuantity`). Also holds the free-text `logisticsNote` for the pickup instructions field and the `isOpen`/`open`/`close` state `CartPanel` renders from.

### ManagedBusinessContext

**Files:** `ManagedBusinessContext/ManagedBusinessProvider.tsx`, `managed-business-context.ts` — exposed via `useManagedBusiness()`

Tracks "which of my businesses am I managing right now" for a BusinessManager staffed at more than one business. Only ever fetches (`businessesApi.getPaged({staffUserId, pageSize: 100})`) for role `BusinessManager`; an Admin has no "my business" concept at all — every business-scoped page treats a `null` `selectedBusinessId` as "no filter" for an Admin, the same way the data itself does. The selection persists to `localStorage["ecomeal.managedBusiness.{userId}"]` via `select(businessId)`.

Its `loading` flag is **derived**, not a separate `useState(false)` flipped imperatively — `loading = authLoading || (isManager && userId !== loadedForUserId)`. This closes a real race: on a hard reload, this provider's own fetch effect hasn't run yet, but a descendant page's effect (e.g. `PackageForm`'s "do I manage a business?" check) can fire first, since React commits effects bottom-up. A plain boolean that starts `false` would read "not loading" before the fetch even began, and a consumer could wrongly conclude "zero businesses" and get stuck there. Deriving it from "do we know the user yet" and "have we fetched for this exact user yet" can't go stale that way.

### ThemeContext

**Files:** `ThemeContext/ThemeProvider.tsx`, `theme-context.ts`

`theme: "light" | "dark"` plus `toggleTheme()`. Reads/writes `localStorage["em-theme"]`, falling back to `prefers-color-scheme` when nothing is stored, and sets **both** `data-theme` and `data-bs-theme` on `<html>` — the former drives `index.css`'s own tokens (§12), the latter is Bootstrap 5's own dark-mode attribute, kept in sync so vendored Bootstrap components match. `index.html`'s inline script (§2) sets the same attributes before first paint so there's no flash; this provider's initial state just reads the same value back so React agrees with the DOM from the start.

### NotificationContext

**Files:** `NotificationContext/NotificationProvider.tsx`, `notification-context.ts`

Splits the same way the old `NotificationPanelState` did, for the stacking-context reason in §4: `NotificationBell` (trigger) and `NotificationPanel` (popup) are different components sharing one source of truth. Polls `GET /notifications/unread-count` every 30 seconds **only while `isAuthenticated`** — an anonymous visitor has no bell to open, so there's no reason to run the timer for them; the effect resets to signed-out defaults (`unreadCount: 0`, panel closed, list cleared) the instant `isAuthenticated` flips, again via the "adjust state when a prop changes" pattern rather than a `useEffect`. Opening the panel (`toggle()`) lazily fetches the last 20 notifications; `markAllRead`/`markAsRead` optimistically patch local state before/alongside the API call.

### TimeZoneContext

**Files:** `TimeZoneContext/TimeZoneProvider.tsx`, `timezone-context.ts`

The simplest of the seven — the context value **is** a plain string (the IANA zone id), not an object. Detected once via `Intl.DateTimeFormat().resolvedOptions().timeZone` (falling back to `"UTC"` if that throws) and never changes for the life of the tab. This is what makes every pickup-window display in the app ("Pickup 20:00–22:00") mean the *viewer's* local time regardless of where the browser or the Api server actually sit — see `utils/dates.ts`/`utils/packagePickup.ts`, which all take a `timeZone` parameter fed from `useTimeZone()`.

### ToastContext

**Files:** `ToastContext/ToastProvider.tsx`, `toast-context.ts`

`showToast(message, variant?)` pushes a Bootstrap `.alert` into a fixed bottom-right stack, auto-dismissed after 5 seconds (and dismissible early via its own close button). Used for the small inline confirmations after a basket action (`PackageDetailModal`'s "Added X to your basket," `BusinessDetail`'s auto-reserve-style toasts) rather than for page-level errors, which stay as inline `alert-danger` blocks next to whatever action produced them.

---

## 6. The API Layer

**File:** `src/api/base/http.ts`

A single `axios` instance (`baseURL: import.meta.env.VITE_API_URL`) with two interceptors:

- **Request:** attaches `Authorization: Bearer <token>` from `localStorage["em_auth_token"]` whenever one exists.
- **Response:** on any error, normalizes the backend's various error shapes (a bare string body, `{error}`, `{detail}`, `{title}`, or ASP.NET's `{errors: {field: [msg]}}` model-state shape) into one `ApiError extends Error` carrying `message`, an optional `code` (e.g. `"email_not_confirmed"`, used by `Login` to show a "resend confirmation" link without string-matching the message), and the HTTP `status`. A `401` while a token is present calls the global handler `AuthProvider` registered (§5) — this is the *only* place a 401 is handled generically; individual pages only ever branch on `403`/`404`/`409` for their own specific "forbidden"/"not found"/"conflict" states.

`http.{get,post,put,remove}` are thin typed wrappers returning `response.data` directly. Two specialized helpers exist for shapes the generic ones can't cover: `postForm` (clears the instance's default `Content-Type: application/json` so the browser computes its own `multipart/form-data; boundary=...` header for a `FormData` body — used by `UploadsApiClient` for package/business photo uploads) and `getBlob` (`responseType: "blob"`, used by `ExportsApiClient` for CSV downloads — a bearer token can't ride along on a plain `<a href>` navigation the way a cookie could, so the caller fetches the blob via Axios and turns it into an object URL itself, see `downloadBlob` in `ExportsApiClient.ts`).

**`src/api/clients/`** — one typed client per backend controller, each a plain object of functions calling `http.*` with a fixed path prefix: `AiApiClient`, `AuditLogApiClient`, `AuthApiClient`, `BrandsApiClient`, `BusinessTypesApiClient`, `BusinessesApiClient`, `ExportsApiClient`, `FavoritesApiClient`, `ImpactApiClient`, `KitchenTipsApiClient`, `LoyaltyApiClient`, `NotificationsApiClient`, `OrdersApiClient`, `PackageTemplatesApiClient`, `PackageTypesApiClient`, `PackagesApiClient`, `PaymentsApiClient`, `PushSubscriptionsApiClient`, `ReferralsApiClient`, `ReportsApiClient`, `RescueCirclesApiClient`, `ReviewsApiClient`, `StandingOrdersApiClient`, `StreaksApiClient`, `UploadsApiClient`, `UsersApiClient`.

**`src/api/models/`** — the TypeScript interfaces these clients return, named to mirror the backend's own DTOs (`Business.ts`, `Order.ts`, `Package.ts`, `Pagination.ts`'s generic `PaginatedList<T>`, etc.). Every timestamp field is a UTC ISO-8601 string; nothing in this layer does timezone conversion — that's `utils/dates.ts`'s job, applied at render time with whatever zone `useTimeZone()` returns.

---

## 7. Public & Customer Pages

### Home — the storefront (`/`)

**File:** `components/Home/index.tsx`

Two independent data loads, mirroring the split between a page's hero stats and its paginated grid: an unfiltered fetch of every live package (for the hero's "packages live / portions to save / kitchens on board / kg saved" stats, plus each card's "N live"/"from X" figures) and a separate, server-paged, filtered/sorted business query that reloads on every filter change. The **search box** is debounced 300 ms (`useDebouncedValue`); every other filter (kitchen type, sort, diet/allergen, favorites-only, max price from an AI search) reloads immediately and resets to page 1. Ratings are batch-loaded per visible page (`reviewsApi.getByBusinesses(businessIds)`) rather than one query per card.

Filters (kitchen type, sort, diet/allergen, Near me, Favorites, and a dismissible "Under X lei" chip from the AI search) live inside an `.em-popover` gated by one `filtersOpen` boolean — the always-visible toolbar keeps only the search box, the Filters trigger (badged with `activeFilterCount`), and a Map-view toggle. "Near me" requests browser geolocation via `useGeolocation()` (§10) before switching the sort to distance; each card also shows its own "X km/m away" badge once a position is known, independent of which sort is active. **Map view** swaps the card grid for a `useLeafletMap()`-rendered map plotting *every* business with a saved location — deliberately ignoring the active filters, since a map that silently drops pins would be more confusing than one that's always complete.

**AI search bar:** a second input above the literal search box, submitted via `aiApi.parseSearchIntent(query, lastIntent)`. A successful parse just writes into the same filter state the manual controls already bind to (`search`, `dietaryTagFilter`, `sortBy`, `maxPriceFilter`) — the existing dropdowns visibly reflect what the AI understood, and the regular "Clear all" button already covers undoing it. `lastIntent` is fed back on the next call as refinement context ("cheaper," "gluten-free only" adjusts the prior turn). A `409` response from the Api means "the AI assistant isn't configured" and surfaces as a plain inline error, not a crash.

### BusinessDetail — one kitchen's page (`/businesses/:id`)

**File:** `components/BusinessDetail/index.tsx`

A business that isn't `{status: "Approved", isHidden: false}` renders the identical `NotFoundPanel` a genuinely deleted one would — there's no distinct "not available yet" state for a pending/rejected/hidden business. Packages are filtered client-side to live, non-hidden ones and sorted by soonest `pickupEnd`; "X left" per package is `stockQuantity − reservedElsewhere − inBasketQuantity` (`utils/packageAvailability.ts`), where `reservedElsewhere` comes from one bulk `ordersApi.getPendingReservedQuantities` call so a package's displayed count accounts for *other* customers' Pending orders, not just this browser's own basket.

**Live stock:** subscribes via `useStockHub(businessId, onChanged)` (§11) and re-runs the same package load on every push for this business — a package selling out from under the viewer, or a manager editing/hiding/restocking one, updates the row (and the open `PackageDetailModal`, which closes itself if its package drops off the live list) with no polling and no manual refresh.

Below the package list: an opening-hours panel (only rendered when hours or closures exist, using `utils/businessHoursStatus.ts` for "open now"/active-closure logic and highlighting today's row); a loyalty-progress banner for a signed-in Customer when the business has a punch card configured; a **standing order** section (create one inline, or see/pause/resume/remove the existing one for this business — list management for *all* of a customer's standing orders lives on its own page, §below); a review form (gated on `reviewContext.canReview`, itself driven by the backend's own order-history check) plus the existing review list; and a **kitchen tips** section (any signed-in customer can post one, no order history required) where each tip carries its own report-flag button reusing the same `ReportDialog` instance the business-level report button uses, switched by a `reportTipId` field.

Adding a package to the basket when the cart already holds a different business's items shows the same `ConfirmDialog` ("Start a new basket?") that `Orders`' reorder and `BasketPlanner`'s "add approved to basket" also use — all three share `CartContext.wouldReplaceCart`.

### Brands / BrandDetail — chain grouping (`/brands`, `/brands/:id`)

**Files:** `components/Brands/index.tsx`, `components/BrandDetail/index.tsx`

`Brands` is a card grid over every brand with at least one *public* location (derived client-side from `brandsApi.getAll()` + `businessesApi.getAll(true)`, since the list DTO carries no location count of its own). `BrandDetail` sorts its locations by `(open-now, distance, name)` — distance only computed once `useGeolocation()` resolves a position — and surfaces the first open-and-nearest one as a green "Nearest location open now" banner linking straight to that business's page; this is the one payoff that justifies asking for geolocation here at all. The favorite button reuses `BusinessDetail`'s exact heart-icon pattern against the brand-scoped favorite endpoints instead.

### Impact — the community leaderboard (`/impact`)

**File:** `components/Impact/index.tsx`

Public, no role gate — `impactApi.getMonthlyLeaderboard()` is fetched for every visitor; the opt-in toggle itself (and the viewer's own opt-in status fetch) only renders for a signed-in Customer. Toggling it re-fetches the whole board rather than just flipping a local flag, since opting in/out can make the viewer's own row appear or disappear, not just relabel one that's already showing. Each row highlighting the signed-in viewer's own entry, a gold/silver/bronze accent on the top three ranks, and a small flame badge for `streakWeeks > 0` are all pure presentation over data the endpoint already returns pre-sorted. The hero repeats the same km-not-driven/liters-of-water equivalency chips `ImpactEquivalencyStats` renders elsewhere (§9), computed inline here instead since this is a platform-wide total rather than one order's.

### BasketPlanner — the AI rescue-basket planner (`/plan-basket`, Customer)

**File:** `components/BasketPlanner/index.tsx`

Three inputs (headcount, RON budget, optional dietary/allergen tag) submit to `aiApi.proposeBasket`, which returns a fully server-validated `BasketPlanDto` — every package in it is real and already shares one `businessId`, so the page never re-checks anything before rendering. Every proposed item starts pre-approved in a `Set<packageId>`; unchecking one is a pure client-side toggle recomputing the approved total. Because the plan is guaranteed single-business, "Add approved to basket" only ever needs to check `wouldReplaceCart` once, against the *first* approved item, before reusing the same `ConfirmDialog` every other add-to-basket flow uses. A `409` surfaces as "the AI assistant isn't configured"; any other failure (including a slow local model timing out) falls back to a generic "taking too long, try again" message so the button never gets stuck disabled with no explanation.

### Orders — history, reorder, cancel (`/orders`, Customer)

**File:** `components/Orders/index.tsx`

A lifetime-stats hero (orders placed / portions rescued / kitchens visited / kg saved, computed from one **unfiltered** full order fetch, plus a separate `streaksApi.getMyStreakWeeks()` call for the flame-badged streak figure) sits above a status-filterable, server-paged ticket list — two independent queries, because the hero's totals must reflect the customer's whole history regardless of which status chip is active below it.

**Reorder** ("Order again," shown only on Completed/Cancelled tickets) is a pure frontend feature with no dedicated backend endpoint: it walks the order's lines, skips any whose pickup window has already closed, and calls `CartContext.addItem` for the rest — `addItem` already clamps quantity to whatever's currently in stock, so re-adding a stale order's quantities is safe by construction. Because `OrderDto` (unlike the old in-process `Package` entity) carries no live stock count, a sold-out line isn't caught here the way `BusinessDetail`'s own add-to-basket is — the worst case is a line added at its original quantity that the basket then has to reconcile once opened. `addedCount` counts distinct lines, not units, by design.

Each ticket shows `payment.amount` (what Stripe actually charged, post loyalty/referral-credit discount) as its total, falling back to the raw line subtotal only when there's no `Payment` yet — with a small "(X before discount)" note whenever the two differ. `OrderDetailModal` (§9) is the drill-down on click; Cancel (Pending/Confirmed) and the QR-code link (Confirmed only, to `OrderPickupPass`) are the ticket's other actions.

### OrderPickupPass — the QR code(s) (`/orders/pickup/:id`, Customer)

**File:** `components/OrderPickupPass/index.tsx`

QR generation happens **client-side** via the `qrcode` package (`QRCode.toString(url, {type: "svg", ...})`), not server-rendered — the payload is still exactly `{origin}/orders/validate/{orderId}/{passId}`, unchanged from when it was generated server-side. Only renders for a `Confirmed` order; every other status shows an explanation instead of a stale/broken code. More than one pass (from a Rescue Circle split, or a manual "Splitting with a group?" request) renders a row of pass tabs — clicking one just regenerates the SVG locally, no round trip. "Update passes" (1–6) calls `ordersApi.splitPickupPasses` and reloads.

### TripPlanner — multi-stop pickup routing (`/trip-planner`, Customer)

**File:** `components/TripPlanner/index.tsx`

Groups the customer's own Pending/Confirmed orders into one stop per business (an active order can't span businesses), then orders those stops with a nearest-neighbor walk (`utils/tripRoute.ts`'s `planRoute`, a direct port of the backend's own `TripPlanner.PlanRoute` — plain haversine geometry, not a routing API or a true TSP solve). Geolocation (`useGeolocation`, with its own hard 10-second ceiling, §10) is only requested once there's more than one stop to route between; a denial just falls back to starting from the first stop rather than blocking the page. Renders as a numbered list next to a `useLeafletMap()` map, each marker's label prefixed with its stop number.

### Rescue Circles — splitting a basket with friends (`/circles`, `/circles/:circleId`, `/circles/return`)

**Files:** `components/RescueCircleList/index.tsx`, `components/RescueCircleInvite/index.tsx`, `components/RescueCircleReturn/index.tsx`, plus the starter panel inside `common/CartPanel` (§9)

`CartPanel`'s "Split this with friends instead" link swaps the Pay button for a people-count stepper (2–6) showing a live "≈ X each" estimate and a "Start & pay my share" button (`rescueCirclesApi.startCircle`) — unlike a solo checkout, this clears the basket **immediately** rather than waiting for a return-page confirmation, because the underlying order is placed the moment the circle starts, not once everyone's paid.

`RescueCircleList` is the plain "circles I organize or joined" list (`getMyCircles`), rendered as the same `.order-ticket` cards `Orders` uses. `RescueCircleInvite` is the actual invite-link destination, reachable by any signed-in customer: it tries `getDetail` (full per-participant paid/unpaid breakdown, organizer/participant-only) first and falls back to `getSummary` (counts only, no names) on a `403`, mirroring the same two-tier access the backend enforces. It then renders exactly one of: an unpaid participant's "pay share" button (plus "leave" for a non-organizer); a non-participant's "join an open slot" button (if the circle isn't full); or a read-only state for everyone else (full, cancelled, or already fully paid). `RescueCircleReturn` is the Stripe success-redirect landing page for one participant's own share — same `.login-page`/`.cart-confirmation` shell as `PaymentReturn` — branching its success copy on whether the *whole* circle is now paid or just this one share.

### StandingOrders — every saved "usual" in one place (`/standing-orders`, Customer)

**File:** `components/StandingOrders/index.tsx`

The management counterpart to `BusinessDetail`'s inline create form: a table of every standing order the customer has, across every business, with Pause/Resume/Remove per row. Creation only happens from a specific business's own page (§above) — there is no "create" control here.

### Referrals — invite link & store credit (`/referrals`, Customer)

**File:** `components/Referrals/index.tsx`

One `referralsApi.getMine()` call backs the whole page. The referral link itself is built client-side (`${window.location.origin}/account/register?ref=${code}`) rather than returned by the backend, since only the browser knows what host it's actually being served from. The invite table shows a "Reward earned"/"Waiting on first order" pill per row straight off the DTO's own `rewarded` flag — no polling, since a reward can only change between page loads.

### BusinessApply — self-service business signup (`/businesses/apply`, Customer + BusinessManager)

**File:** `components/BusinessApply/index.tsx`

A deliberately small form (name/description/address/type/image URL) posting to `businessesApi.apply` (not `create`). On success the form is replaced in place by a static confirmation card — there's no redirect to a detail page and no "my applications" list, since a `PendingApproval` business has no public page to redirect to anyway (`BusinessDetail`'s own visibility gate, above). The applicant learns the outcome via an in-app notification once an admin approves or rejects it.

### PaymentReturn / PaymentCancel — the Stripe redirect landings (`/checkout/return`, `/checkout/cancel`, Customer)

**Files:** `components/PaymentReturn/index.tsx`, `components/PaymentCancel/index.tsx`

`PaymentReturn` is where the order actually gets created — Stripe's `success_url` carries `pc`/`session_id` query params, which `paymentsApi.complete(pc, sessionId)` exchanges for the finished order. This endpoint is idempotent server-side, so reloading this page (a stale tab, a double-redirect) returns the same already-created order rather than placing a second one. On success the basket is cleared (`cart.clear()`) — not before, since the basket shouldn't be considered spent until payment is actually confirmed — and the success screen shows the order number plus a kg-saved impact line. `PaymentCancel` is static: no API call, no cart mutation (the basket was never touched by starting checkout), just a "nothing was charged" message and a link back to `/`.

### Account pages (`/account/...`)

**Files:** `components/Account/{Login,Register,ConfirmEmail,ResendConfirmation,ForgotPassword,ResetPassword,AccountSettings,AccessDenied}/index.tsx`

All render under `EmptyLayout` except `AccountSettings` (`PublicLayout`, reachable to any signed-in role via the header's gear icon) and `AccessDenied` (a static `ForbiddenPanel` wrapper, not currently linked to by `RequireRole` itself, which renders its own inline `ForbiddenPanel` instead of redirecting here). `Login` reads `?returnUrl=`/`?info=` query params — the former from `RequireRole`'s redirect, the latter from `Register` (email-confirmation-required) and `AccountSettings` (post-password-change forced sign-out) linking back with a success banner. `Register` prefills an optional `?ref=CODE` referral code from a friend's invite link, still editable by hand. `ConfirmEmail` guards against React's `StrictMode` double-invoking its mount effect with a `useRef` flag, since the confirmation token is single-use server-side and a second identical call would fail even though the first one already succeeded. `AccountSettings` splits into two independent forms — display name (a normal API call) and change-password (which, on success, signs the user out and redirects to `Login` with an info banner, since rotating the password invalidates the current JWT immediately).

### NotFound (`*`, `/not-found`)

**File:** `components/NotFound/index.tsx` — the catch-all for any unmatched route, under `PublicLayout`.

---

## 8. Business-Manager & Admin Pages

All of these render under `DashboardLayout`, gated at the layout level to `Admin`/`BusinessManager`, with four pages additionally gated to `Admin` only (nested `RequireRole`, §3).

### Dashboard (`/dashboard`)

**File:** `components/Dashboard/index.tsx`

Stat cards (Businesses/Users: Admin-only; Packages/Orders: scoped to the signed-in manager's currently-selected business, or platform-wide for an Admin) plus a hand-rolled 14-day Orders/Kg-saved trend chart and a second "Business Analytics" card (sell-through rate, busiest pickup hours) — no charting library, just absolutely-positioned divs with heights from `utils/dashboardAnalytics.ts`'s pure bucketing functions. An Admin with no business selected (or a manager with none assigned) sees "Not assigned" in place of the Orders stat rather than a spinner that never resolves.

Two collapsible "Business tools" panels (plain `useState` toggles, not Bootstrap's accordion JS): **Share your impact**, a copy-pasteable `<script>` snippet pointing at the Api's own `/js/impact-widget.js` with the business id baked in; and **POS/inventory webhook**, which generates/revokes a per-business API key (shown once, on generation) and documents the webhook endpoint/payload shape for a POS integrator to call directly.

### Businesses / BusinessForm (`/businesses`, `/businesses/create`, `/businesses/edit/:id`)

**Files:** `components/Businesses/index.tsx`, `components/BusinessForm/index.tsx`

`Businesses` branches hard on role: an Admin gets the full platform list — search/type/status filters, a staff-assignment `AnchoredDropdown` per row, and every moderation action (approve, reject-with-reason, hide-with-reason, unhide, delete) driven by a shared `ReportDialog`/`ConfirmDialog`; a BusinessManager just sees their own staffed businesses, read-only except for an Edit link. `BusinessForm` is Admin-only for create, Admin-or-staff-member for edit (checked client-side via a `GET .../staff` call before the form even renders, so a non-staff manager sees `ForbiddenPanel` immediately rather than after filling the whole thing out — the Api enforces the same rule server-side regardless). An edit also unlocks two sub-sections absent on create: **Opening Hours** (seven always-rendered day rows, open/close `<input type="time">` pairs, a closed-checkbox per day) and **Holiday Closures** (a date-range + optional reason, overriding the weekly hours for that span).

### Packages / PackageForm / PackageTemplates (`/packages`, `/packages/create`, `/packages/edit/:id`, `/packages/templates`)

**Files:** `components/Packages/index.tsx`, `components/PackageForm/index.tsx`, `components/PackageTemplates/index.tsx`

`Packages` is the densest admin list in the app: paginated, search/type/business-filterable, with per-row badges for "Daily" (template-generated), "Hidden," and "Donated," a **multi-select bulk toolbar** (duplicate / adjust quantity by a signed delta / extend the pickup window by N hours, applied to every checked row across pages), and two AI-driven nudges — a markdown-price-suggestion icon (only shown on candidates the backend already flagged as closing-soon-with-stock) and a mark-as-donated shortcut (only on candidates that closed with nothing sold). `PackageForm` offers an AI "Write it for me" description draft (`aiApi.draftDescription`, gated the same `409`-means-"not configured" way as every other AI feature) and an optional "Repeat this every day" checkbox on create, which — alongside creating the package — registers it with `packageTemplatesApi.create` so it starts auto-generating daily. `PackageTemplates` is the pure management view over those templates: pause/resume/delete, with a `lastGeneratedDate` column so a manager can tell a template is still actually firing.

### OrderManagement / OrderScan / OrderValidate / OrderValidateLegacy (`/orders/manage`, `/orders/scan`, `/orders/validate/:id/:passId`, `/orders/validate/:id`)

**Files:** `components/OrderManagement/index.tsx`, `components/OrderScan/index.tsx`, `components/OrderValidate/index.tsx`, `components/OrderValidateLegacy/index.tsx`

`OrderManagement` is the staff-facing order table — searchable/filterable by status and (Admin-only) business, with inline status-transition buttons per row (Confirm/Cancel on Pending; Complete/No-show/Cancel on Confirmed — No-show is disabled until the pickup window has actually closed) and a date-ranged CSV export. Clicking a row (outside its action buttons) opens `OrderDetailModal`.

The pickup-confirmation flow is a three-page handoff, unchanged in shape from the backend's own QR payload format:

```
Customer's phone                    Manager's device
─────────────────                    ──────────────────
OrderPickupPass (§7)
  {origin}/orders/validate/{orderId}/{passId}
        │ customer shows screen to counter
        ▼
                                     OrderScan
                                       live camera → jsQR decode loop (requestAnimationFrame)
                                       valid decode → client-side navigate()
                                             │
                                             ▼
                                     OrderValidate
                                       re-checks auth + order ownership itself
                                       "Confirm pickup" → redeems this pass, order → Completed
```

`OrderScan`'s capture→decode loop (`useQrScanner`, §10) only starts after an explicit "Start scanning" tap — never on render — both for reliable iOS Safari behavior and so a camera permission prompt doesn't fire before the page's own route guard has even settled. A successful decode is validated against a strict same-origin, `/orders/validate/{guid}/{guid}`-shaped pattern before navigating anywhere — a security boundary, not just parsing convenience: a maliciously crafted or unrelated QR code pointed at this camera by mistake simply can't redirect an authenticated manager's session off-app. A manual lookup fallback (order number → `ordersApi.getForManagementPaged` filtered to `Confirmed`) exists for a torn screen, no camera, or a customer reading their order number aloud.

`OrderValidate` re-fetches and re-checks ownership itself regardless of how the visitor arrived (in-app scanner, a manually typed URL, or a third-party QR app opening the link directly) — it never trusts the navigation path that got it there. "Confirm pickup" redeems *this* pass and completes the whole order in one call; a conflict (already redeemed, a different pass already completed the order, or the order was cancelled — a duplicate scan or a race with the manager dashboard) re-fetches rather than leaving a stale badge on screen. `OrderValidateLegacy` exists purely to catch a QR code printed before pickup passes carried their own route segment: it resolves the order's single pass and forwards (`<Navigate replace>`) to the real `OrderValidate` route, or shows an "ambiguous" panel if the order somehow has zero or multiple passes.

### Payments (`/payments`)

**File:** `components/Payments/index.tsx`

A read-only payout ledger — same business-scoping rule as `OrderManagement` (Admin sees everything, a manager sees their selected business), with "collected"/"refunded" stat cards computed over the *current page* only (not the whole filtered set) and its own CSV export.

### Users (`/users`, Admin only)

**File:** `components/Users/index.tsx`

Search/role-filterable user table. Each row's role is changed via an `AnchoredDropdown` (disabled on the signed-in admin's own row — you can't change your own role) and, only for a `BusinessManager` row, a second `AnchoredDropdown` to add/remove business-staff assignments — the two dropdowns are mutually exclusive, opening one closes the other. Changing someone's role away from `BusinessManager` can auto-release a business server-side, so a role change also triggers a staff-assignment reload, not just the user list.

### Reports (`/reports`, Admin only)

**File:** `components/Reports/index.tsx`

The open-reports moderation queue — customer-submitted flags against a business, package, or kitchen tip. Each row offers Dismiss (closes with no action) or "Hide target" (hides the underlying business/package using the report's own submitted reason), both behind a `ConfirmDialog`.

### AuditLog (`/audit-log`, Admin only)

**File:** `components/AuditLog/index.tsx`

A read-only, paginated, search/action/target-type-filterable history of moderation and role-change actions. The action dropdown is a deliberately curated subset of the backend's full action-constant list, matching what's actually worth filtering by in practice rather than exhaustively mirroring every constant.

### Types (`/types`, Admin only)

**File:** `components/Types/index.tsx`

Three independent cards — kitchen (business) types, package types, brands — sharing one inline add/rename/delete UX and one `ConfirmDialog` for the delete confirmation across all three. Deleting something still referenced elsewhere surfaces the backend's own conflict message rather than a generic failure.

---

## 9. Shared Components

**Location:** `src/components/common/`

- **`AnchoredDropdown`** — a `position: fixed`, viewport-clamped dropdown that opens upward when there's no room below, positioned via `getBoundingClientRect()` on its trigger rather than plain CSS (so it can escape an ancestor's `overflow`/stacking-context clipping, the same problem described in §4). Used by the sidebar's business switcher (`NavMenu`) and every staff-assignment / role-change control (`Businesses`, `Users`).
- **`CartPanel`** — the slide-in basket (§7's Rescue Circle starter lives inside it).
- **`ConfirmDialog`** — a generic confirm/cancel modal; almost every destructive action in the app (delete, cancel order, start a new basket, revoke a webhook key, …) is one call site passing it a title/message/confirm label.
- **`EmptyState`** — icon + muted message, used for every "nothing here yet" block across list pages.
- **`ForbiddenPanel`** / **`NotFoundPanel`** — inline, chrome-free "access denied"/"not found" blocks, rendered *in place* rather than as a redirect (see `RequireRole`, §3, and the various pages' own 403/404 branches).
- **`ImpactEquivalencyStats`** — turns a raw kg-saved number into "~N km not driven"/"~N L of water saved" chips (`utils/impactEquivalency.ts`); used by `OrderDetailModal` (a Completed order's own total) and `Impact`'s hero (the platform-wide total).
- **`LoadingSpinner`** — a centered Bootstrap spinner, the default "still loading" state for most pages.
- **`NotificationBell`** / **`NotificationPanel`** — trigger and popup, split per §4/§5; the panel also owns the web-push "enable browser alerts" toggle (`usePushSubscription`, §10).
- **`OrderDetailModal`** — the order receipt drill-down shared by `Orders` and `OrderManagement`, reusing the package-modal's own `.pkg-modal-facts` CSS shell under an `.order-modal` namespace.
- **`PackageDetailModal`** — the package drill-down from `BusinessDetail`'s package rows: full description/dietary tags/pickup window, a quantity stepper + "Add to basket" for a signed-in Customer, and its own report-flag button.
- **`Pagination`** — a plain prev/next + "Page X of Y" control; renders nothing when `totalPages <= 1`.
- **`ReportDialog`** — a `ConfirmDialog` variant that additionally collects a required free-text reason; reused for a customer reporting a business/package/kitchen tip *and* for an admin's reject-application/hide-business prompts (same shell, different copy/handler).
- **`Skeleton`** (`BusinessCardSkeletons`, `ListRowSkeletons`) — loading placeholders built from Bootstrap's own `.placeholder`/`.placeholder-glow` utility classes rather than a bespoke style.
- **`StarRating`** — one component, two modes via a discriminated-union prop type: a read-only fractional-fill display (`editable?: false`) or an editable 1–5 click/hover picker (`editable: true`).
- **`ThemeToggle`** — the dark-mode trigger button, used in both `PublicLayout`'s header and `NavMenu`'s sidebar footer.

---

## 10. Custom Hooks

**Location:** `src/hooks/` — this is the direct replacement for the old Blazor app's JS-interop layer; every one of these used to be a `window.EcoMeal.*` call into hand-written JS. Now it's native browser APIs wrapped in a hook.

- **`useDebouncedCallback(callback, delayMs)`** — a generic "collapse rapid calls into one, after a quiet period" wrapper (clear-and-reschedule `setTimeout`), used wherever a filter/search handler shouldn't fire on every keystroke.
- **`useDebouncedValue(value, delayMs)`** — the companion "debounce a value itself" shape, used by `Home`'s search box (300 ms, matching every other paginated list page's search debounce).
- **`useGeolocation()`** — wraps `navigator.geolocation.getCurrentPosition` with an 8-second native timeout *and* a hard 10-second client-side fallback timeout on top, so the returned promise **always** settles (`GeoPosition | null`) and never hangs or throws — callers never need a `try/catch`. This exists specifically to guarantee a real, known failure mode (a browser/extension that never calls either geolocation callback at all) can't hang a page waiting on it.
- **`useLeafletMap(containerRef, markers, onMarkerClick?)`** — vanilla Leaflet (not `react-leaflet`), map instance and marker layer group both torn down on unmount/re-render so repeatedly toggling a map view on and off never leaks a Leaflet instance. Explicitly re-points Leaflet's default marker icon URLs at the bundler-resolved asset paths, since Vite doesn't rewrite the relative URLs Leaflet's own CSS bakes in. Defaults to centering on Timișoara when there are no markers to fit bounds to.
- **`useQrScanner(videoRef, canvasRef)`** — `getUserMedia` → draw each frame to an off-screen canvas → `jsQR` decode, looped via `requestAnimationFrame`; stops every media track on unmount or explicit stop so the OS's camera-in-use indicator doesn't stay lit after navigating away. A successful decode is validated same-origin-and-shape before it's allowed to trigger a `react-router` navigation (§8's security note).
- **`usePushSubscription()`** (+ the standalone `registerServiceWorker()`/`isPushSupported()` exports) — subscribe/unsubscribe/get-current-endpoint for Web Push, including the VAPID base64url→`Uint8Array` conversion the Push API requires. Every method resolves rather than rejects, the same "never make the caller write a try/catch" convention `useGeolocation` follows.
- **`useStockHub(businessId, onChanged)`** — the SignalR client hook; see §11.

---

## 11. Live Stock Over SignalR

**File:** `src/hooks/useStockHub.ts`, consumed by `BusinessDetail` (§7).

```ts
function hubUrl(): string {
  const apiBase = import.meta.env.VITE_API_URL as string;
  return `${apiBase.replace(/\/api\/?$/, "")}/hubs/stock`;
}

const connection = new signalR.HubConnectionBuilder()
  .withUrl(hubUrl(), {
    accessTokenFactory: () => localStorage.getItem(TOKEN_KEY) ?? "",
    withCredentials: false,
  })
  .withAutomaticReconnect()
  .build();

connection.on("BusinessStockChanged", (changedBusinessId: string) => {
  if (changedBusinessId === businessId) onChangedRef.current();
});
```

On connect (and on every reconnect), the hook calls `connection.invoke("JoinBusinessGroup", businessId)` to join that business's SignalR group; the Api's `PackageService`/`OrderService` broadcast `BusinessStockChanged` to that group whenever an order is placed/confirmed/cancelled/no-showed, a manager edits/hides/restocks a package, or a background sweep expires something. `BusinessDetail` responds by re-running its own package load — not by trying to patch individual fields — so the "X left" numbers and the `PackageDetailModal` (if open) stay correct with no polling.

**The `withCredentials: false` line is load-bearing, not a stylistic default override.** `@microsoft/signalr`'s `HubConnectionBuilder` defaults `withCredentials` to `true`, which tells the browser to send cookies/credentials with every hub request. Auth here is a bearer token on the query string via `accessTokenFactory`, never a cookie — and the Api's dev CORS policy allows any origin but does **not** set `AllowCredentials()`. A credentialed request against a non-credentialed CORS policy doesn't fail loudly: the preflight `OPTIONS` still returns `204`, but the browser silently discards the real response for not echoing back `Access-Control-Allow-Credentials`, surfacing only as a bare, logless "Failed to fetch." Setting `withCredentials: false` avoids ever needing a credentialed CORS policy for a connection that was never actually credentialed to begin with — this is the one gotcha in the whole SignalR integration worth remembering if the hub connection ever mysteriously stops connecting after a CORS policy change.

Connecting and joining are both best-effort: a page that can't reach the hub at all still works off its own fetch-on-load data, just without the live push — every failure here is swallowed (`.catch(() => {})`) rather than surfaced as a page error.

---

## 12. CSS Design System

**File:** `src/index.css` — ~4,500 lines, one file, no preprocessor, no CSS Modules, no component-library theme override layer. This is a close, deliberate port of the old Blazor app's `app.css`: the same design tokens, the same light/dark theme, the same component classes (`.biz-pkg-add-btn`, `.order-ticket`, `.em-popover`, …) — **not** replaced with a component library, specifically to keep pixel parity with the UI the app has always looked like.

**Design tokens** (`:root`, redefined under `:root[data-theme="dark"]`): two brand hues doing real semantic work — `--em-leaf` (herb-green, "fresh/rescued") and `--em-rescue` (an orange, "reduced/closing soon" urgency signal) — on a warm paper ground (`--em-paper`/`--em-bg`) rather than a cool neutral one, plus a reserved gold (`--em-gold`) for "you earned this" moments (loyalty, streaks, leaderboard rank #1). A semantic layer (`--em-bg`, `--em-surface-raised`, `--em-border`, `--em-text-muted`, …) is what components actually reach for, so dark mode (toggled by `ThemeContext` setting `data-theme="dark"` on `<html>`, §5) only has to redefine the token layer once rather than touch every component rule. A separate `--em-*-solid` token set exists for solid-fill buttons/badges that need white text on top regardless of theme, since dark mode brightens the on-surface accent colors in a way that would otherwise fail contrast against white text. Three Google Fonts back the type system via CSS custom properties: `Fraunces` (display headings), `Hanken Grotesk` (body), `JetBrains Mono` (order numbers/QR labels).

**The signature visual** is the **order ticket** (`.order-ticket` and its `-main`/`-seam`/`-stub` children) — a perforated-receipt look (a dashed `.order-ticket-seam` divider between the order details and a stub carrying the order number/QR code) used consistently across `Orders`, `OrderPickupPass`, and `RescueCircleList`, so a pending rescue always reads as a physical ticket regardless of which page it's showing on.

**Popovers, not Bootstrap JS:** `.em-popover` (+ `.em-popover-backdrop`/`-header`/`-body`/`-footer`) is the shared shell for every click-to-open panel that isn't a full modal — `Home`'s Filters panel and `OrderManagement`'s CSV-export panel both use it, gated by a plain `useState` boolean in the component rather than any Bootstrap `data-bs-toggle` wiring (there is none — see §2).

**Dark mode** is `data-theme`/`data-bs-theme` driven (§5/§2), with every accent color re-checked for contrast against both surfaces rather than naively inverted — a hairline light border substitutes for `box-shadow` elevation in dark mode, since a flat black shadow reads as no shadow at all on a dark surface.

---

## 13. Build, Test, Lint & Docker

**`package.json` scripts:**

```
npm run dev       # vite dev server
npm run build      # tsc -b && vite build
npm run lint       # oxlint
npm run preview    # vite preview
npm run test        # vitest run
```

**Linting** is `oxlint`, not ESLint — a deliberate choice for this project, not a gap to fill in with an ESLint config. Type-checking is a separate step (`tsc -b`, invoked as part of `build`) rather than something oxlint does itself.

**Testing** is `vitest` + `@testing-library/react` + `msw`, configured in `vite.config.ts` (`environment: "jsdom"`, `setupFiles: ["./src/test/setup.ts"]`, `globals: true`). Test files sit next to what they test rather than in a mirrored `__tests__/` tree — `RequireRole.test.tsx` next to `RequireRole.tsx`, `CartProvider.test.tsx` next to `CartProvider.tsx`, `http.test.ts` next to `http.ts`, and several `utils/*.test.ts` files (`currency`, `packageAvailability`, `packagePickup`, `tripRoute`) covering the pure formatting/calculation helpers most worth pinning down.

**Environment:** `VITE_API_URL` (gitignored `.env`/`.env.local`, documented in `.env.example`) **must** end in `/api` — every frontend API call path starts with a bare `/segment` (e.g. `/auth/login`), so the full request URL is simply `VITE_API_URL + path`. `useStockHub` (§11) derives the SignalR hub's own URL by stripping that trailing `/api` back off, since the hub is mounted on the Api host's root, not under `/api`.

**Docker:** `Frontend/eco-meal-frontend/Dockerfile` is a two-stage build:

```dockerfile
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ARG VITE_API_URL=http://localhost:8081/api
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

`VITE_API_URL` is a build **ARG**, not a runtime environment variable — Vite bakes it into the static JS bundle at build time, since the bundle runs in the browser, entirely outside whatever Docker network the containers share at runtime; there is no server-side process reading it later. Changing the API URL for an already-built image means rebuilding the image, not restarting the container with a different env var. `nginx.conf` does plain SPA fallback (`try_files $uri $uri/ /index.html`) so a hard reload on any client-side route (e.g. `/businesses/abc-123`) still resolves to `index.html` and lets React Router take over, rather than 404ing at the nginx layer.
