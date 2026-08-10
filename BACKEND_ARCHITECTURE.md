# Backend Architecture — Netrom Eco Meal

**Stack:** ASP.NET Core 10 · Blazor Server (interactive server render mode) · Entity Framework Core · PostgreSQL (Npgsql) · ASP.NET Identity (cookie auth) · QRCoder · Stripe Checkout (`Stripe.net`)
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
│   ├── Payments/                 # CheckoutService, StripeGateway — see §5
│   └── *.cs                      # Business logic, DI-registered Scoped (2 are BackgroundService/plain classes — see §5)
├── Controllers/                 # ApiController classes — mostly in-process façades, see §6
├── Constants/                   # Fixed string values, enums-as-strings, small pure helpers
├── Models/                      # PaginatedList<T>, Debouncer, GeoDistance, BusinessHoursStatus (Debouncer is frontend-facing, see FRONTEND_ARCHITECTURE.md)
├── Migrations/                  # EF-generated migration history
├── Components/                  # Blazor UI — see FRONTEND_ARCHITECTURE.md
├── wwwroot/                     # Static assets — see FRONTEND_ARCHITECTURE.md
└── Tests/                       # Netrom-Eco-Meal.Tests — xUnit, see §11
```

`Program.cs` wires every layer with plain `AddScoped<TInterface, TImplementation>()` calls — no assembly scanning, no MediatR, no separate composition-root project.

Logging goes through Serilog rather than the default `Microsoft.Extensions.Logging` console provider: a minimal bootstrap logger (`Log.Logger = new LoggerConfiguration()...CreateBootstrapLogger()`) covers anything that fails before the host itself is up, then `builder.Host.UseSerilog(...)` replaces it with the real configuration-driven one (`ReadFrom.Configuration` — see §10 — plus `FromLogContext`/`WithMachineName`/`WithEnvironmentName`/`WithThreadId` enrichers). `app.UseSerilogRequestLogging()` is the first middleware in the pipeline, so it wraps and times every request including the `UseExceptionHandler("/Error")` re-execution for unhandled exceptions further down. `DbSeeder`'s own `ILoggerFactory`-sourced logger and every ASP.NET Core/EF Core framework log line flow through the same sinks — there's no second logging pipeline to keep in sync.

---

## 2. Project Layers

### 2.1 Entities

Plain EF Core entity classes, no DTOs anywhere in the app — Razor components bind directly to entities (see [FRONTEND_ARCHITECTURE.md §11](FRONTEND_ARCHITECTURE.md) for the two `EditForm` pages that use a private nested `FormModel` instead, purely for `[Required]`/`[Range]` validation attributes that don't belong on the persisted entity).

### 2.2 Database

| File | Role |
|------|------|
| `EcoMealDbContext.cs` | `IdentityDbContext<ApplicationUser>` — `DbSet<T>` for `Business`, `BusinessType`, `Order`, `OrderPackage`, `Package`, `PackageType`, `Status`, `Review`, `Notification`, `Favorite`, `PackageTemplate`, `Payment`, `PendingCheckout`, `AuditLog`, `Report`; all Fluent API config lives inline in `OnModelCreating` (no separate `Configurations/` folder — the model is small enough that splitting it out would be pure ceremony) |
| `DbSeeder.cs` | Static `SeedAsync(services, configuration)` called once from `Program.cs` after `MigrateAsync()` — see [§9](#9-database-seeding) |
| `Migrations/` | Notable ones: `AddOrderNumberSequenceAndPackageConcurrency` (the `order_numbers` DB sequence + `xmin` row-version column), `RemoveStalePackageSeedData` / `RemoveStaleBusinessSeedData` (cleaned up seed rows from an earlier, cruder seeding approach before `DbSeeder` existed), `AddPackageWeightKg`, `AddNotificationsFavoritesOrderCreatedAtPackageDietaryTags` (one migration bundling four Phase 2 features together), `AddGeolocationAndPackageTemplates` (`Business.Latitude`/`Longitude`, the `PackageTemplates` table, `Package.TemplateId`) |

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
  │      │  │          │
  │      │  └──── Payment  (0..1 — set once Stripe Checkout confirms payment)
  │      └──< OrderPackage >──── Package ──── PackageType
  │                                  │
  │                                  └──── PackageTemplate  (0..1, TemplateId — the template that
  │                                                           generated this instance, if any)
  │
  ├──< Favorite >──── Business        (one per (UserId, BusinessId), unique index)
  ├──< Review >────── Business        (one per (BusinessId, UserId), unique index)
  ├──< Notification
  └──< PendingCheckout ──── Business  (no FK/nav to Order — bridges checkout to Stripe; see §3 PendingCheckout)

Business ──< BusinessStaff >── ApplicationUser  (many-to-many: staff a business, unique on (BusinessId, UserId))
Business ──< BusinessHours            (0..7 rows, one per DayOfWeek, unique on (BusinessId, DayOfWeek))
Business ──< BusinessClosure          (0..N holiday date-range overrides)
Order ──── Status                     (Pending | Confirmed | Completed | Cancelled | NoShow)
PackageTemplate ──< Package           (0..N generated instances, one per calendar day)

AuditLog                              (no FK/nav to its target — polymorphic Business/Package/User, denormalized TargetName)
Report ──── ApplicationUser (Reporter)  (TargetId/TargetType also polymorphic, no FK to the target)
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
    public double? Latitude { get; set; }          // optional: powers "near me" sort and the map view
    public double? Longitude { get; set; }
    public string Status { get; set; } = BusinessStatuses.Approved;  // PendingApproval | Approved | Rejected
    public string? RejectionReason { get; set; }
    public bool IsHidden { get; set; }              // moderation flag, orthogonal to Status
    public string? HiddenReason { get; set; }
    public string? SubmittedByUserId { get; set; }  // set only for self-service applications
    public Guid BusinessTypeId { get; set; }
    public BusinessType BusinessType { get; set; } = null!;
    public ICollection<BusinessStaff> Staff { get; set; } = [];
    public ICollection<Package> Packages { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
    public ICollection<Favorite> Favorites { get; set; } = [];
    public ICollection<BusinessHours> Hours { get; set; } = [];
    public ICollection<BusinessClosure> Closures { get; set; } = [];
}
```
`Staff` is the many-to-many side of `BusinessStaff` (below) — a business can have several staff, and a single staff member can be staff of more than one business. There's no cap and no "primary" staffer; `IBusinessService.IsStaffAsync` is the one authorization check every write path (`BusinessService`, `PackageService`, `PackageTemplateService`, `OrderService`) uses to ask "can this user act on this business."

`Hours`/`Closures` back the weekly-schedule + holiday-closure feature (below) — both loaded via `AsSplitQuery()` on every `BusinessRepository.GetAllAsync`/`GetPagedAsync`/`GetByIdAsync` call alongside `Staff`, since three separate `Include`d collections on one query would otherwise multiply rows together (the "cartesian explosion" EF Core's `MultipleCollectionIncludeWarning` warns about for this shape — caught in practice via Serilog's request-scoped EF Core warning logging, see §10).

`Latitude`/`Longitude` are both nullable — set by hand or via a browser-geolocation "use my location" button on `BusinessForm.razor`, read by `BusinessRepository.GetPagedAsync`'s `BusinessSortOptions.Distance` sort and `Home.razor`'s map view (see the Pagination Helper note in §4).

