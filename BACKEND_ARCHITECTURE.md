# Backend Architecture — Netrom Eco Meal

**Stack:** ASP.NET Core 10 · Blazor Server (interactive server render mode) · Entity Framework Core · PostgreSQL (Npgsql) · ASP.NET Identity (cookie auth) · QRCoder
**Location:** repo root (single project, `Netrom-Eco-Meal.csproj`)

Unlike a typical API + SPA split, this is **one ASP.NET Core project** that is both the backend and the Blazor Server UI host. There's no separate `Api`/`BusinessLogic`/`DataAccess` assembly split — instead the layering is expressed as top-level folders in one project, and Razor components call straight into the service layer through a DI-injected "Controller" rather than over HTTP. See [§6](#6-controllers--the-in-process-façade-pattern) for why that's a deliberate choice, not an oversight.

---

## Table of Contents

1. [Solution Structure](#1-solution-structure)
2. [Project Layers](#2-project-layers)
3. [Data Model & Entities](#3-data-model--entities)
4. [Repository Pattern](#4-repository-pattern)
5. [Service Layer](#5-service-layer)
6. [Controllers — the In-Process Façade Pattern](#6-controllers--the-in-process-façade-pattern)
7. [Authentication & Authorization](#7-authentication--authorization)
8. [Background Services](#8-background-services)
9. [Database Seeding](#9-database-seeding)
10. [Configuration Reference](#10-configuration-reference)
11. [Automated Tests](#11-automated-tests)

---

## 1. Solution Structure

```
Netrom-Eco-Meal/
├── Program.cs                  # DI registration, middleware pipeline, startup migrate + seed
├── Entities/                   # EF Core entity classes
├── Database/
│   ├── EcoMealDbContext.cs     # IdentityDbContext<ApplicationUser> + Fluent API config
│   └── DbSeeder.cs             # Idempotent reference-data + demo-data seeding
├── Repositories/
│   ├── Interfaces/              # One interface per entity needing custom queries
│   └── *.cs                     # EF Core implementations
├── Services/
│   ├── Interfaces/               # One interface per service
│   └── *.cs                      # Business logic, DI-registered Scoped (2 are BackgroundService/plain classes — see §5)
├── Controllers/                 # ApiController classes — mostly in-process façades, see §6
├── Constants/                   # Fixed string values, enums-as-strings, small pure helpers
├── Models/                      # PaginatedList<T>, Debouncer (frontend-facing, see FRONTEND_ARCHITECTURE.md)
├── Migrations/                  # EF-generated migration history
├── Components/                  # Blazor UI — see FRONTEND_ARCHITECTURE.md
├── wwwroot/                     # Static assets — see FRONTEND_ARCHITECTURE.md
└── Tests/                       # Netrom-Eco-Meal.Tests — xUnit, see §11
```

`Program.cs` wires every layer with plain `AddScoped<TInterface, TImplementation>()` calls — no assembly scanning, no MediatR, no separate composition-root project.

---

## 2. Project Layers

### 2.1 Entities

Plain EF Core entity classes, no DTOs anywhere in the app — Razor components bind directly to entities (see [FRONTEND_ARCHITECTURE.md §11](FRONTEND_ARCHITECTURE.md) for the two `EditForm` pages that use a private nested `FormModel` instead, purely for `[Required]`/`[Range]` validation attributes that don't belong on the persisted entity).

### 2.2 Database

| File | Role |
|------|------|
| `EcoMealDbContext.cs` | `IdentityDbContext<ApplicationUser>` — `DbSet<T>` for `Business`, `BusinessType`, `Order`, `OrderPackage`, `Package`, `PackageType`, `Status`, `Review`, `Notification`, `Favorite`; all Fluent API config lives inline in `OnModelCreating` (no separate `Configurations/` folder — the model is small enough that splitting it out would be pure ceremony) |
| `DbSeeder.cs` | Static `SeedAsync(services, configuration)` called once from `Program.cs` after `MigrateAsync()` — see [§9](#9-database-seeding) |
| `Migrations/` | Notable ones: `AddOrderNumberSequenceAndPackageConcurrency` (the `order_numbers` DB sequence + `xmin` row-version column), `RemoveStalePackageSeedData` / `RemoveStaleBusinessSeedData` (cleaned up seed rows from an earlier, cruder seeding approach before `DbSeeder` existed), `AddPackageWeightKg`, `AddNotificationsFavoritesOrderCreatedAtPackageDietaryTags` (one migration bundling four Phase 2 features together) |

### 2.3 Repositories

Thin data-access classes — see [§4](#4-repository-pattern). No generic `IRepository<T>` base: every repository interface is hand-written for its entity, since query shapes differ enough (pagination, business-scoping, xmin concurrency) that a generic base would need overrides for almost every method anyway.

### 2.4 Services

All business logic, authorization checks, and orchestration — see [§5](#5-service-layer).

### 2.5 Controllers

`ApiController` classes that are, with two exceptions, never actually reachable over HTTP — see [§6](#6-controllers--the-in-process-façade-pattern).

---

## 3. Data Model & Entities

### Entity Overview

```
ApplicationUser (IdentityUser)
  │
  ├──< Order >──── Business ──── BusinessType
  │      │             │
  │      └──< OrderPackage >──── Package ──── PackageType
  │
  ├──< Favorite >──── Business        (one per (UserId, BusinessId), unique index)
  ├──< Review >────── Business        (one per (BusinessId, UserId), unique index)
  ├──< Notification
  └──── Business                      (Manager — 0..1, unique index on ManagerId)

Order ──── Status                     (Pending | Confirmed | Completed | Cancelled | NoShow)
```

### Entities

#### ApplicationUser
```csharp
public class ApplicationUser : IdentityUser
{
    public required string Name { get; set; }
    public ICollection<Order> Orders { get; set; } = [];
}
```
Extends Identity's built-in user with the one field Identity doesn't provide: a display name. Everything else (email, password hash, roles) comes from `IdentityUser`/`IdentityDbContext` for free.

#### Business
```csharp
public class Business
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Address { get; set; }
    public string? ImageUrl { get; set; }
    public Guid BusinessTypeId { get; set; }
    public BusinessType BusinessType { get; set; } = null!;
    public string? ManagerId { get; set; }         // nullable/unique: a manager oversees at most one business
    public ApplicationUser? Manager { get; set; }
    public ICollection<Package> Packages { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
    public ICollection<Favorite> Favorites { get; set; } = [];
}
```
`ManagerId` has a **unique index** (`EcoMealDbContext.OnModelCreating`) — enforced at the DB level so two concurrent "assign this manager" admin actions can't both succeed and leave one manager running two businesses. `OnDelete(DeleteBehavior.SetNull)` means deleting a manager's user account un-assigns them rather than failing the delete or cascading.

#### BusinessType / PackageType / Status
```csharp
public class BusinessType { public Guid Id; public required string Name; }
public class PackageType  { public Guid Id; public required string Name; }
public class Status       { public Guid Id; public required string Name; }
```
Three near-identical lookup tables, seeded once by `DbSeeder` and never written to afterward. `Status.Name` values are fixed by `Constants.OrderStatuses` (`Pending`/`Confirmed`/`Completed`/`Cancelled`/`NoShow`) and looked up by name everywhere rather than by a hardcoded `Guid` — the seed IDs exist only so re-seeding is idempotent. `NoShow` was added after the original four in a way that had to work around an old migration having hardcoded `InsertData` for those four (see §9) — `SeedStatusesAsync` adds whichever names are missing rather than bailing out when the table is merely non-empty.

#### Package
```csharp
public class Package
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required Guid PackageTypeId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required decimal Price { get; set; }
    public required int Quantity { get; set; }      // live stock: decremented on confirm, restored on cancel-from-confirmed
    public required decimal WeightKg { get; set; }   // drives the "food saved" impact stats
    public List<string> DietaryTags { get; set; } = [];  // free-form, from Constants.DietaryTags.All
    public required DateTime PickupStart { get; set; }
    public required DateTime PickupEnd { get; set; }
    public string? ImageUrl { get; set; }
    public Business Business { get; set; } = null!;
    public PackageType PackageType { get; set; } = null!;
    public ICollection<OrderPackage> OrderPackages { get; set; } = [];
}
```
Restocking a package from `0` to a positive `Quantity` (or publishing a brand-new one) notifies everyone who's favorited that business — see `PackageService` in §5; there's no per-package "notify me" subscription, so Favorites doubles as the closest proxy.

`Quantity` carries an EF Core **shadow property row-version** — `modelBuilder.Entity<Package>().Property<uint>("xmin").IsRowVersion()` maps Postgres's native `xmin` system column as an optimistic-concurrency token, with zero extra columns to migrate. Two managers confirming orders against the same package's last unit at the same time get a `DbUpdateConcurrencyException` on the loser, translated by `OrderService` into "Stock for this order just changed — please refresh and try again" instead of silently overselling. `DietaryTags` is stored as a plain `List<string>` — EF Core 8+ maps this to a Postgres `text[]` column with no extra configuration needed.

#### Order / OrderPackage
```csharp
public class Order
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    public required Guid StatusId { get; set; }
    public int OrderNumber { get; set; }            // assigned by the order_numbers DB sequence on insert
    public DateTime CreatedAt { get; set; }
    public DateTime? PickupReminderSentAt { get; set; }  // set once, so the reminder sweep never double-sends
    public required ApplicationUser User { get; set; }
    public Business Business { get; set; } = null!;
    public Status Status { get; set; } = null!;
    public ICollection<OrderPackage> OrderPackages { get; set; } = [];
}

public class OrderPackage
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid PackageId { get; set; }
    public required int Quantity { get; set; }      // quantity ordered on this line — distinct from Package.Quantity (live stock)
    public Order Order { get; set; } = null!;
    public Package Package { get; set; } = null!;
}
```
`OrderNumber` is **not** application-assigned — `EcoMealDbContext` maps it to `nextval('order_numbers')` via `HasDefaultValueSql`, backed by a real Postgres sequence (`modelBuilder.HasSequence<int>("order_numbers")`). This means two concurrent checkouts can never collide on the same human-friendly ticket number the way an app-side `MAX(OrderNumber) + 1` could. `OrderNumber` also carries its own unique index as a belt-and-suspenders check. Unlike the reviewed Smart Shopping Assistant project, `OrderPackage` does **not** snapshot the package's name/price at order time — it stays live-joined to `Package`, so a manager editing a package's name after the fact would (in principle) change how historical orders render. This hasn't come up as a real problem in practice since packages are re-created daily rather than edited after orders exist against them.

#### Review
```csharp
public class Review
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required string UserId { get; set; }
    public required int Rating { get; set; }        // 1-5
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public Business Business { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
```
Unique index on `(BusinessId, UserId)` — resubmitting via `ReviewService.SubmitAsync` updates the existing row in place (and bumps `CreatedAt`) rather than creating a duplicate. Gated on `IOrderRepository.HasCompletedOrderAsync(userId, businessId)` — a customer must have at least one `Completed` order with that business before they can review it.

#### Favorite
```csharp
public class Favorite
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Business Business { get; set; } = null!;
}
```
Unique index on `(UserId, BusinessId)`. `FavoriteRepository.AddAsync`/`RemoveAsync` persist immediately (their own `SaveChangesAsync` call) rather than following the stage-then-`SaveChangesAsync` split most other repositories use — there's no batching scenario where a favorite toggle needs to ride along with another unsaved change.

#### Notification
```csharp
public class Notification
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string Message { get; set; }
    public string? Url { get; set; }                // relative app URL, e.g. "/orders"
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
```
Created server-side by `OrderService` at every status transition (new order → the business's manager; confirmed/completed/cancelled/no-show → the customer), by `OrderLifecycleSweepService` on auto-cancel/pickup-reminder/no-show, and by `PackageService` on restock. Every one of those customer-facing order-lifecycle notifications also fires a best-effort email via `IAppEmailSender` (see §5/§8) — the in-app bell record is still the source of truth; the email is a delivery-channel add-on that never blocks the underlying transition if it fails. Indexed on `(UserId, CreatedAt)` since "this user's notifications, newest first" is the only query pattern (`NotificationRepository.GetRecentByUserIdAsync`). See [§4](#4-repository-pattern) for why this repository is architecturally distinct from the rest.

### DateTime handling

`EcoMealDbContext.OnModelCreating` installs a single `ValueConverter<DateTime, DateTime>` across **every** `DateTime` property in the model (via reflection over `modelBuilder.Model.GetEntityTypes()`), tagging any non-UTC-kind value as `DateTimeKind.Utc` on the way in and out:

```csharp
var utcDateTimeConverter = new ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
```
Npgsql rejects `Kind = Unspecified` for `timestamptz` columns outright — this converter sidesteps that without the app tracking timezones of its own; the DB always stores and returns UTC, and the *viewer's* local time is reconstructed client-side (see `ClientTimeZoneService` in `FRONTEND_ARCHITECTURE.md`). Applying it globally instead of per-property is what keeps every new `DateTime` field (pickup windows, `CreatedAt`, etc.) safe by default without a developer having to remember to opt in.

---

## 4. Repository Pattern

No generic base — each interface is purpose-built. All "write" repositories follow the same convention: `AddAsync`/`DeleteAsync` only stage the change via `context.Businesses.Add(...)`, and a separate `SaveChangesAsync()` call actually persists it, so a service method can make several repository calls and commit them together in one transaction.

| Repository | Key methods | Notes |
|---|---|---|
| `IBusinessRepository` / `BusinessRepository` | `GetAllAsync`, `GetPagedAsync(search, businessTypeId, managerId?, sortBy?, favoritedByUserId?)`, `GetByIdAsync`, `GetByManagerIdAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | `GetPagedAsync`'s search also matches **live packages'** name/description (`b.Packages.Any(p => p.PickupEnd > now && ...)`), so searching "bread" surfaces a bakery even if its own name/description doesn't mention bread. `sortBy: "closingSoon"` orders by each business's nearest live `PickupEnd` (`?? DateTime.MaxValue` so businesses with nothing live sort last regardless of mode) |
| `IBusinessTypeRepository` / `BusinessTypeRepository` | `GetAllAsync` | Read-only lookup, no writes anywhere in the app |
| `IFavoriteRepository` / `FavoriteRepository` | `GetFavoriteBusinessIdsAsync`, `IsFavoriteAsync`, `AddAsync`, `RemoveAsync`, `GetFavoritingUsersAsync` | `AddAsync`/`RemoveAsync` persist immediately — the one repository that breaks the stage-then-save convention (see §3 Favorite). `GetFavoritingUsersAsync` feeds `PackageService`'s back-in-stock notifications |
| `INotificationRepository` / `NotificationRepository` | `GetRecentByUserIdAsync`, `GetUnreadCountAsync`, `MarkAsReadAsync`, `MarkAllAsReadAsync`, `CreateAsync` | Takes `IDbContextFactory<EcoMealDbContext>`, not the circuit-scoped `EcoMealDbContext` — every method opens and disposes its own short-lived context. This is what lets `NotificationBell`'s 30-second background poll (see FRONTEND_ARCHITECTURE.md) query the DB without racing whatever query the routed page is running against the shared per-circuit context at the same moment. `MarkAsReadAsync`/`MarkAllAsReadAsync` use `ExecuteUpdateAsync` — a single `UPDATE ... WHERE` round-trip, no load-then-save |
| `IOrderRepository` / `OrderRepository` | `GetAllAsync`, `GetByUserIdAsync`, `GetByBusinessIdAsync`, `GetPagedByUserIdAsync`, `GetPagedForManagementAsync(search, businessId?, status?)`, `GetInRangeAsync(businessId?, from?, to?)`, `GetByIdAsync`, `HasCompletedOrderAsync`, `GetTotalWeightSavedKgAsync`, `GetStalePendingOrdersAsync`, `GetOverduePickupOrdersAsync`, `GetPickupReminderCandidatesAsync`, `GetPendingQuantitiesByPackageIdsAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | The busiest repository — see `OrderService` in §5 for how its methods compose. `GetPagedForManagementAsync`'s order-number search strips a leading `#` and substring-matches the plain integer (`EF.Functions.ILike(o.OrderNumber.ToString(), ...)`) because Npgsql can't translate the zero-padded `ToString("000")` overload into SQL. `GetInRangeAsync` is unpaginated and date-bounded — it feeds both the CSV export and the dashboard trend chart (see §6 and FRONTEND_ARCHITECTURE.md §11). `GetPendingQuantitiesByPackageIdsAsync` is a `GroupBy`/`Sum` over `OrderPackages` for a batch of package IDs — the same Pending-reservation shape `PlaceOrderAsync`'s `pendingElsewhere` check computes per-package, exposed in bulk for display (§5) |
| `IPackageRepository` / `PackageRepository` | `GetAllAsync`, `GetPagedAsync(search, businessId?, packageTypeId?)`, `GetByIdAsync`, `GetByIdsAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | `GetByIdsAsync` is the batch-load path `CartService.RestoreAsync` uses to re-hydrate a localStorage-persisted cart into live `Package` entities on reconnect |
| `IPackageTypeRepository` / `PackageTypeRepository` | `GetAllAsync` | Read-only lookup |
| `IReviewRepository` / `ReviewRepository` | `GetAllAsync`, `GetByBusinessIdAsync`, `GetByBusinessIdsAsync`, `GetByUserAndBusinessAsync`, `AddAsync`, `SaveChangesAsync` | `GetByBusinessIdsAsync` is the batch path `Home.razor` uses to load ratings for an entire page of business cards in one query instead of one query per card |

### DI Registration (Program.cs)

```csharp
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IBusinessTypeRepository, BusinessTypeRepository>();
builder.Services.AddScoped<IPackageRepository, PackageRepository>();
builder.Services.AddScoped<IPackageTypeRepository, PackageTypeRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
```
`NotificationRepository` is `Scoped` like the rest even though it never touches the injected `EcoMealDbContext` — it's `Scoped` for consistency, not because it needs to be; it depends only on the singleton `IDbContextFactory<EcoMealDbContext>` registered alongside it.

### Pagination Helper

```csharp
public class PaginatedList<T>
{
    public List<T> Items { get; }
    public int PageIndex { get; }
    public int TotalPages { get; }
    public int TotalCount { get; }

    public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize);

    // For queries EF can't translate once projected into T (e.g. a record-constructor projection) —
    // order/page an intermediate shape, then map to T after materializing.
    public static async Task<PaginatedList<T>> CreateAsync<TSource>(
        IQueryable<TSource> source, Func<TSource, T> map, int pageIndex, int pageSize);
}
```
One `CountAsync` + one `Skip/Take` `ToListAsync`. The second overload exists specifically for `UserService.GetPagedAsync` — its query is a `join` producing an anonymous type (EF can't `OrderBy` a query already projected through a `record` constructor), so it orders/pages the anonymous shape and maps to `UserWithRole` only after materializing.

---

## 5. Service Layer

Every service is `Scoped`. A few break that pattern deliberately: `OrderLifecycleSweepService` is a `BackgroundService` (effectively a singleton — see §8); `CartService`/`ClientTimeZoneService` are `Scoped` but have no backing interface or repository — they're pure in-memory, per-circuit state with a JS-interop side door (documented in `FRONTEND_ARCHITECTURE.md` since they're really frontend concerns that happen to live in `Services/` alongside everything else); `SmtpEmailSender` (`IAppEmailSender`) is stateless and could be `Singleton` but is registered `Scoped` for consistency with everything else.

| Service Interface | Implementation | Responsibilities |
|---|---|---|
| `IAuthService` | `AuthService` | Thin wrapper over ASP.NET Identity's `SignInManager`/`UserManager` — `LoginAsync`, `RegisterAsync` (self-registration always lands in the `Customer` role; only an admin can promote from there), `LogoutAsync`, plus `ConfirmEmailAsync`/`RequestPasswordResetAsync`/`ResetPasswordAsync` for the email-confirmation and password-reset flows `Identity:RequireConfirmedAccount` unlocks (see §7/§10). Builds absolute links for those emails off `App:BaseUrl`, since neither a background sweep nor (reliably) a Blazor circuit has an `HttpContext` to derive one from |
| `IAppEmailSender` | `SmtpEmailSender` | One method, `SendEmailAsync(toEmail, subject, htmlBody)`, over `System.Net.Mail.SmtpClient`. Deliberately **not** ASP.NET Identity's `IEmailSender<TUser>` — this app has no Identity Razor Pages UI to trigger that interface, and order-lifecycle/back-in-stock emails aren't Identity's concern anyway. Reads `Email:Smtp:*`/`Email:From*` straight off `IConfiguration` (same style as `SeedAdmin:*`); when `Email:Smtp:Host` isn't set, it logs the email instead of sending — the same "optional infra, degrade gracefully" pattern `DbSeeder` uses for `SeedAdmin` |
| `IBusinessService` | `BusinessService` | CRUD (create/delete admin-only, update admin-or-own-manager) + `AssignManagerAsync` (admin-only; unassigns the manager from wherever they were previously assigned, then assigns them here, inside one `SaveChangesAsync` — a `DbUpdateException` from the unique `ManagerId` index racing a concurrent assignment is caught and reported as "already assigned elsewhere") |
| `IBusinessTypeService` / `IPackageTypeService` | `BusinessTypeService` / `PackageTypeService` | Thin pass-throughs — lookup data has no business rules to enforce |
| `IFavoriteService` | `FavoriteService` | Customer-only, always scoped to the signed-in user — no "favorite on behalf of" path exists. `ToggleFavoriteAsync` returns the new state so the caller doesn't need a second read |
| `INotificationService` | `NotificationService` | Scoped to the signed-in user for every read method; `CreateAsync(userId, message, url?)` is the one exception — other services call it to notify *someone else* (e.g. `OrderService` notifying a business's manager about a new order) |
| `IPackageService` | `PackageService` | CRUD, admin-or-own-business-manager on writes (`EnsureCanManageBusinessAsync`); `GetByIdsAsync` is the batch path `CartService` uses on reconnect. `AddAsync`/`UpdateAsync` also notify (bell + email) every customer who's favorited the package's business when it's created in stock or restocked from `0` — see §3 Package |
| `IReviewService` | `ReviewService` | `GetContextAsync(businessId)` → `ReviewContext(CanReview, MyReview)` — `CanReview` is true once the customer has a completed order with the business; `SubmitAsync` updates an existing review in place if one exists, otherwise inserts |
| `IUserService` | `UserService` | Admin-only (`EnsureAdminAsync` on every method — enforced via `CurrentUserAccessor`, not an `[Authorize]` HTTP filter, since this is only ever called in-process; see §6). `UpdateRoleAsync` refuses to demote the platform's last remaining admin, and auto-releases whatever business a user managed if they're moved away from `BusinessManager` |
| `IOrderService` | `OrderService` | The biggest service — see the deep dive below |

### CurrentUserAccessor

```csharp
public class CurrentUserAccessor(AuthenticationStateProvider authenticationStateProvider)
{
    public async Task<(bool IsAdmin, string? UserId)> GetCurrentUserAsync();
    public async Task<bool> IsInRoleAsync(string role);
}
```
Not behind an interface — it's a small, concrete helper every other service takes a direct dependency on to read "who is the caller and what role are they in" without depending on `HttpContext` (which Blazor Server's persistent SignalR-circuit model doesn't reliably expose the way a per-request HTTP pipeline does). Because it wraps `AuthenticationStateProvider.GetAuthenticationStateAsync()`, **it only works from inside a component's Blazor circuit** — calling it (directly or transitively, through any service that depends on it) from a genuine HTTP request throws `InvalidOperationException: Do not call GetAuthenticationStateAsync outside of the DI scope for a Razor component`. This bit `OrderExportController` during development (a real HTTP endpoint that briefly tried to call `IOrderService.GetOrdersInRangeAsync`) — see §6 for how that controller works around it instead of fighting it.

### OrderService — deep dive

`OrderService` owns every rule around placing, confirming, completing, cancelling, marking-no-show, and reporting on orders. Its constructor pulls in `IOrderRepository`, `IPackageRepository`, `IBusinessService`, `INotificationService`, `IAppEmailSender`, the raw `EcoMealDbContext` (for ad-hoc queries that don't warrant a repository method), and `CurrentUserAccessor`.

**`PlaceOrderAsync(businessId, lines)`** — customer-only:
1. Resolves the caller via `CurrentUserAccessor`; throws `UnauthorizedAccessException` if not signed in or not a `Customer`.
2. **Rate limit**: counts the caller's own orders with `CreatedAt >= UtcNow - OrderRateLimit.Window` (10 minutes); at `OrderRateLimit.MaxOrdersPerWindow` (5) or more, throws `InvalidOperationException` naming the limit and window — surfaced to the customer as a red banner in the cart panel, not a silent failure.
3. For each requested line, loads the `Package` and checks `line.Quantity <= package.Quantity - pendingElsewhere`, where `pendingElsewhere` is the sum of **other customers'** `Pending`-order quantities against that same package:
   ```csharp
   var pendingElsewhere = await dbContext.OrderPackages
       .Where(op => op.PackageId == package.Id && op.Order.Status.Name == OrderStatuses.Pending)
       .SumAsync(op => (int?)op.Quantity) ?? 0;
   ```
   This is the mechanism that prevents overselling **before** confirmation — `Package.Quantity` itself only drops once an order is actually confirmed (step below), so without this check two customers could both "successfully" place a Pending order for the last unit.

   The same reservation math is also exposed **read-only** for the storefront's "X left" display — `GetPendingReservedQuantitiesAsync(packageIds)` (`IOrderService`/`IOrderRepository`, no auth check, same public audience as browsing packages) bulk-sums Pending quantity per package for a batch of IDs. This exists because the display-side quantity used to only subtract the *viewer's own local cart* (`CartService.AvailableQuantity`), so a package correctly blocked from overselling server-side would still show its full, pre-reservation count to every other browser as soon as the reserving customer's cart cleared (e.g. right after they placed the order) — see `FRONTEND_ARCHITECTURE.md` §15.
4. Inserts the `Order` (status `Pending`, `OrderNumber` left for the DB sequence) with its `OrderPackage` rows, and notifies the business's manager (if assigned) of a new order needing confirmation.

**`ApplyStatusChangeAsync(order, statusName)`** — the single choke point shared by manager/admin status changes (`UpdateStatusAsync`) *and* customer self-cancellation (`CancelMyOrderAsync`), so the transition rules and stock adjustments can't drift out of sync between the two call paths:
```csharp
var allowedTransition = (currentStatusName, statusName) switch
{
    (Pending, Confirmed)   => true,
    (Pending, Cancelled)   => true,
    (Confirmed, Completed) => true,
    (Confirmed, Cancelled) => true,
    (Confirmed, NoShow)    => true,
    _ => false,
};
```
- **Pending → Confirmed**: re-checks every line's requested quantity against the package's *current* `Quantity` (not the pending-reservation snapshot from placement — stock may have moved since), then decrements it. A concurrent confirm of the same package by another manager races on the `xmin` row-version and surfaces as "Stock for this order just changed — please refresh and try again" (`DbUpdateConcurrencyException` → `InvalidOperationException`).
- **Confirmed → Cancelled / Confirmed → NoShow**: both restore each line's quantity back onto the package (the reverse of the confirm-time decrement) — the reservation is being released either way, whether the manager cancelled it or the pickup window just closed unclaimed. Pending → Cancelled needs **no** stock restoration, since a Pending order never touched `Package.Quantity` in the first place — it only ever affected the `pendingElsewhere` reservation math above.
- Every successful transition fires a customer-facing notification (`Confirmed` → "show your QR code at pickup" linking to the pickup pass; `Completed` → thank-you; `Cancelled` → plain cancellation notice; `NoShow` → missed-pickup notice), each also best-effort emailed via `IAppEmailSender` (`SendCustomerEmailAsync`, wrapped so a failed/unconfigured send never breaks the transition itself).
- `NoShow` is reachable two ways: a manager marking it by hand from `/orders/manage` (this transition), or automatically once the pickup window fully closes — see `ExpireNoShowOrdersAsync` below.

**`ExpireStalePendingOrdersAsync()`** — no current-user check (it's a system-triggered call from `OrderLifecycleSweepService`, not something a Razor page invokes). Cancels any `Pending` order whose `CreatedAt` is older than `OrderExpiry.PendingTimeout` (30 min) **or** whose earliest package's `PickupEnd` has already passed, notifying (bell + email) the customer either way. No stock restoration needed, for the same reason as the Pending→Cancelled case above.

**`ExpireNoShowOrdersAsync()`** — also system-triggered. Finds `Confirmed` orders where *every* line's `Package.PickupEnd` has passed (`IOrderRepository.GetOverduePickupOrdersAsync`) and moves them to `NoShow`, restoring stock exactly like a manual `Confirmed → NoShow` would. A single stale line isn't enough — an order spanning packages with different windows only counts once the last one closes.

**`SendPickupRemindersAsync()`** — also system-triggered. Finds `Confirmed` orders whose latest-closing line falls within `OrderExpiry.PickupReminderLeadTime` (30 min) of closing, that haven't already been reminded (`Order.PickupReminderSentAt is null`) and haven't fully closed yet (`IOrderRepository.GetPickupReminderCandidatesAsync`), notifies (bell + email) the customer, and stamps `PickupReminderSentAt` so the next sweep tick doesn't remind them again.

**`GetOrdersInRangeAsync(from?, to?, businessId?)`** — unpaginated, date-bounded, scoped the same way `GetOrdersForManagementPagedAsync` is: admins see everything (optionally filtered to one business via `businessId`), managers are always pinned to their own business regardless of what `businessId` they pass. Backs both the CSV export and the dashboard trend chart — see §6 for why the CSV export controller can't call this method directly despite it living on the same interface.

**`GetTotalKgSavedAsync()`** — the one genuinely public read (no auth check at all): sums `Quantity * WeightKg` across every `OrderPackage` on a `Completed` order, platform-wide. Backs the anonymous home-hero stat.

### Reorder note

Re-adding a past order's items to the cart (`Orders.razor`'s "Order again") is implemented entirely in the **frontend** — it iterates the already-loaded `Order.OrderPackages`, skips any package that's no longer live or in stock, and calls the existing `CartService.AddAsync` per line (which itself clamps quantity to what's actually available). There's no `OrderService.ReorderAsync` — the feature needed no new backend surface, just client-side orchestration over methods that already existed. See `FRONTEND_ARCHITECTURE.md` for the full flow.

---

## 6. Controllers — the In-Process Façade Pattern

This is the single most distinctive architectural choice in the backend. Every controller in `Controllers/` is decorated `[ApiController] [Route("/")]`, but **none of their action methods carry `[HttpGet]`/`[HttpPost]` attributes** — which means `app.MapControllers()` never actually maps most of them to a reachable route. Instead, each controller class is *also* registered directly in DI:

```csharp
builder.Services.AddControllers();
builder.Services.AddScoped<BusinessController>();
builder.Services.AddScoped<PackageController>();
builder.Services.AddScoped<UserController>();
builder.Services.AddScoped<OrderController>();
builder.Services.AddScoped<ReviewController>();
builder.Services.AddScoped<NotificationController>();
builder.Services.AddScoped<FavoriteController>();
```

Razor components `@inject` these the same way they'd inject any other service, and call their methods as plain in-process C# calls — no HTTP round-trip, no JSON (de)serialization, no separate DTO layer:

```csharp
@inject OrderController OrderController
...
var result = await OrderController.PlaceOrderAsync(businessId, lines);
if (result.Result is ConflictObjectResult conflict) { ... }
```

**Why go through a "controller" at all, instead of injecting `IOrderService` directly?** The `ActionResult<T>` return type and `Ok()`/`Conflict()`/`NotFound()`/`Unauthorized()` helper methods give every Razor page a uniform, already-familiar way to distinguish "success" from "the service threw `UnauthorizedAccessException`" from "the service threw `InvalidOperationException`" — each controller action wraps its service call in a `try/catch` and maps exceptions to the matching `ActionResult`, so pages check `result.Result is ConflictObjectResult` instead of wrapping every call site in its own `try/catch`. It's a deliberate reuse of ASP.NET MVC's result-typing conventions purely for their ergonomics, decoupled from HTTP entirely.

### The two real exceptions

Two controllers genuinely are HTTP endpoints, and both are structured differently as a result:

| Controller | Route | Why it's real HTTP |
|---|---|---|
| `AuthController` | `[Route("api/[controller]")]` — `[ManualValidateAntiforgeryToken]` on `LoginAsync`/`RegisterAsync` only, **not** `LogoutAsync` (see §7) | Login/Register/Logout are plain `<form method="post">` submissions from `Login.razor`/`Register.razor`/the logout button — a real page navigation is required so the ASP.NET Identity auth cookie actually gets set on the response. `[ManualValidateAntiforgeryTokenAttribute]` (see §7) validates the antiforgery token these forms carry, since this API-only project doesn't register the MVC view-engine services `[AutoValidateAntiforgeryToken]` needs. `AuthController` also carries three plain in-process methods with no HTTP attribute at all — `ConfirmEmailAsync`, `RequestPasswordResetAsync`, `ResetPasswordAsync` — injected directly into `ConfirmEmail.razor`/`ForgotPassword.razor`/`ResetPassword.razor` the same way `OrderController` etc. are injected everywhere else, since none of those three touch the auth cookie and so don't need the real-HTTP round trip |
| `OrderExportController` | `[Route("api/orders")]`, `[HttpGet("export")]` | The CSV download link (`OrderManagement.razor`) is a plain `<a href="/api/orders/export">` — the browser needs to treat the response as a file, which only works over a genuine HTTP GET |

`OrderExportController` can't use `IOrderService` the way every in-process controller does, because there's no Blazor circuit backing a standalone HTTP GET request — and `IOrderService`'s authorization checks all go through `CurrentUserAccessor`, which needs one (see the `CurrentUserAccessor` note in §5). Instead, it resolves identity straight from `HttpContext.User` (available on any real HTTP request via `[Authorize]`) and talks to `IOrderRepository`/`IBusinessService` directly:

```csharp
[ApiController]
[Route("api/orders")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.BusinessManager}")]
public class OrderExportController(IOrderRepository orderRepository, IBusinessService businessService) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> ExportCsvAsync(DateTime? from, DateTime? to, Guid? businessId = null)
    {
        Guid? effectiveBusinessId;
        if (User.IsInRole(AppRoles.Admin))
        {
            effectiveBusinessId = businessId;
        }
        else
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var business = userId is null ? null : await businessService.GetByManagerIdAsync(userId);
            if (business is null) return Unauthorized();
            effectiveBusinessId = business.Id;
        }

        var orders = await orderRepository.GetInRangeAsync(effectiveBusinessId, from, to);
        return File(Encoding.UTF8.GetBytes(BuildCsv(orders)), "text/csv", $"orders-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
```
Note `IBusinessService.GetByManagerIdAsync` is safe to call here — it's a pure read with no `CurrentUserAccessor` dependency of its own, unlike most of `BusinessService`'s write methods.

### Controller surface reference

| Controller | Methods |
|---|---|
| `BusinessController` | `GetAllAsync`, `GetPagedAsync`, `GetByIdAsync`, `AddAsync` → `Created()`, `UpdateAsync` → `NoContent()`, `DeleteAsync` → `NoContent()`, `AssignManagerAsync` → `NoContent()`/`Conflict()` |
| `PackageController` | `GetAllAsync`, `GetPagedAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync` |
| `OrderController` | `PlaceOrderAsync`, `GetMyOrdersAsync`, `GetOrdersForManagementAsync`, `GetMyOrdersPagedAsync`, `GetOrdersForManagementPagedAsync`, `GetOrdersInRangeAsync`, `UpdateOrderStatusAsync`, `CancelMyOrderAsync`, `GetTotalKgSavedAsync`, `GetMyOrderAsync`, `GetOrderForManagementAsync`, `GetPendingReservedQuantitiesAsync` — every mutating/ownership-sensitive method wraps the service call in `try/catch (UnauthorizedAccessException or InvalidOperationException) → Conflict(ex.Message)`, or `Unauthorized()`/`NotFound()` for the read-only ownership checks. `GetPendingReservedQuantitiesAsync` is the one method with no auth check at all — same public audience as package browsing (see §5) |
| `ReviewController` | `GetAllAsync`, `GetByBusinessAsync`, `GetByBusinessesAsync`, `GetContextAsync`, `SubmitAsync` |
| `FavoriteController` | `GetMyFavoriteBusinessIdsAsync`, `ToggleFavoriteAsync` |
| `NotificationController` | `GetMyNotificationsAsync`, `GetMyUnreadCountAsync`, `MarkAsReadAsync`, `MarkAllAsReadAsync` |
| `UserController` | `GetAllAsync`, `GetByRoleAsync`, `GetPagedAsync`, `UpdateRoleAsync` |
| `AuthController` *(real HTTP)* | `POST /api/auth/login`, `POST /api/auth/register`, `POST /api/auth/logout` — all `LocalRedirect` responses, never JSON. Plus the three in-process-only methods noted above |
| `OrderExportController` *(real HTTP)* | `GET /api/orders/export?from=&to=&businessId=` → CSV file download |

---

## 7. Authentication & Authorization

### Identity setup (`Program.cs`)

```csharp
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = builder.Configuration.GetValue("Identity:RequireConfirmedAccount", false);
    options.Password.RequiredLength = 8;
}).AddEntityFrameworkStores<EcoMealDbContext>().AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
});
```
Cookie-based auth, not JWT — a natural fit for Blazor Server, where the same persistent SignalR connection serves the whole session rather than a stateless token per request. `RequireConfirmedAccount` defaults to `false` (a bare `dotnet run` works with no SMTP set up at all — `AuthService.RegisterAsync` sets `EmailConfirmed = true` at creation time in that case, same as before). Flip `Identity:RequireConfirmedAccount` to `true` (as `docker-compose.test.yml` does, paired with the bundled Mailpit container — see §9/§10) and self-registration instead emails a confirmation link (`AuthService.SendConfirmationEmailAsync` → `ConfirmEmail.razor`) before `PasswordSignInAsync` will succeed. `ForgotPassword.razor`/`ResetPassword.razor` (`AuthService.RequestPasswordResetAsync`/`ResetPasswordAsync`) work either way, independent of this flag — Identity's password-reset token doesn't care whether the account is confirmed.

### Roles

Three fixed roles in `Constants.AppRoles`: `Admin`, `Customer`, `BusinessManager`. Seeded once by `DbSeeder` (`RoleManager<IdentityRole>.CreateAsync` per role if it doesn't already exist). Self-registration (`AuthService.RegisterAsync`) always lands a new account in `Customer` — the only way to become a `BusinessManager` or `Admin` is for an existing admin to promote them via `/users` (`UserService.UpdateRoleAsync`), which additionally:
- Refuses to demote the platform's **last remaining admin** (counts `GetUsersInRoleAsync(Admin)` before allowing a role change away from it).
- Auto-releases whatever business a user managed (`BusinessService.AssignManagerAsync(businessId, null)`) if they're moved to any role other than `BusinessManager` — otherwise a demoted manager would keep a phantom `Business.ManagerId` pointing at an account that can no longer act on it.

### Antiforgery

Blazor's own interactive forms (`EditForm` on the admin CRUD pages) get antiforgery handling for free from `AddRazorComponents()`. The pages that instead use a **plain HTML `<form method="post">`** (`Login.razor`, `Register.razor`, and the logout buttons in both layouts) need it validated manually, because `[AutoValidateAntiforgeryToken]` requires MVC's view-engine services (`AddControllersWithViews`/`AddMvc`), which this API-only project never registers:

```csharp
public class ManualValidateAntiforgeryTokenAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException) { context.Result = new BadRequestResult(); return; }
        await next();
    }
}
```
It validates the same token directly against `IAntiforgery`, which `AddRazorComponents` already wires up for Blazor's own forms — so both form styles end up protected by the identical underlying token, just checked through two different code paths.

**Only `LoginAsync`/`RegisterAsync` carry the attribute — `LogoutAsync` deliberately doesn't.** Forging either a login or a registration against a victim (login/registration CSRF) has real impact, so those stay protected. A forged logout's worst case is just logging the victim out — no state an attacker benefits from — so it isn't worth protecting, and for good reason: `<AntiforgeryToken/>` needs a real per-request `HttpContext` to mint a token against, which it doesn't have when the header's Sign Out form re-renders through the Blazor Router's client-side `NotFoundPage` fallback (no fresh HTTP request backs that render, just an in-circuit component swap). Concretely, that meant any signed-in user who landed on a 404 — a stale bookmark, a typo, a dead link — and then clicked Sign Out got a raw `400 Bad Request` instead of logging out, because the form's hidden token field rendered empty. Requiring the token on logout was pure downside with no corresponding security benefit here, so the attribute was removed from that one action instead of chasing the render-timing issue.

### Authorization patterns

| Pattern | Where | Effect |
|---|---|---|
| `[Authorize(Roles = AppRoles.Customer)]` | Orders, pickup pass pages | Customer-only |
| `[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.BusinessManager}")]` | Businesses, Packages, OrderManagement, OrderScan, OrderValidate, Dashboard | Either management role |
| `[Authorize(Roles = AppRoles.Admin)]` | Users | Admin-only |
| No page-level attribute + service-side `CurrentUserAccessor` check | Every in-process controller/service | The real enforcement point for most rules — see §6 |
| `AuthorizeRouteView` in `Routes.razor` | Global | Distinguishes "authenticated but wrong role" (→ `ForbiddenPanel`) from "not authenticated at all" (→ `RedirectToLogin`, forced full page load since no circuit exists yet) — see `FRONTEND_ARCHITECTURE.md` |

Because most authorization actually lives in the service layer (via `CurrentUserAccessor`) rather than in `[Authorize]` attributes on HTTP endpoints that don't really exist, the `[Authorize]` attributes on Razor `@page` components are the **first** line of defense (keeps an unauthorized user from even rendering the page) and the service-layer checks are the **real** one (keeps a crafted or stale client-side call from mutating something it shouldn't, regardless of what page rendered it).

---

## 8. Background Services

### OrderLifecycleSweepService

```csharp
public class OrderLifecycleSweepService(IServiceScopeFactory scopeFactory, ILogger<OrderLifecycleSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(OrderExpiry.SweepInterval);
        do
        {
            using var scope = scopeFactory.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            await orderService.ExpireStalePendingOrdersAsync();
            await orderService.SendPickupRemindersAsync();
            await orderService.ExpireNoShowOrdersAsync();
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
```
Registered via `builder.Services.AddHostedService<OrderLifecycleSweepService>()` (renamed from `PendingOrderExpiryService` when the reminder/no-show sweeps were added — one periodic pass over every time-based order transition, rather than three separate `BackgroundService`s each paying for their own timer and DI scope). Runs every `OrderExpiry.SweepInterval` (5 minutes) and does three things in order:
1. **Stale-Pending expiry** — cancels any `Pending` order idle longer than `OrderExpiry.PendingTimeout` (30 minutes) or whose pickup window has already closed. Exists to fix a real phantom-stock-lock: a `Pending` order that's never confirmed still counts against a package's availability via the `pendingElsewhere` check in `OrderService.PlaceOrderAsync` (§5) — without this sweep, an abandoned checkout could tie up stock indefinitely.
2. **Pickup reminders** — emails/notifies `Confirmed` orders closing within `OrderExpiry.PickupReminderLeadTime` (30 minutes) that haven't been reminded yet.
3. **No-show detection** — moves `Confirmed` orders whose pickup window has fully closed to `NoShow`, restoring stock.

`BackgroundService` instances are effectively singletons, so it can't hold a `Scoped` `IOrderService` directly — it creates a fresh DI scope on every tick via `IServiceScopeFactory` instead, exactly the pattern any singleton needing scoped dependencies must use. The whole tick body is one `try/catch`, logged and swallowed on failure so a bad tick doesn't take the loop down — the next `PeriodicTimer` tick just tries again.

---

## 9. Database Seeding

`DbSeeder.SeedAsync(services, configuration)` runs once at startup, right after `dbContext.Database.MigrateAsync()` in `Program.cs`. It's designed to be **safely re-run on every single startup**, not a one-time migration-adjacent script:

1. **Roles** — creates any of `AppRoles.AllRoles` that don't already exist.
2. **Seed admin** — reads `SeedAdmin:Email`/`SeedAdmin:Password` from configuration; if either is missing, logs a warning and skips (no admin account is seeded, the app still runs); if the account doesn't already exist, creates it and adds it to the `Admin` role.
3. **Lookup tables** (`BusinessTypes`, `PackageTypes`, `Statuses`) — insert-only. `BusinessTypes`/`PackageTypes` are skipped entirely once the table has any rows (`AnyAsync()` guard). `Statuses` instead adds whichever of the five fixed names are missing, because a database that's run the old pre-`DbSeeder` migrations already has the original four `Status` rows from a hardcoded `InsertData` by the time this runs — a blanket `AnyAsync()` guard there would've silently skipped seeding `NoShow` forever (this is exactly the migration-vs-seeder class of bug `Tests/Database/DbSeederTests.cs` exists to catch — see §11).
4. **Demo businesses & packages** — a fixed set of World-Cup-themed Timișoara businesses/packages with **hardcoded GUIDs**, reconciled against what's already in the DB rather than blindly re-inserted:
   - Missing seed rows are added.
   - An existing row's `ImageUrl` is only overwritten if it's currently blank or points at a retired placeholder host (`picsum.photos`) — `IsStalePlaceholderImage` — so an admin's own custom image is never clobbered by a re-seed.
   - A package's `PickupStart`/`PickupEnd` are only refreshed (advanced to "today") if the existing window has **already expired** — so the storefront always opens with live, orderable packages on any given day, without resetting `Quantity` (which reflects real orders placed against it) or touching a still-valid future window.
   - `WeightKg` and `DietaryTags` are **backfill-only** — only set if currently `0`/empty — since the seeder can't distinguish "never set" from "a manager deliberately cleared it," so it defaults to filling in demo data either way rather than guessing.
5. **Demo customer/manager activity** (`SeedDemoActivityAsync`) — creates `demo.customer@ecomeal.local`/`demo.manager@ecomeal.local` (fixed passwords, not configuration-gated like the admin account, since they carry no real data), assigns the demo manager to Stadionul de Gusturi, and — **only on a genuinely fresh database** (`if (await db.Orders.AnyAsync()) return;`, unlike the reconcile-on-every-run steps above) — creates seven orders spanning every status (`Pending`/`Confirmed`/`Completed`×3/`Cancelled`/`NoShow`) across the last 14 days plus favorites, reviews, and notifications. This exists so every feature (dashboard trend chart, CSV export, reorder, the notification bell, QR pickup, reviews, the `NoShow` badge) has real data to look at immediately after a fresh `docker compose up`, without manually clicking through the app first. Because it's gated on "no orders exist yet" rather than reconciled like steps 3–4, it never touches orders placed by real usage afterward.

This reconciliation approach — add-if-missing, refresh-if-stale, backfill-if-empty, never overwrite a live/customized value — is what lets the exact same seeder run unconditionally on every container start (see `docker-compose.test.yml`) without ever fighting real usage data.

---

## 10. Configuration Reference

| Key | Source | Purpose |
|---|---|---|
| `ConnectionStrings:EcoMealContext` | user-secrets (dev) / `docker-compose.test.yml` env (`ConnectionStrings__EcoMealContext`) | Npgsql connection string |
| `SeedAdmin:Email` / `SeedAdmin:Password` | user-secrets / docker-compose env (`SeedAdmin__Email`/`SeedAdmin__Password`) | The one admin account `DbSeeder` creates if it doesn't already exist |
| `Identity:RequireConfirmedAccount` | user-secrets / docker-compose env (`Identity__RequireConfirmedAccount`) | Defaults `false`. When `true`, self-registration requires clicking an emailed confirmation link before sign-in works (§7) |
| `App:BaseUrl` | user-secrets / docker-compose env (`App__BaseUrl`) | Absolute origin (e.g. `http://localhost:8081`) used to build links in confirmation/reset emails — falls back to `http://localhost:8080` if unset |
| `Email:Smtp:Host` / `Port` / `Username` / `Password` / `EnableSsl` | user-secrets / docker-compose env (double-underscore form) | SMTP settings for `SmtpEmailSender`. Leaving `Host` unset makes it log the email instead of sending — no SMTP server is required to run the app |
| `Email:FromAddress` / `Email:FromName` | user-secrets / docker-compose env | The `From:` header on outgoing emails; defaults to `no-reply@ecomeal.local` / `Eco Meal` |
| `Logging:LogLevel` | `appsettings.json` | Standard ASP.NET Core logging config; `Microsoft.AspNetCore` pinned to `Warning` to keep request-pipeline noise out of the console in Development |

No `appsettings.Production.json` exists — the only environment-specific file is `appsettings.Development.json`, which layers in the seed-admin credentials for local dev so a fresh `dotnet run` against an empty DB has a working login without extra setup. `docker-compose.test.yml` is explicitly documented (README) as a local test/demo harness, not a production deployment (fixed DB password, HTTP only) — it also bundles a `mailpit` container and points `Email:Smtp:Host` at it, so every email the app sends is visible at `http://localhost:8025` with zero real SMTP setup.

`Program.cs` also sets a fixed `CultureInfo("ro-RO")` as both `DefaultThreadCurrentCulture` and `DefaultThreadCurrentUICulture` at startup — every `ToString("C")` call across the app (cart totals, package prices, order totals) formats as RON without any per-call culture handling, since the app has exactly one supported locale.

---

## 11. Automated Tests

`Tests/Netrom-Eco-Meal.Tests.csproj` is a separate xUnit project referencing the main project (`Netrom-Eco-Meal.csproj` excludes `Tests/**` from its own item globs — see the `<Compile Remove>` in the `.csproj` — since the Web SDK's default globbing would otherwise also pull the test project's generated `obj/` files into the main build). Run everything with `dotnet test` from the repo root, or `dotnet test Netrom-Eco-Meal.slnx`.

Two kinds of tests, deliberately using different EF Core providers for different reasons:

- **`Services/OrderServiceTests.cs`** — unit tests for `OrderService`'s status-transition/stock logic (rate limiting, the pending-reservation math in `PlaceOrderAsync`, confirm/cancel/no-show stock reservation and restoration, illegal-transition rejection, manager/admin/customer authorization scoping, the pickup-reminder and no-show sweep methods). `IOrderRepository`/`IPackageRepository`/`IBusinessService`/`INotificationService`/`IAppEmailSender` are mocked with Moq; `EcoMealDbContext` is real but backed by the EF Core **InMemory** provider (`Tests/TestSupport/InMemoryDb.cs`), since `OrderService` queries it directly for a few things (rate-limit counts, status lookups, pending-reservation sums) that are simple enough for InMemory to translate correctly. `CurrentUserAccessor` is also real, constructed around a `FakeAuthenticationStateProvider` test double instead of mocking the concrete class.
- **`Database/DbSeederTests.cs`** — integration tests that run `DbSeeder.SeedAsync` against a **real Postgres** container (`Testcontainers.PostgreSql`, `Tests/TestSupport/PostgresFixture.cs`), applying real EF migrations first via `MigrateAsync()` — exactly what `Program.cs` does on startup. This is deliberate, not incidental: an InMemory-provider test wouldn't replay real migration history, so it can't catch the class of bug this project has hit before (see §9 point 4's history and the old `SeedData`/`MoreSeedData` migrations vs. `DbSeeder` conflict) — only a real migration run proves the current seed data actually wins. One Postgres container is shared per test class; each test gets its own logical database on it (`CreateDatabaseAsync`) for isolation without paying container-startup cost per test. Requires Docker to be running locally.

Given this split, Postgres-only query behavior (`EF.Functions.ILike` in `OrderRepository`, the `xmin` optimistic-concurrency token on `Package`, the `order_numbers` sequence) is exercised by the seeding integration tests' real Postgres round-trip, not by the InMemory-backed unit tests.