`Status`/`RejectionReason`/`SubmittedByUserId` back the Phase 9 self-service approval workflow — an admin-created business (`BusinessService.AddAsync`) is always born `Approved`; a customer/manager self-service application (`BusinessService.ApplyAsync`, `/businesses/apply`) is born `PendingApproval` with `SubmittedByUserId` set to the applicant, and only an admin can move it to `Approved`/`Rejected` (`ApproveAsync`/`RejectAsync`). `IsHidden`/`HiddenReason` are a separate moderation flag an admin can toggle on any `Approved` business independent of its approval status (`HideAsync`/`UnhideAsync`) — a business can only be publicly visible to customers when `Status == Approved && !IsHidden` (`BusinessRepository.GetPagedAsync`/`GetAllAsync`'s `publicOnly` parameter; `Home.razor` and `BusinessDetail.razor` are the only callers that pass it — see §5/§9).

#### BusinessStaff
```csharp
public class BusinessStaff
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public DateTime AssignedAt { get; set; }
}
```
The join table behind `Business.Staff` — replaced an earlier `Business.ManagerId` nullable-FK design that capped a manager at one business. Unique index on `(BusinessId, UserId)` (`EcoMealDbContext.OnModelCreating`) so the same pairing can't be added twice; both FKs cascade-delete, so removing a business or a user account cleans up their staff rows instead of leaving orphans or blocking the delete. Admin-only to add/remove (`Businesses.razor`'s and `Users.razor`'s staff chip UI, both calling `BusinessController.AddStaffAsync`/`RemoveStaffAsync` — see §11 in `FRONTEND_ARCHITECTURE.md`).

#### BusinessHours / BusinessClosure
```csharp
public class BusinessHours
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required DayOfWeek DayOfWeek { get; set; }
    public bool IsClosed { get; set; }
    public TimeOnly? OpenTime { get; set; }   // null when IsClosed
    public TimeOnly? CloseTime { get; set; }  // null when IsClosed
    public Business Business { get; set; } = null!;
}

public class BusinessClosure
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required DateOnly StartDate { get; set; }
    public required DateOnly EndDate { get; set; }   // inclusive
    public string? Reason { get; set; }
    public Business Business { get; set; } = null!;
}
```
`BusinessHours` is a fixed weekly schedule — up to one row per `DayOfWeek` (unique index on `(BusinessId, DayOfWeek)`), always written as a complete replacement of the week rather than a per-day upsert: `IBusinessService.SetHoursAsync` → `IBusinessRepository.SetHoursAsync` deletes every existing row for the business and re-inserts whatever list it's given. A business with zero `BusinessHours` rows means "hours never configured" — distinct from every day being marked `IsClosed`, and treated as "unknown" (not "closed") by the open/closed calculation below. `BusinessClosure` is the opposite shape: an open-ended list of independent date ranges a manager adds/removes one at a time (`AddClosureAsync`/`RemoveClosureAsync`), each overriding `BusinessHours` for its `[StartDate, EndDate]` window regardless of what that weekday's hours say — used for vacations/one-off closures rather than the recurring weekly pattern.

Both are cascade-deleted with their `Business` (`EcoMealDbContext.OnModelCreating`). Authorization mirrors `UpdateAsync`: admin or one of the business's own staff (`BusinessService.EnsureStaffOrAdminAsync`, factored out of the staff-or-admin check `UpdateAsync` already had).

`Models.BusinessHoursStatus` is the pure open/closed calculation both `Home.razor`'s card badge and `BusinessDetail.razor`'s hours panel call — `IsOpenNow(hours, closures, localNow)` returns `null` (unknown, hide the indicator) when `hours` is empty, `false` when an active `BusinessClosure` covers `localNow`'s date or today's `BusinessHours` row is missing/closed/outside its open–close window, `true` otherwise. It takes the already-loaded collections rather than a `Business`/DbContext, so it's covered by plain unit tests (`Tests/Models/BusinessHoursStatusTests.cs`) with no database involved — including the overnight-window case (`CloseTime < OpenTime`, e.g. 22:00–02:00) where a naive `>= open && < close` check would wrongly read "closed" for the stretch after midnight. `localNow` is the viewer's browser-local time via `ClientTimeZoneService`, same convention `PickupLabel` already uses for pickup windows — this app has no per-business timezone field, so a business's hours are assumed to mean the same local time as everything else it shows.

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
    public Guid? TemplateId { get; set; }           // set when a recurring template generated this instance
    public bool IsHidden { get; set; }              // moderation flag — hides from the storefront without deleting
    public string? HiddenReason { get; set; }
    public Business Business { get; set; } = null!;
    public PackageType PackageType { get; set; } = null!;
    public PackageTemplate? Template { get; set; }
    public ICollection<OrderPackage> OrderPackages { get; set; } = [];
}
```
Restocking a package from `0` to a positive `Quantity` (or publishing a brand-new one) notifies everyone who's favorited that business — see `PackageService` in §5; there's no per-package "notify me" subscription, so Favorites doubles as the closest proxy.

`Quantity` carries an EF Core **shadow property row-version** — `modelBuilder.Entity<Package>().Property<uint>("xmin").IsRowVersion()` maps Postgres's native `xmin` system column as an optimistic-concurrency token, with zero extra columns to migrate. Two managers confirming orders against the same package's last unit at the same time get a `DbUpdateConcurrencyException` on the loser, translated by `OrderService` into "Stock for this order just changed — please refresh and try again" instead of silently overselling. `DietaryTags` is stored as a plain `List<string>` — EF Core 8+ maps this to a Postgres `text[]` column with no extra configuration needed.

`TemplateId` is nullable and `OnDelete(DeleteBehavior.SetNull)` — deleting the owning `PackageTemplate` (via `/packages/templates`' "stop repeating") unlinks any instances it already generated instead of deleting them, since they're real packages that may already have orders against them. `Packages.razor` shows a 🔁 "Daily" badge on any row with `TemplateId` set.

`IsHidden`/`HiddenReason` are the package-level counterpart to `Business.IsHidden` (Phase 9 moderation) — an admin or the package's own business staff can toggle it (`PackageService.HideAsync`/`UnhideAsync`, same `EnsureCanManageBusinessAsync` authorization as every other write on this entity) to pull a specific package off the storefront (e.g. in response to a `Report`) without touching the rest of the business. `Home.razor`/`BusinessDetail.razor` filter it out of the live package lists alongside the existing `PickupEnd > now` check.

#### PackageTemplate
```csharp
public class PackageTemplate
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required Guid PackageTypeId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required decimal Price { get; set; }
    public required int Quantity { get; set; }       // restocked to this amount on every generated instance
    public required decimal WeightKg { get; set; }
    public List<string> DietaryTags { get; set; } = [];
    public required TimeSpan PickupStartTimeUtc { get; set; }  // daily window as UTC time-of-day
    public required TimeSpan PickupEndTimeUtc { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly? LastGeneratedDate { get; set; } // guards one generation per calendar day
    public Business Business { get; set; } = null!;
    public PackageType PackageType { get; set; } = null!;
    public ICollection<Package> GeneratedPackages { get; set; } = [];
}
```
Backs the "repeat this every day" checkbox on `PackageForm.razor` — ticking it when creating a package (create-only, not available on edit) calls `PackageTemplateService.CreateFromPackageAsync`, which copies the just-created package's fields into a new template, derives `PickupStartTimeUtc`/`PickupEndTimeUtc` from that package's `PickupStart`/`PickupEnd` time-of-day, and links the two via `Package.TemplateId`. `PickupEndTimeUtc <= PickupStartTimeUtc` means the window crosses midnight (end falls the next day) — handled by `PackageTemplateService.GenerateDueInstancesAsync` (§8) when combining the stored time-of-day with a calendar date.

`LastGeneratedDate` is what makes generation idempotent regardless of the background sweep's cadence — a template only ever produces one `Package` per UTC calendar day, tracked here rather than derived by querying for an existing instance. `IsActive` is the pause/resume toggle on `/packages/templates`; a paused template stops generating but its already-created instances are untouched. Managed with the same admin-or-own-business-manager authorization as `Package` itself (`PackageTemplateService.EnsureCanManageBusinessAsync`, identical shape to `PackageService`'s).

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
    public Payment? Payment { get; set; }           // null until CheckoutService confirms the Stripe payment
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

An `Order` now only ever exists once its Stripe Checkout payment is confirmed — see `PendingCheckout` and `Payment` below, and `CheckoutService` in §5, for the bridge that makes that true.

#### Payment
```csharp
public class Payment
{
    public Guid Id { get; set; }
    public required Guid OrderId { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }      // lowercase ISO code, as Stripe returns it (e.g. "ron")
    public required string StripeCheckoutSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public required string Status { get; set; }         // see Constants.PaymentStatuses: Succeeded | Refunded | RefundFailed
    public DateTime CreatedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public Order Order { get; set; } = null!;
}
```
One row per `Order`, created by `CheckoutService.CompleteCheckoutAsync` the moment Stripe confirms a session as paid — `modelBuilder.Entity<Payment>().HasOne(p => p.Order).WithOne(o => o.Payment).HasForeignKey<Payment>(p => p.OrderId)` makes this a true 1:1, matching Stripe Checkout being one session per order, never split. Never deleted: `OrderService.RefundIfPaidAsync` (§5) flips `Status` to `Refunded` and stamps `RefundedAt` on cancellation instead of removing the row, or to `RefundFailed` (no `RefundedAt`) if the Stripe call itself errors, and deliberately leaves it `Succeeded` on a `NoShow` — the un-reversed charge **is** the no-show fee (FEATURE_IDEAS.md's Phase 7), with no separate fee-specific code needed. `Constants.PaymentStatuses.Label`/`BadgeClass`/`IconClass` are the single place all three statuses map to display text/color/icon, so every payment badge across the app (`Orders.razor`, `OrderDetailModal`, `OrderManagement.razor`, `Payments.razor`) renders `RefundFailed` consistently instead of it silently reading as `Paid`. `/payments` (`Payments.razor`, `FRONTEND_ARCHITECTURE.md` §11) is the manager/admin-facing ledger over this table, reusing `OrderController.GetOrdersForManagementPagedAsync`'s already-`Payment`-included query rather than a dedicated read path.

#### PendingCheckout
```csharp
public class PendingCheckout
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    public required string LinesJson { get; set; }      // serialized List<OrderLineRequest> — the cart at checkout time
    public string? StripeCheckoutSessionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }            // set once CompleteCheckoutAsync has resolved this
    public Guid? ResultingOrderId { get; set; }           // null if the paid-for order couldn't be placed and was refunded instead
}
```
Bridges "customer clicked Pay" to "the `Order` actually exists." `CheckoutService.StartCheckoutAsync` parks the cart's lines here and sends the customer to Stripe's hosted Checkout page *before* any `Order` (and its stock reservation) is created — `CompleteCheckoutAsync`, called from the Stripe success-redirect landing page (`PaymentReturn.razor`), re-validates the Stripe session, only then calls `IOrderService.PlaceOrderAsync`, and stamps `ConsumedAt`/`ResultingOrderId`. No FK/navigation to `Order` — deliberately loose, since a `PendingCheckout` can resolve to no order at all (payment succeeded but stock vanished in the meantime — see §5's `CompleteCheckoutAsync` walkthrough). `ConsumedAt` being non-null is what makes completion idempotent: a page refresh on the Stripe return URL replays `CompleteCheckoutAsync` against the same row instead of double-spending one Stripe payment into two orders. Abandoned rows (`ConsumedAt` still null past `OrderExpiry.PendingCheckoutTimeout`) are hard-deleted by `OrderLifecycleSweepService` (§8) — nothing was ever charged, so there's nothing to refund, just tidying.

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

#### AuditLog
```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public required string ActorUserId { get; set; }
    public required string ActorName { get; set; }   // denormalized at write time
    public required string Action { get; set; }       // see Constants.AuditActions
    public required string TargetType { get; set; }   // see Constants.AuditTargetTypes
    public string? TargetId { get; set; }
    public required string TargetName { get; set; }   // denormalized so the log stays readable after a rename/delete
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
```
Phase 9's trust-and-safety record — who promoted/demoted a user, who created/edited/deleted/staffed a business, who approved/rejected/hid/unhid a business or package, who dismissed/actioned a report. Deliberately has **no** FK/navigation to its target: `TargetType`/`TargetId` are polymorphic (a `User`, `Business`, or `Package`), so `TargetName` is captured as plain text at write time instead of joined at read time — a renamed or deleted target doesn't retroactively make an old log entry unreadable. Written exclusively by `IAuditLogService.LogAsync`, called from *inside* `BusinessService`/`PackageService`/`UserService`/`ReportService` after a mutation has already succeeded (never exposed on a controller as a standalone write) — this is what guarantees an entry always reflects something that actually happened, rather than being a value a client could fabricate. `LogAsync` resolves `ActorUserId`/`ActorName` itself via `CurrentUserAccessor` + `UserManager<ApplicationUser>`, so callers only ever pass the action/target. The read side (`GetPagedAsync`, admin-only) backs `/audit-log` (`AuditLog.razor`). Indexed on `CreatedAt` — newest-first is the only sort.

#### Report
```csharp
public class Report
{
    public Guid Id { get; set; }
    public required string ReporterUserId { get; set; }
    public ApplicationUser Reporter { get; set; } = null!;
    public required string TargetType { get; set; }   // Business or Package — Constants.AuditTargetTypes
    public required Guid TargetId { get; set; }
    public required string Reason { get; set; }
    public required string Status { get; set; }        // see Constants.ReportStatuses
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
}
```
A customer-submitted flag on a business or package (the "report" half of Phase 9's "hide/report flow instead of only hard delete"). `TargetId`/`TargetType` are polymorphic like `AuditLog`'s, so there's no FK to the target — `ReportService` resolves the target's current name at read time via `IBusinessService.GetByIdAsync`/`IPackageService.GetByIdAsync` instead (small, admin-only "open reports" list, so the extra lookups are cheap). `SubmitAsync` is open to any signed-in user and needs no authorization beyond that; `DismissAsync`/`TakeActionAsync`/`GetOpenAsync` are admin-only. `TakeActionAsync` doesn't hide the target itself — it delegates to `IBusinessService.HideAsync`/`IPackageService.HideAsync` (using the report's own `Reason` as the hide reason) and lets those log their own `AuditLog` entry, then logs a second `ReportActionTaken` entry of its own, so the audit trail shows both "why the target was hidden" and "which report drove it." Indexed on `Status`, since "open reports" is the only query the admin UI ever runs.

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
| `IBusinessRepository` / `BusinessRepository` | `GetAllAsync`, `GetPagedAsync(search, businessTypeId, staffUserId?, sortBy?, favoritedByUserId?, customerLat?, customerLng?)`, `GetByIdAsync`, `GetByStaffUserIdAsync`, `GetStaffAsync`, `IsStaffAsync`, `AddStaffAsync`, `RemoveStaffAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | `GetPagedAsync`'s search also matches **live packages'** name/description (`b.Packages.Any(p => p.PickupEnd > now && ...)`), so searching "bread" surfaces a bakery even if its own name/description doesn't mention bread. `sortBy: "closingSoon"` orders by each business's nearest live `PickupEnd` (`?? DateTime.MaxValue` so businesses with nothing live sort last regardless of mode). `sortBy: "distance"` (`BusinessSortOptions.Distance`) is the one mode EF/SQL can't express — see the Pagination Helper note below. `AddStaffAsync`/`RemoveStaffAsync` return `bool` (false on a duplicate pair or a no-op removal) instead of throwing, so the caller can turn that into a `Conflict()`/`NotFound()` without a `try/catch` |
| `IPackageTemplateRepository` / `PackageTemplateRepository` | `GetAllAsync`, `GetByBusinessIdAsync`, `GetActiveAsync`, `GetByIdAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | `GetActiveAsync` is the one method with no navigation-property includes — it's the batch load `PackageTemplateGenerationService` (§8) uses every sweep tick, where `Business`/`PackageType` details aren't needed |
| `IBusinessTypeRepository` / `BusinessTypeRepository` | `GetAllAsync` | Read-only lookup, no writes anywhere in the app |
| `IFavoriteRepository` / `FavoriteRepository` | `GetFavoriteBusinessIdsAsync`, `IsFavoriteAsync`, `AddAsync`, `RemoveAsync`, `GetFavoritingUsersAsync` | `AddAsync`/`RemoveAsync` persist immediately — the one repository that breaks the stage-then-save convention (see §3 Favorite). `GetFavoritingUsersAsync` feeds `PackageService`'s back-in-stock notifications |
| `INotificationRepository` / `NotificationRepository` | `GetRecentByUserIdAsync`, `GetUnreadCountAsync`, `MarkAsReadAsync`, `MarkAllAsReadAsync`, `CreateAsync` | Takes `IDbContextFactory<EcoMealDbContext>`, not the circuit-scoped `EcoMealDbContext` — every method opens and disposes its own short-lived context. This is what lets `NotificationPanelState`'s 30-second background poll (see FRONTEND_ARCHITECTURE.md §5) query the DB without racing whatever query the routed page is running against the shared per-circuit context at the same moment. `MarkAsReadAsync`/`MarkAllAsReadAsync` use `ExecuteUpdateAsync` — a single `UPDATE ... WHERE` round-trip, no load-then-save |
| `IOrderRepository` / `OrderRepository` | `GetAllAsync`, `GetByUserIdAsync`, `GetByBusinessIdAsync`, `GetPagedByUserIdAsync`, `GetPagedForManagementAsync(search, businessId?, status?)`, `GetInRangeAsync(businessId?, from?, to?)`, `GetByIdAsync`, `HasCompletedOrderAsync`, `GetTotalWeightSavedKgAsync`, `GetStalePendingOrdersAsync`, `GetOverduePickupOrdersAsync`, `GetPickupReminderCandidatesAsync`, `GetPendingQuantitiesByPackageIdsAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | The busiest repository — see `OrderService` in §5 for how its methods compose. `GetPagedForManagementAsync`'s order-number search strips a leading `#` and substring-matches the plain integer (`EF.Functions.ILike(o.OrderNumber.ToString(), ...)`) because Npgsql can't translate the zero-padded `ToString("000")` overload into SQL — it also strips any leading zeros left after the `#`, so searching the padded number shown on screen (`#008`) still matches. `GetInRangeAsync` is unpaginated and date-bounded — it feeds both the CSV export and the dashboard trend chart (see §6 and FRONTEND_ARCHITECTURE.md §11). `GetPendingQuantitiesByPackageIdsAsync` is a `GroupBy`/`Sum` over `OrderPackages` for a batch of package IDs — the same Pending-reservation shape `PlaceOrderAsync`'s `pendingElsewhere` check computes per-package, exposed in bulk for display (§5) |
| `IPackageRepository` / `PackageRepository` | `GetAllAsync`, `GetPagedAsync(search, businessId?, packageTypeId?)`, `GetByIdAsync`, `GetByIdsAsync`, `GetForAnalyticsAsync(businessId?, since)`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | `GetByIdsAsync` is the batch-load path `PlaceOrderAsync` uses to re-validate every package referenced in the submitted cart lines in one query — the bulk-action toolbar (§5) reuses it too, to load every selected package before duplicating/adjusting/extending. `GetForAnalyticsAsync` includes each package's `OrderPackages`/`Order`/`Status` graph so the Dashboard can aggregate sell-through and pickup-hour stats without a second round-trip. `CartService` restores its own localStorage-persisted cart directly against `EcoMealDbContext` via `IDbContextFactory`, not through this repository — see §5 |
| `IPackageTypeRepository` / `PackageTypeRepository` | `GetAllAsync` | Read-only lookup |
| `IReviewRepository` / `ReviewRepository` | `GetAllAsync`, `GetByBusinessIdAsync`, `GetByBusinessIdsAsync`, `GetByUserAndBusinessAsync`, `AddAsync`, `SaveChangesAsync` | `GetByBusinessIdsAsync` is the batch path `Home.razor` uses to load ratings for an entire page of business cards in one query instead of one query per card |
| `IAuditLogRepository` / `AuditLogRepository` | `AddAsync`, `GetPagedAsync(action?, targetType?, search?)` | Plain injected `EcoMealDbContext`, not a factory — writes are infrequent admin actions, no polling concern like `NotificationRepository`. `search` matches `ActorName`/`TargetName` via `EF.Functions.ILike` |
| `IReportRepository` / `ReportRepository` | `AddAsync`, `GetByIdAsync`, `GetByStatusAsync`, `SaveChangesAsync` | `GetByIdAsync`/`GetByStatusAsync` both `.Include(r => r.Reporter)` — the one navigation this entity actually has, since `TargetType`/`TargetId` are polymorphic with no FK to `Include` |

### DI Registration (Program.cs)

```csharp
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IBusinessTypeRepository, BusinessTypeRepository>();
builder.Services.AddScoped<IPackageRepository, PackageRepository>();
builder.Services.AddScoped<IPackageTypeRepository, PackageTypeRepository>();
builder.Services.AddScoped<IPackageTemplateRepository, PackageTemplateRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
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

    // Synchronous — for sorts EF/SQL can't express at all, not just ones that need a different shape.
    public static PaginatedList<T> Create(List<T> orderedSource, int pageIndex, int pageSize);
}
```
One `CountAsync` + one `Skip/Take` `ToListAsync`. The second overload exists specifically for `UserService.GetPagedAsync` — its query is a `join` producing an anonymous type (EF can't `OrderBy` a query already projected through a `record` constructor), so it orders/pages the anonymous shape and maps to `UserWithRole` only after materializing.

The third, synchronous `Create` overload backs `BusinessRepository.GetPagedAsync`'s `BusinessSortOptions.Distance` sort — Haversine distance to a runtime (customer) point isn't something Npgsql translates to SQL, so that one sort mode materializes the filtered-but-unsorted `IQueryable` in full, orders it in memory via `Models.GeoDistance.Km` (plain lat/lng trig, no external dependency), then hands the already-ordered `List<Business>` to this method for the same `Skip/Take` page-slicing every other sort gets. Fine at this dataset's size; would need revisiting (e.g. a PostGIS extension) at real scale.

---

## 5. Service Layer

Every service is `Scoped`. A few break that pattern deliberately: `OrderLifecycleSweepService` and `PackageTemplateGenerationService` are `BackgroundService`s (effectively singletons — see §8); `CartService`/`ClientTimeZoneService`/`ManagedBusinessContext` are `Scoped` but have no backing interface or repository — they're pure in-memory, per-circuit state with a JS-interop side door (documented in `FRONTEND_ARCHITECTURE.md` since they're really frontend concerns that happen to live in `Services/` alongside everything else); `SmtpEmailSender` (`IAppEmailSender`) is stateless and could be `Singleton` but is registered `Scoped` for consistency with everything else.

| Service Interface | Implementation | Responsibilities |
|---|---|---|
| `IAuthService` | `AuthService` | Thin wrapper over ASP.NET Identity's `SignInManager`/`UserManager` — `LoginAsync`, `RegisterAsync` (self-registration always lands in the `Customer` role; only an admin can promote from there), `LogoutAsync`, plus `ConfirmEmailAsync`/`RequestPasswordResetAsync`/`ResetPasswordAsync` for the email-confirmation and password-reset flows `Identity:RequireConfirmedAccount` unlocks (see §7/§10). Builds absolute links for those emails off `App:BaseUrl`, since neither a background sweep nor (reliably) a Blazor circuit has an `HttpContext` to derive one from |
| `IAppEmailSender` | `SmtpEmailSender` | One method, `SendEmailAsync(toEmail, subject, htmlBody)`, over `System.Net.Mail.SmtpClient`. Deliberately **not** ASP.NET Identity's `IEmailSender<TUser>` — this app has no Identity Razor Pages UI to trigger that interface, and order-lifecycle/back-in-stock emails aren't Identity's concern anyway. Reads `Email:Smtp:*`/`Email:From*` straight off `IConfiguration` (same style as `SeedAdmin:*`); when `Email:Smtp:Host` isn't set, it logs the email instead of sending — the same "optional infra, degrade gracefully" pattern `DbSeeder` uses for `SeedAdmin` |
| `IBusinessService` | `BusinessService` | CRUD (create/delete admin-only, update admin-or-own-staff, via `IsStaffAsync`) + `AddStaffAsync`/`RemoveStaffAsync` (admin-only; a `DbUpdateException` from the unique `(BusinessId, UserId)` index racing a concurrent add is caught and turned into a `false` return rather than propagating) + `GetByStaffUserIdAsync`/`GetStaffAsync`/`IsStaffAsync` (the read side other services and `ManagedBusinessContext` build on). **Phase 9**: `ApplyAsync` (any signed-in user — self-service business signup, born `PendingApproval`), `ApproveAsync`/`RejectAsync` (admin-only; `ApproveAsync` also allows re-approving a `Rejected` business, not just a `PendingApproval` one) and `HideAsync`/`UnhideAsync` (admin-only moderation, orthogonal to approval status) — all four log to `IAuditLogService` and notify (`INotificationService`) the affected submitter/staff. Every write method logs its own `AuditLog` entry (create/update/delete/staff-add/staff-remove/apply/approve/reject/hide/unhide) |
| `IBusinessTypeService` / `IPackageTypeService` | `BusinessTypeService` / `PackageTypeService` | Thin pass-throughs — lookup data has no business rules to enforce |
| `IFavoriteService` | `FavoriteService` | Customer-only, always scoped to the signed-in user — no "favorite on behalf of" path exists. `ToggleFavoriteAsync` returns the new state so the caller doesn't need a second read |
| `INotificationService` | `NotificationService` | Scoped to the signed-in user for every read method; `CreateAsync(userId, message, url?)` is the one exception — other services call it to notify *someone else* (e.g. `OrderService` notifying a business's manager about a new order) |
| `IPackageService` | `PackageService` | CRUD, admin-or-own-business-manager on writes (`EnsureCanManageBusinessAsync`). `AddAsync`/`UpdateAsync` also notify (bell + email) every customer who's favorited the package's business when it's created in stock or restocked from `0` — see §3 Package. `DuplicateManyAsync`/`AdjustQuantityManyAsync`/`ExtendPickupWindowManyAsync` back the `/packages` bulk-action toolbar (Phase 8), each authorizing every affected package's business via `EnsureCanManageBusinessesAsync` (a thin loop over the single-package check) before mutating. `GetForAnalyticsAsync(businessId?, since)` backs the Dashboard's Business Analytics card — admin-only for a `null` businessId, otherwise requires `IsStaffAsync` on the requested one, same scoping shape as `OrderService.ResolveManagerBusinessIdAsync`. **Phase 9**: `HideAsync`/`UnhideAsync` — same `EnsureCanManageBusinessAsync` authorization as every other write, so either an admin or the package's own business staff can moderate it; both log to `IAuditLogService` |
| `IPackageTemplateService` | `PackageTemplateService` | Same admin-or-own-business-manager authorization shape as `IPackageService`. `CreateFromPackageAsync`, `SetActiveAsync`, `DeleteAsync` are the manager-facing CRUD; `GenerateDueInstancesAsync` is the one method with **no** current-user check — system-triggered by `PackageTemplateGenerationService` (§8), same pattern as `OrderService`'s sweep methods |
| `IReviewService` | `ReviewService` | `GetContextAsync(businessId)` → `ReviewContext(CanReview, MyReview)` — `CanReview` is true once the customer has a completed order with the business; `SubmitAsync` updates an existing review in place if one exists, otherwise inserts |
| `IUserService` | `UserService` | Admin-only (`EnsureAdminAsync` on every method — enforced via `CurrentUserAccessor`, not an `[Authorize]` HTTP filter, since this is only ever called in-process; see §6). `UpdateRoleAsync` refuses to demote the platform's last remaining admin, and auto-releases whatever business a user managed if they're moved away from `BusinessManager`. Logs a `RoleChanged` `AuditLog` entry (old role → new role) on every successful change |
| `IAuditLogService` | `AuditLogService` | `LogAsync(action, targetType, targetId?, targetName, details?)` — called from *inside* `BusinessService`/`PackageService`/`UserService`/`ReportService` after a mutation succeeds, never exposed as a standalone write endpoint (see §3 AuditLog). Resolves the actor's name itself via `CurrentUserAccessor` + `UserManager<ApplicationUser>`. `GetPagedAsync` (admin-only) backs `/audit-log` |
| `IReportService` | `ReportService` | `SubmitAsync` — open to any signed-in user, no further authorization. `GetOpenAsync`/`DismissAsync`/`TakeActionAsync` — admin-only. `TakeActionAsync` delegates to `IBusinessService.HideAsync`/`IPackageService.HideAsync` (using the report's `Reason` as the hide reason) rather than mutating the target itself, so hiding always goes through the same authorized, audit-logged path regardless of whether it's triggered directly or via a report |
| `ICheckoutService` | `CheckoutService` (`Services/Payments/`) | Owns the "pay before the order exists" flow — see the deep dive below |
| `IStripeGateway` | `StripeGateway` (`Services/Payments/`) | Thin wrapper over the Stripe SDK (`CreateCheckoutSessionAsync`, `GetSessionStatusAsync`, `RefundAsync`). Kept separate from `ICheckoutService` so `OrderService` (needs to issue refunds) doesn't have to depend on the order-creation side of checkout, and `ICheckoutService` (needs to place orders) doesn't have to depend back on `OrderService`'s refund path — breaks what would otherwise be a circular DI dependency. Reads `Stripe:SecretKey`/`Stripe:Currency` straight off `IConfiguration`, same style as `SmtpEmailSender`'s `Email:Smtp:*`; `EnsureConfigured` turns a missing key into "payments aren't configured yet" instead of a raw Stripe SDK exception, same degrade-gracefully pattern `SmtpEmailSender` uses for a missing `Email:Smtp:Host` |
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

`OrderService` owns every rule around placing, confirming, completing, cancelling, marking-no-show, and reporting on orders. Its constructor pulls in `IOrderRepository`, `IPackageRepository`, `IBusinessService`, `INotificationService`, `IAppEmailSender`, `IStripeGateway` (refunds only — issuing a charge is `CheckoutService`'s job, not this one's), the raw `EcoMealDbContext` (for ad-hoc queries that don't warrant a repository method), and `CurrentUserAccessor`.

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
4. Inserts the `Order` (status `Pending`, `OrderNumber` left for the DB sequence) with its `OrderPackage` rows, and notifies every one of the business's staff (`IBusinessService.GetStaffAsync` — zero, one, or several) of a new order needing confirmation.

**Business-scoped reads/writes now take an explicit `businessId`.** Since a manager can staff more than one business (`BusinessStaff`), there's no longer a single implicit "their business" — `GetOrdersForManagementAsync`, `GetOrdersForManagementPagedAsync`, and `GetOrdersInRangeAsync` all take `Guid? businessId` from the caller. Two private helpers keep that resolution in one place: `GetOwnedOrderAsync(orderId)` (used by `UpdateStatusAsync`/`GetOrderForManagementAsync`) checks `IBusinessService.IsStaffAsync(order.BusinessId, userId)` against the order's own business; `ResolveManagerBusinessIdAsync(userId, requestedBusinessId)` (used by the three list methods above) requires the caller to pass a `businessId` and rejects it with `UnauthorizedAccessException` if the manager isn't staff there. Admins skip both checks — `businessId` is optional for them and just narrows an otherwise-unscoped read.

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
- **Refund on Cancelled, never on NoShow**: any transition to `Cancelled` (manual or the stale-Pending sweep) calls `RefundIfPaidAsync`, which looks up the order's `Succeeded` `Payment` and issues a real Stripe refund (`IStripeGateway.RefundAsync`) before flipping it to `Refunded`. `NoShow` deliberately skips this — the customer reserved/held the food and never collected it, so the un-reversed charge **is** the no-show fee, with no separate fee-specific code (FEATURE_IDEAS.md's Phase 7). A failed/unconfigured Stripe call here doesn't block the cancellation itself (same reasoning as `SendCustomerEmailAsync` below — the order still needs to come off the books either way), but unlike an email failure it isn't silently swallowed: `RefundIfPaidAsync` flips `Payment.Status` to `RefundFailed` and returns `true`, which `ApplyStatusChangeAsync` folds into the cancellation notification/email text ("We couldn't process your refund automatically — we'll follow up.") so it's never indistinguishable from a normal `Paid` order.
- Every successful transition fires a customer-facing notification (`Confirmed` → "show your QR code at pickup" linking to the pickup pass; `Completed` → thank-you; `Cancelled` → plain cancellation notice, extended as above on a failed refund; `NoShow` → missed-pickup notice), each also best-effort emailed via `IAppEmailSender` (`SendCustomerEmailAsync`, wrapped so a failed/unconfigured send never breaks the transition itself).
- `NoShow` is reachable two ways: a manager marking it by hand from `/orders/manage` (this transition), or automatically once the pickup window fully closes — see `ExpireNoShowOrdersAsync` below.

**`ExpireStalePendingOrdersAsync()`** — no current-user check (it's a system-triggered call from `OrderLifecycleSweepService`, not something a Razor page invokes). Cancels any `Pending` order whose `CreatedAt` is older than `OrderExpiry.PendingTimeout` (30 min) **or** whose earliest package's `PickupEnd` has already passed, notifying (bell + email) the customer either way. No stock restoration needed, for the same reason as the Pending→Cancelled case above.

**`ExpireNoShowOrdersAsync()`** — also system-triggered. Finds `Confirmed` orders where *every* line's `Package.PickupEnd` has passed (`IOrderRepository.GetOverduePickupOrdersAsync`) and moves them to `NoShow`, restoring stock exactly like a manual `Confirmed → NoShow` would. A single stale line isn't enough — an order spanning packages with different windows only counts once the last one closes.

**`SendPickupRemindersAsync()`** — also system-triggered. Finds `Confirmed` orders whose latest-closing line falls within `OrderExpiry.PickupReminderLeadTime` (30 min) of closing, that haven't already been reminded (`Order.PickupReminderSentAt is null`) and haven't fully closed yet (`IOrderRepository.GetPickupReminderCandidatesAsync`), notifies (bell + email) the customer, and stamps `PickupReminderSentAt` so the next sweep tick doesn't remind them again.

**`GetOrdersInRangeAsync(from?, to?, businessId?)`** — unpaginated, date-bounded, scoped the same way `GetOrdersForManagementPagedAsync` is via `ResolveManagerBusinessIdAsync` above: admins see everything (optionally filtered to one business via `businessId`), a manager must pass a `businessId` they're actually staff of. Backs both the CSV export and the dashboard trend chart — see §6 for why the CSV export controller can't call this method directly despite it living on the same interface.

**`GetTotalKgSavedAsync()`** — the one genuinely public read (no auth check at all): sums `Quantity * WeightKg` across every `OrderPackage` on a `Completed` order, platform-wide. Backs the anonymous home-hero stat.

### CheckoutService — deep dive

Owns the "pay before the order exists" flow, bridging `CartPanel.razor`'s Pay button to a real `Order` via Stripe Checkout (see `FRONTEND_ARCHITECTURE.md` §8 for the frontend side).

**`StartCheckoutAsync(businessId, lines)`** — customer-only:
1. Re-validates every requested line the same way `OrderService.PlaceOrderAsync` step 3 does (`package.Quantity - pendingElsewhere`) — a courtesy so nobody pays for stock that's already gone; `PlaceOrderAsync` re-checks this for real once payment actually succeeds, since stock can still move in the time the customer spends on Stripe's page.
2. Inserts a `PendingCheckout` row with the cart's lines serialized to JSON, then calls `IStripeGateway.CreateCheckoutSessionAsync` (one Stripe line item per cart line, `ClientReferenceId` set to the `PendingCheckout.Id`) with success/cancel URLs built off `App:BaseUrl` — `/checkout/return?pc={id}&session_id={CHECKOUT_SESSION_ID}` (Stripe substitutes that placeholder token itself) and `/checkout/cancel?pc={id}`.
3. Stamps the returned Stripe session ID back onto the `PendingCheckout` row and returns Stripe's hosted checkout URL for the frontend to redirect to.

**`CompleteCheckoutAsync(pendingCheckoutId, sessionId)`** — called from `PaymentReturn.razor` (`/checkout/return`) on the redirect back from Stripe:
1. Loads the `PendingCheckout`; throws `UnauthorizedAccessException` if it belongs to a different signed-in user.
2. **Idempotent by `ConsumedAt`**: if this checkout was already resolved (a page refresh replaying the return URL), it re-loads and returns the same `Order` instead of processing the payment twice — this is what stops one Stripe payment from ever becoming two orders.
3. Re-validates the passed `sessionId` matches the stored one, then asks Stripe for the session's real status (`IStripeGateway.GetSessionStatusAsync`) rather than trusting anything client-supplied — a `session_id` query param is just as spoofable as any other URL parameter.
4. Once Stripe confirms `IsPaid`, deserializes the parked cart lines and calls `IOrderService.PlaceOrderAsync` for real. **If that throws** (stock vanished, the rate limit was hit while the customer was on Stripe's page) the payment already went through, so it refunds via `IStripeGateway.RefundAsync` instead of keeping a charge for nothing, stamps `ConsumedAt` with no `ResultingOrderId`, and reports the refund to the customer.
5. On success, inserts the matching `Payment` row (`Status = Succeeded`) and stamps `PendingCheckout.ConsumedAt`/`ResultingOrderId`.

**`ExpireStalePendingCheckoutsAsync()`** — system-triggered, called by `OrderLifecycleSweepService` (§8). Hard-deletes any `PendingCheckout` still unconsumed past `OrderExpiry.PendingCheckoutTimeout` — nobody was ever charged for an abandoned Stripe session, so there's nothing to refund, just tidying up the bridge row.

### Reorder note

Re-adding a past order's items to the cart (`Orders.razor`'s "Order again") is implemented almost entirely in the **frontend** — it iterates the already-loaded `Order.OrderPackages`, skips any package that's no longer live or in stock, and calls the existing `CartService.AddAsync` per line (which itself clamps quantity to what's actually available). There's no `OrderService.ReorderAsync` — no new service method was needed, just client-side orchestration over methods that already existed. See `FRONTEND_ARCHITECTURE.md` for the full flow.

The one backend piece it did need: `OrdersWithIncludes()` (§4) originally included `OrderPackages.Package` but not `Package.Business`, so `CartService.AddInternal`'s `package.Business.Name` read (§5 CartService) could `NullReferenceException` on reorder — masked in ad-hoc testing because EF's change-tracker sometimes backfills `Package.Business` incidentally from the same query's `Order.Business` include, so it didn't reproduce every time. Fixed by adding `.ThenInclude(p => p.Business)` to the chain. Worth remembering: "no new backend surface" doesn't mean "the existing includes are definitely sufficient" — check what a reused entity's *nested* navigation properties actually need, not just whether the top-level query already exists.

---

## 6. Controllers — the In-Process Façade Pattern

This is the single most distinctive architectural choice in the backend. Every controller in `Controllers/` is decorated `[ApiController] [Route("/")]`, but **none of their action methods carry `[HttpGet]`/`[HttpPost]` attributes** — which means `app.MapControllers()` never actually maps most of them to a reachable route. Instead, each controller class is *also* registered directly in DI:

```csharp
builder.Services.AddControllers();
builder.Services.AddScoped<BusinessController>();
builder.Services.AddScoped<PackageController>();
builder.Services.AddScoped<PackageTemplateController>();
builder.Services.AddScoped<UserController>();
builder.Services.AddScoped<OrderController>();
builder.Services.AddScoped<ReviewController>();
builder.Services.AddScoped<NotificationController>();
builder.Services.AddScoped<FavoriteController>();
builder.Services.AddScoped<PaymentController>();
builder.Services.AddScoped<AuditLogController>();
builder.Services.AddScoped<ReportController>();
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
            var staffBusinesses = userId is null ? [] : await businessService.GetByStaffUserIdAsync(userId);
            if (staffBusinesses.Count == 0) return Unauthorized();

            if (businessId is not null)
            {
                if (staffBusinesses.All(b => b.Id != businessId)) return Forbid();
                effectiveBusinessId = businessId;
            }
            else if (staffBusinesses.Count == 1)
            {
                effectiveBusinessId = staffBusinesses[0].Id;
            }
            else
            {
                return BadRequest("You manage more than one business — specify businessId.");
            }
        }

        var orders = await orderRepository.GetInRangeAsync(effectiveBusinessId, from, to);
        return File(Encoding.UTF8.GetBytes(BuildCsv(orders)), "text/csv", $"orders-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
```
Note `IBusinessService.GetByStaffUserIdAsync` is safe to call here — it's a pure read with no `CurrentUserAccessor` dependency of its own, unlike most of `BusinessService`'s write methods. A manager staffing exactly one business gets it automatically; one staffing several must pass `businessId` explicitly (and can only pick one they're actually staff of) — the same shape `OrderService.ResolveManagerBusinessIdAsync` uses on the in-process side, reimplemented here rather than shared because this controller can't call into `IOrderService`/`CurrentUserAccessor` at all (see above).

### Controller surface reference

| Controller | Methods |
|---|---|
| `BusinessController` | `GetAllAsync(publicOnly?)`, `GetPagedAsync(..., statusFilter?, publicOnly?)`, `GetByIdAsync`, `GetStaffAsync`, `AddAsync` → `Created()`, `UpdateAsync` → `NoContent()`, `DeleteAsync` → `NoContent()`, `AddStaffAsync(businessId, userId, userName?)` → `NoContent()`/`Conflict()`, `RemoveStaffAsync(businessId, userId, userName?)` → `NoContent()`/`NotFound()`, `ApplyAsync` → the created `Business` or `Unauthorized()`, `ApproveAsync`/`RejectAsync`/`HideAsync`/`UnhideAsync` → `NoContent()` |
| `PackageController` | `GetAllAsync`, `GetPagedAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `DuplicateManyAsync`, `AdjustQuantityManyAsync`, `ExtendPickupWindowManyAsync`, `GetForAnalyticsAsync` → `Unauthorized()` on a non-admin's missing/foreign `businessId`, `HideAsync`/`UnhideAsync` → `NoContent()` |
| `PackageTemplateController` | `GetAllAsync`, `GetByBusinessIdAsync`, `CreateFromPackageAsync`, `SetActiveAsync`, `DeleteAsync` |
| `OrderController` | `PlaceOrderAsync`, `GetMyOrdersAsync`, `GetOrdersForManagementAsync`, `GetMyOrdersPagedAsync`, `GetOrdersForManagementPagedAsync`, `GetOrdersInRangeAsync`, `UpdateOrderStatusAsync`, `CancelMyOrderAsync`, `GetTotalKgSavedAsync`, `GetMyOrderAsync`, `GetOrderForManagementAsync`, `GetPendingReservedQuantitiesAsync` — every mutating/ownership-sensitive method wraps the service call in `try/catch (UnauthorizedAccessException or InvalidOperationException) → Conflict(ex.Message)`, or `Unauthorized()`/`NotFound()` for the read-only ownership checks. `GetPendingReservedQuantitiesAsync` is the one method with no auth check at all — same public audience as package browsing (see §5) |
| `ReviewController` | `GetAllAsync`, `GetByBusinessAsync`, `GetByBusinessesAsync`, `GetContextAsync`, `SubmitAsync` |
| `FavoriteController` | `GetMyFavoriteBusinessIdsAsync`, `ToggleFavoriteAsync` |
| `NotificationController` | `GetMyNotificationsAsync`, `GetMyUnreadCountAsync`, `MarkAsReadAsync`, `MarkAllAsReadAsync` |
| `UserController` | `GetAllAsync`, `GetByRoleAsync`, `GetPagedAsync`, `UpdateRoleAsync` |
| `PaymentController` | `CreateCheckoutSessionAsync` → the Stripe hosted-checkout URL or `Conflict()`, `CompleteCheckoutAsync` → `CheckoutCompletionResult` or `Unauthorized()` — thin wrappers over `ICheckoutService`, same try/catch-to-`ActionResult` shape as every other in-process controller. `/payments`' read-only ledger reuses `OrderController.GetOrdersForManagementPagedAsync` instead of adding a method here (§5 Payment, `FRONTEND_ARCHITECTURE.md` §11) |
| `AuditLogController` | `GetPagedAsync(action?, targetType?, search?)` → `Unauthorized()` for a non-admin |
| `ReportController` | `SubmitAsync(targetType, targetId, reason)` → `Created()`, `GetOpenAsync` → `Unauthorized()` for a non-admin, `DismissAsync`, `TakeActionAsync(reportId, actionReason)` → `NoContent()` |
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
- Auto-releases every business a user staffed (`GetByStaffUserIdAsync` + `RemoveStaffAsync` per business) if they're moved to any role other than `BusinessManager` — otherwise a demoted manager would keep phantom `BusinessStaff` rows pointing at an account that can no longer act on them.

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
| `[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.BusinessManager}")]` | Businesses, Packages, OrderManagement, Payments, OrderScan, OrderValidate, Dashboard | Either management role |
| `[Authorize(Roles = AppRoles.Admin)]` | Users, Reports, Audit Log | Admin-only |
| `[Authorize(Roles = $"{AppRoles.Customer},{AppRoles.BusinessManager}")]` | BusinessApply (`/businesses/apply`) | Self-service business signup — not Admin (they create directly via `/businesses/create`) |
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
            var checkoutService = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
            await checkoutService.ExpireStalePendingCheckoutsAsync();
            await orderService.ExpireStalePendingOrdersAsync();
            await orderService.SendPickupRemindersAsync();
            await orderService.ExpireNoShowOrdersAsync();
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
```
Registered via `builder.Services.AddHostedService<OrderLifecycleSweepService>()` (renamed from `PendingOrderExpiryService` when the reminder/no-show sweeps were added — one periodic pass over every time-based order transition, rather than several separate `BackgroundService`s each paying for their own timer and DI scope). Runs every `OrderExpiry.SweepInterval` (5 minutes) and does four things in order:
1. **Stale-checkout cleanup** — hard-deletes any `PendingCheckout` (§3) still unconsumed past `OrderExpiry.PendingCheckoutTimeout` (30 minutes): a customer who clicked Pay, then closed the tab on Stripe's page, was never charged, so this is pure tidying, not a refund path.
2. **Stale-Pending expiry** — cancels any `Pending` order idle longer than `OrderExpiry.PendingTimeout` (30 minutes) or whose pickup window has already closed, refunding it (`OrderService.RefundIfPaidAsync`, §5) since it *was* paid for at checkout time. Exists to fix a real phantom-stock-lock: a `Pending` order that's never confirmed still counts against a package's availability via the `pendingElsewhere` check in `OrderService.PlaceOrderAsync` (§5) — without this sweep, an abandoned checkout could tie up stock indefinitely.
3. **Pickup reminders** — emails/notifies `Confirmed` orders closing within `OrderExpiry.PickupReminderLeadTime` (30 minutes) that haven't been reminded yet.
4. **No-show detection** — moves `Confirmed` orders whose pickup window has fully closed to `NoShow`, restoring stock but deliberately *not* refunding (§3 Payment, §5).

`BackgroundService` instances are effectively singletons, so it can't hold a `Scoped` `IOrderService` directly — it creates a fresh DI scope on every tick via `IServiceScopeFactory` instead, exactly the pattern any singleton needing scoped dependencies must use. The whole tick body is one `try/catch`, logged and swallowed on failure so a bad tick doesn't take the loop down — the next `PeriodicTimer` tick just tries again.

### PackageTemplateGenerationService

Same shape as `OrderLifecycleSweepService` above — `IServiceScopeFactory` + `PeriodicTimer`, one `try/catch`-wrapped tick body, registered via `AddHostedService<PackageTemplateGenerationService>()`. Runs every `PackageTemplateGeneration.SweepInterval` (15 minutes) and calls the one system-triggered method on `IPackageTemplateService`:

```csharp
var generated = await templateService.GenerateDueInstancesAsync();
```
`GenerateDueInstancesAsync` loads every active template (`IPackageTemplateRepository.GetActiveAsync`) and, for any whose `LastGeneratedDate` isn't today (UTC), combines its `PickupStartTimeUtc`/`PickupEndTimeUtc` with today's date into a new `Package` — copying every other field straight from the template — and stamps `LastGeneratedDate`. If the combined window has already closed by the time a delayed sweep catches it (app downtime, etc.), it stamps the date without generating a dead-on-arrival package instead. Idempotent by construction: however often the sweep ticks, a template produces at most one instance per UTC calendar day.

---

## 9. Database Seeding

`DbSeeder.SeedAsync(services, configuration)` runs once at startup, right after `dbContext.Database.MigrateAsync()` in `Program.cs`. It's designed to be **safely re-run on every single startup**, not a one-time migration-adjacent script:

1. **Roles** — creates any of `AppRoles.AllRoles` that don't already exist.
2. **Seed admin & demo accounts** — reads `SeedAdmin:Email`/`SeedAdmin:Password` from configuration for the admin; if either is missing, logs a warning and skips (no admin account is seeded, the app still runs). Unconditionally creates `demo.customer@ecomeal.local`, `demo.manager@ecomeal.local`, and `demo.manager2@ecomeal.local` (fixed passwords, not configuration-gated like the admin account, since they carry no real data) — the second demo manager exists purely so a fresh database demonstrates both directions of the `BusinessStaff` many-to-many (point 5 below). All four use the same find-or-create-and-assign-role helper.
3. **Lookup tables** (`BusinessTypes`, `PackageTypes`, `Statuses`) — insert-only. `BusinessTypes`/`PackageTypes` are skipped entirely once the table has any rows (`AnyAsync()` guard). `Statuses` instead adds whichever of the five fixed names are missing, because a database that's run the old pre-`DbSeeder` migrations already has the original four `Status` rows from a hardcoded `InsertData` by the time this runs — a blanket `AnyAsync()` guard there would've silently skipped seeding `NoShow` forever (this is exactly the migration-vs-seeder class of bug `Tests/Database/DbSeederTests.cs` exists to catch — see §11).
4. **Demo businesses & packages** — a fixed set of World-Cup-themed Timișoara businesses/packages with **hardcoded GUIDs**, reconciled against what's already in the DB rather than blindly re-inserted:
   - Missing seed rows are added.
   - An existing row's `ImageUrl` is only overwritten if it's currently blank or points at a retired placeholder host (`picsum.photos`) — `IsStalePlaceholderImage` — so an admin's own custom image is never clobbered by a re-seed.
   - A package's `PickupStart`/`PickupEnd` are only refreshed (advanced to "today") if the existing window has **already expired** — so the storefront always opens with live, orderable packages on any given day, without resetting `Quantity` (which reflects real orders placed against it) or touching a still-valid future window. Skipped entirely once `TemplateId` is set (see point 6) — a template-owned package is left to expire naturally rather than fought over by two refresh mechanisms.
   - `WeightKg`, `DietaryTags`, and `Business.Latitude`/`Longitude` are **backfill-only** — only set if currently `0`/empty/`null` — since the seeder can't distinguish "never set" from "an admin/manager deliberately cleared it," so it defaults to filling in demo data either way rather than guessing.
5. **Demo business staff** (`SeedBusinessStaffAsync`) — staffs the demo managers across the demo businesses, reconciled by `(BusinessId, UserId)` pair like the steps above: the first demo manager staffs both Stadionul de Gusturi and VAR Bistro (one staffer, several businesses), and the second demo manager joins the first at Stadionul de Gusturi (several staff, one business) — a fresh database demonstrates both shapes of the many-to-many without an admin having to click through `/businesses` first.
6. **Demo recurring template** (`SeedPackageTemplateAsync`) — turns the demo-staffed business's "Golden Boot Surprise Bag" package into a `PackageTemplate` (fixed GUID, same idempotency check as the lookup tables) so `/packages/templates` and the 🔁 "Daily" badge aren't empty on a fresh database. Runs once; `PackageTemplateGenerationService` (§8) owns that package's future daily instances from there.
7. **Demo business hours & closures** (`SeedBusinessHoursAsync`, `SeedBusinessClosuresAsync`) — a full weekly `BusinessHours` schedule per demo business, varied by type (restaurants closed one weekday, bakeries/cafes mornings-to-evening, groceries long hours every day, food trucks evenings only), plus two `BusinessClosure` rows: one covering today (so the holiday-closure banner is visible immediately) and one starting a few weeks out (so removing a not-yet-active closure is demoable without it affecting "closed now"). Both gated on the relevant table being empty, so neither re-runs once seeded.
8. **Demo customer/manager activity** (`SeedDemoActivityAsync`) — **only on a genuinely fresh database** (`if (await db.Orders.AnyAsync()) return;`, unlike the reconcile-on-every-run steps above), creates seven orders spanning every status (`Pending`/`Confirmed`/`Completed`×3/`Cancelled`/`NoShow`) across the last 14 days for the demo customer/manager accounts from point 2, plus favorites, reviews, and notifications. Since every real `Order` now only exists once its Stripe payment is confirmed (§3), each seeded order also gets a matching `Payment` row — `Refunded` for the cancelled one (mirroring `OrderService.RefundIfPaidAsync`), left `Succeeded` for the no-show (that's what makes the no-show fee real, per FEATURE_IDEAS.md's Phase 7). This exists so every feature (dashboard trend chart, CSV export, reorder, the notification bell, QR pickup, reviews, the `NoShow` badge, the `/payments` ledger) has real data to look at immediately after a fresh `docker compose up`, without manually clicking through the app first. Because it's gated on "no orders exist yet" rather than reconciled like steps 3–5, it never touches orders placed by real usage afterward.

   The same method also adds 9 **historical** packages (fixed GUIDs, `PickupStart`/`PickupEnd` already in the past across the last several days/hours rather than "today") each with a `Completed` order for less than its full quantity, plus their own `Payment` rows — purely so the Phase 8 Business Analytics card has a non-trivial sell-through rate (~70%, not 0% or 100%) and a multi-bar hourly chart to show immediately. Unlike the packages from point 4, these never appear on the live storefront (their pickup window has already closed) — they only surface in order history and dashboard analytics.

9. **Phase 9 approval/moderation demo data** (`SeedApprovalDemoBusinessesAsync`, `SeedModerationDemoDataAsync`, `SeedReportsAndAuditLogAsync`) — so the approval queue, moderation state, `/reports`, and `/audit-log` aren't empty on a fresh database:
   - Two extra businesses with fixed GUIDs, submitted by the demo customer: "Golazo Grill" (`PendingApproval`) and "Offside Kitchen" (`Rejected`, with a `RejectionReason`) — reconciled like point 4 (idempotent add-if-missing by ID), not gated on "fresh database only."
   - One existing demo business ("Fan Zone Grill") and one existing demo package ("Red Card Pastry Box") are backfilled `IsHidden = true` with a `HiddenReason`, chosen specifically because neither is referenced by any order/favorite/review seed data elsewhere — so marking them hidden can't break another feature's demo data. Backfill-only (checked via `HiddenReason is null`), same reasoning as `WeightKg`/`DietaryTags` in point 4.
   - `SeedReportsAndAuditLogAsync` is gated on "no `Report` rows exist yet" (same fresh-database-only shape as point 8) and inserts four `Report`s (one of each: `Open`, two `ActionTaken`, one `Dismissed`) plus a matching, internally-consistent `AuditLog` history (`BusinessApplied` ×2, `BusinessRejected`, `BusinessStaffAdded` ×3 mirroring point 5's staffing, `PackageHidden`/`BusinessHidden`, `ReportActionTaken` ×2, `ReportDismissed`) — every entry corresponds to something this file actually seeded elsewhere, not synthetic filler. Actor defaults to the seeded admin account if one exists, falling back to the demo customer otherwise so the entries never end up with a dangling `ActorUserId`.

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
| `Stripe:SecretKey` | user-secrets / docker-compose env (`Stripe__SecretKey`) | A Stripe **test-mode** secret key (`sk_test_...`). Empty by default — `StripeGateway.EnsureConfigured` then turns any checkout attempt into "payments aren't configured yet" instead of an SDK exception, so the app still runs (browsing, orders history, everything but actually checking out) with zero Stripe setup |
| `Stripe:Currency` | user-secrets / docker-compose env (`Stripe__Currency`) | Lowercase ISO currency code passed to Stripe Checkout; defaults to `ron` |
| `Serilog:MinimumLevel:Default` / `:Override:*` | `appsettings.json` / `appsettings.{Environment}.json` | Read by `builder.Host.UseSerilog(...)`'s `ReadFrom.Configuration` in `Program.cs`. Base config sets `Default: Information`; `appsettings.Development.json` overrides it to `Debug`. Both override `Microsoft.AspNetCore`/`Microsoft.EntityFrameworkCore` down to `Warning` so framework noise doesn't drown out app-level logs |
| `Serilog:WriteTo` | `appsettings.json` | Sink list — a `Console` sink with a compact `[HH:mm:ss LVL] Message` template by default. Adding a file/aggregator sink is a config-only change (plus the matching `Serilog.Sinks.*` NuGet package), no code change needed |
| `Logging:LogLevel` | `appsettings.json` | Standard ASP.NET Core logging config; `Microsoft.AspNetCore` pinned to `Warning` to keep request-pipeline noise out of the console in Development |

No `appsettings.Production.json` exists — the only environment-specific file is `appsettings.Development.json`, which layers in the seed-admin credentials for local dev so a fresh `dotnet run` against an empty DB has a working login without extra setup. `docker-compose.test.yml` is explicitly documented (README) as a local test/demo harness, not a production deployment (fixed DB password, HTTP only) — it also bundles a `mailpit` container and points `Email:Smtp:Host` at it, so every email the app sends is visible at `http://localhost:8025` with zero real SMTP setup.

`Program.cs` also sets a fixed `CultureInfo("ro-RO")` as both `DefaultThreadCurrentCulture` and `DefaultThreadCurrentUICulture` at startup — every `ToString("C")` call across the app (cart totals, package prices, order totals) formats as RON without any per-call culture handling, since the app has exactly one supported locale.

---

## 11. Automated Tests

`Tests/Netrom-Eco-Meal.Tests.csproj` is a separate xUnit project referencing the main project (`Netrom-Eco-Meal.csproj` excludes `Tests/**` from its own item globs — see the `<Compile Remove>` in the `.csproj` — since the Web SDK's default globbing would otherwise also pull the test project's generated `obj/` files into the main build). Run everything with `dotnet test` from the repo root, or `dotnet test Netrom-Eco-Meal.slnx`.

Two kinds of tests, deliberately using different EF Core providers for different reasons:

- **`Services/OrderServiceTests.cs`** — unit tests for `OrderService`'s status-transition/stock logic (rate limiting, the pending-reservation math in `PlaceOrderAsync`, confirm/cancel/no-show stock reservation and restoration, illegal-transition rejection, manager/admin/customer authorization scoping, the pickup-reminder and no-show sweep methods, refund-on-cancel vs. kept-charge-on-no-show). `IOrderRepository`/`IPackageRepository`/`IBusinessService`/`INotificationService`/`IAppEmailSender`/`IStripeGateway` are mocked with Moq; `EcoMealDbContext` is real but backed by the EF Core **InMemory** provider (`Tests/TestSupport/InMemoryDb.cs`), since `OrderService` queries it directly for a few things (rate-limit counts, status lookups, pending-reservation sums) that are simple enough for InMemory to translate correctly. `CurrentUserAccessor` is also real, constructed around a `FakeAuthenticationStateProvider` test double instead of mocking the concrete class.
- **`Services/CheckoutServiceTests.cs`** — covers the "pay before the order exists" bridge (§5 CheckoutService deep dive): `PendingCheckout` bookkeeping, pre-checkout availability validation, and the refund-on-failure path when `PlaceOrderAsync` fails after Stripe already confirmed payment. `IStripeGateway` is mocked (`CheckoutService` never talks to real Stripe) and `IOrderService` is mocked too (never creates `Order`s directly) — so this test file is purely about `CheckoutService`'s own orchestration, not re-testing `OrderService`'s or `StripeGateway`'s own logic.
- **`Services/PackageServiceTests.cs`** — covers the Phase 8 additions: the bulk-action toolbar (`DuplicateManyAsync`/`AdjustQuantityManyAsync`/`ExtendPickupWindowManyAsync`, including that a manager not staffed to *every* affected package's business throws and saves nothing) and `GetForAnalyticsAsync`'s admin-vs-manager scoping, plus Phase 9's `HideAsync`/`UnhideAsync` authorization and state changes. `IPackageRepository`/`IBusinessService` are mocked, same shape as `BusinessServiceTests.cs`.
- **`Services/BusinessServiceTests.cs`** — also covers Phase 9's `ApplyAsync` (anonymous throws, signed-in sets `PendingApproval` + `SubmittedByUserId`), `ApproveAsync`/`RejectAsync` (admin-only, including that a `Rejected` business can be reconsidered back to `Approved`), and `HideAsync`/`UnhideAsync` (admin-only, notifies staff). `IAuditLogService`/`INotificationService` are mocked alongside `IBusinessRepository`.
- **`Services/ReportServiceTests.cs`** — covers `ReportService`'s authorization split (submit open to any signed-in user; dismiss/take-action/list admin-only) and that `TakeActionAsync` delegates to the right target service (`IBusinessService.HideAsync` vs. `IPackageService.HideAsync`) based on `TargetType` rather than mutating the target itself. `IReportRepository`/`IBusinessService`/`IPackageService`/`IAuditLogService` are mocked.
- **`Repositories/BusinessRepositoryTests.cs`** — the many-to-many `BusinessStaff` CRUD and the `SetHoursAsync`/`AddClosureAsync`/`RemoveClosureAsync` hours/closures methods against a real `EcoMealDbContext`, InMemory-provider-backed (`InMemoryDb.Create()`) rather than mocked — exercises `BusinessRepository`'s own query/persistence logic instead of `BusinessService`'s authorization, which `BusinessServiceTests.cs` above covers with the repository mocked out.
- **`Models/BusinessHoursStatusTests.cs`** — pure-function coverage for `BusinessHoursStatus.IsOpenNow`/`ActiveClosure` (§3), no DbContext at all: today-in-window, today marked closed, no row for today, an active vs. an out-of-range `BusinessClosure`, and the overnight-window wraparound case (`CloseTime < OpenTime`).
- **`Database/DbSeederTests.cs`** — integration tests that run `DbSeeder.SeedAsync` against a **real Postgres** container (`Testcontainers.PostgreSql`, `Tests/TestSupport/PostgresFixture.cs`), applying real EF migrations first via `MigrateAsync()` — exactly what `Program.cs` does on startup. This is deliberate, not incidental: an InMemory-provider test wouldn't replay real migration history, so it can't catch the class of bug this project has hit before (see §9 point 4's history and the old `SeedData`/`MoreSeedData` migrations vs. `DbSeeder` conflict) — only a real migration run proves the current seed data actually wins. One Postgres container is shared per test class; each test gets its own logical database on it (`CreateDatabaseAsync`) for isolation without paying container-startup cost per test. Requires Docker to be running locally.

Given this split, Postgres-only query behavior (`EF.Functions.ILike` in `OrderRepository`, the `xmin` optimistic-concurrency token on `Package`, the `order_numbers` sequence) is exercised by the seeding integration tests' real Postgres round-trip, not by the InMemory-backed unit tests.
