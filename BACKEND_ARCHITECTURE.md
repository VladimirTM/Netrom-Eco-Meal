# Backend Architecture — Netrom Eco Meal

**Stack:** ASP.NET Core 10 Web API · Entity Framework Core · PostgreSQL (Npgsql) · ASP.NET Identity Core (JWT Bearer auth) · SignalR · QRCoder · Stripe Checkout (`Stripe.net`) · `Microsoft.Extensions.AI` + `OllamaSharp` (self-hosted Ollama) · `WebPush` (browser push notifications) · Serilog
**Location:** `Backend/` — four projects under `Backend/NetromEcoMeal.slnx`

The backend is a layered ASP.NET Core Web API: `NetromEcoMeal.DataAccess` (EF Core, repositories, the data model) is referenced by `NetromEcoMeal.BusinessLogic` (services, DTOs, business rules), which is referenced by `NetromEcoMeal.Api` (controllers, auth, the request pipeline — the only runtime host). `NetromEcoMeal.Tests` references all three. A React single-page app (`Frontend/`, documented in `FRONTEND_ARCHITECTURE.md`) is the only client; every capability is reached over real HTTP, there is no in-process UI layer anywhere in this stack. The backend was rewritten into this shape from an earlier single-project Blazor Server monolith. Everything below describes the system as it exists today.

---

## Table of Contents

1. [Solution Structure](#1-solution-structure)
2. [Project Layers](#2-project-layers)
3. [Data Model & Entities](#3-data-model--entities)
4. [Repository Pattern](#4-repository-pattern)
5. [Service Layer](#5-service-layer)
6. [Controllers & DTOs](#6-controllers--dtos)
7. [Authentication & Authorization](#7-authentication--authorization)
8. [Cross-Cutting Request Pipeline](#8-cross-cutting-request-pipeline)
9. [SignalR — Live Stock](#9-signalr--live-stock)
10. [Background Services](#10-background-services)
11. [Database Seeding](#11-database-seeding)
12. [Configuration Reference](#12-configuration-reference)
13. [Automated Tests](#13-automated-tests)

---

## 1. Solution Structure

```
Backend/
├── NetromEcoMeal.slnx                  # references exactly the four projects below
├── Dockerfile                           # builds/publishes NetromEcoMeal.Api
├── NetromEcoMeal.DataAccess/
│   ├── Entities/                        # EF Core entity classes
│   ├── Database/
│   │   ├── EcoMealDbContext.cs          # IdentityDbContext<ApplicationUser> + Fluent API config
│   │   ├── EcoMealDbContextFactory.cs   # IDesignTimeDbContextFactory, for `dotnet ef` tooling
│   │   └── DbSeeder.cs                  # idempotent reference-data + demo-data seeding
│   ├── Repositories/
│   │   ├── Interfaces/                  # one interface per entity needing custom queries
│   │   └── *.cs                         # EF Core implementations
│   ├── Constants/                       # fixed string values, enums-as-strings, small pure helpers
│   ├── Models/                          # PaginatedList<T>, GeoDistance, LeaderboardEntry
│   ├── Migrations/                      # EF-generated migration history
│   └── ApiKeyHasher.cs                  # SHA-256 hex hashing for webhook API keys
├── NetromEcoMeal.BusinessLogic/
│   ├── Services/
│   │   ├── Interfaces/                  # one interface per service
│   │   ├── AI/                          # Ollama-backed assistants — see §5
│   │   ├── Email/                       # SmtpEmailSender, EmailTemplateBuilder
│   │   ├── Payments/                    # CheckoutService, StripeGateway
│   │   └── *.cs                         # business logic, authorization, orchestration
│   ├── Models/                          # BusinessHoursStatus, SearchIntent, …
│   └── DTOs/                            # every object that crosses the Api boundary — see §6
├── NetromEcoMeal.Api/
│   ├── Program.cs                       # the single source of DI, auth, CORS, middleware, hosting
│   ├── Controllers/                     # real [ApiController] classes, one per resource area
│   ├── Middleware/
│   │   └── ExceptionHandlingMiddleware.cs
│   ├── Hubs/
│   │   └── StockHub.cs                  # SignalR hub at /hubs/stock
│   ├── Services/
│   │   ├── HttpCurrentUser.cs            # ICurrentUser, reads the JWT ClaimsPrincipal
│   │   └── SignalRPackageStockNotifier.cs
│   ├── wwwroot/
│   │   └── js/impact-widget.js          # embeddable script, served from this host — see §8
│   └── appsettings*.json
└── NetromEcoMeal.Tests/                  # xUnit — unit, API-integration, DB-integration, architecture
    ├── Services/ · Repositories/ · Models/ · Database/ · Api/ · Architecture/
    └── TestSupport/                      # ApiFactory, PostgresFixture, InMemoryDb, fakes
```

`NetromEcoMeal.Api/Program.cs` wires every layer with plain `AddScoped<TInterface, TImplementation>()` calls — no assembly scanning, no MediatR, no separate composition-root project. Logging goes through Serilog rather than the default console provider: a minimal bootstrap logger (`Log.Logger = new LoggerConfiguration()...CreateBootstrapLogger()`) covers anything that fails before the host itself is up, then `builder.Host.UseSerilog(...)` replaces it with the real configuration-driven one (`ReadFrom.Configuration` — see §12 — plus `FromLogContext`/`WithMachineName`/`WithEnvironmentName`/`WithThreadId` enrichers). `app.UseSerilogRequestLogging()` is the first middleware in the pipeline, so it wraps and times every request including the exception-handling middleware further down. `DbSeeder`'s own logger and every ASP.NET Core/EF Core framework log line flow through the same sinks.

### Why four projects

The split is a strict dependency chain — `DataAccess ← BusinessLogic ← Api` — enforced partly by project references and partly by an actual test:

```csharp
// NetromEcoMeal.Tests/Architecture/LayeringTests.cs
private static readonly string[] ForbiddenAssemblyPrefixes =
[
    "Microsoft.AspNetCore.Components",
    "Microsoft.JSInterop",
];

[Fact]
public void BusinessLogic_DoesNotReferenceBlazorComponentsOrJsInterop() =>
    AssertNoForbiddenReferences(typeof(AuthService).Assembly);

[Fact]
public void DataAccess_DoesNotReferenceBlazorComponentsOrJsInterop() =>
    AssertNoForbiddenReferences(typeof(EcoMealDbContext).Assembly);
```
This is a deliberate guard against regressing into the old monolith's shape: `DataAccess` and `BusinessLogic` must stay usable by *any* ASP.NET Core host (a Web API today, conceivably a different front door tomorrow) and must never pull in a component-model dependency again. Both projects *do* carry a `FrameworkReference Include="Microsoft.AspNetCore.App"` — that's unrelated to Blazor: `BusinessLogic` needs it for `AuthService` (`UserManager`/`SignInManager`), the three sweep `BackgroundService` classes, `IWebHostEnvironment` (`ImageUploadService`), and ASP.NET Identity's `LoginRequest`/`RegisterRequest` DTOs — all plain ASP.NET Core/generic-host types that any Web API host needs regardless of UI technology.

`NetromEcoMeal.Tests` is the one project allowed to reference all three (`DataAccess`, `BusinessLogic`, and `Api` itself, the last one so its API-integration tests can boot `WebApplicationFactory<Program>`).

---

## 2. Project Layers

### 2.1 NetromEcoMeal.DataAccess

Entities, the `DbContext`, repositories, and small cross-cutting data-side helpers (`PaginatedList<T>`, `GeoDistance`, `LeaderboardEntry`, `ApiKeyHasher`). No DTOs here — this project only knows about entities and query shapes. `EcoMealDbContextFactory` implements `IDesignTimeDbContextFactory<EcoMealDbContext>` purely so `dotnet ef migrations add`/`dotnet ef database update` can construct a context at design time without needing the full `Api` host (and its JWT key / Stripe key / etc. configuration checks) to be satisfiable just to generate a migration.

### 2.2 NetromEcoMeal.BusinessLogic

All business logic, authorization checks, and orchestration — see [§5](#5-service-layer) — plus every DTO that crosses the wire (`DTOs/`, see [§6](#6-controllers--dtos)) and small pure models (`BusinessHoursStatus`, `SearchIntent`). DTOs live here rather than in `Api` because several services (not just controllers) build or consume them directly — e.g. `OrderDto.FromEntity` is called from `CheckoutService`'s completion result, not only from `OrdersController`.

### 2.3 NetromEcoMeal.Api

The only runtime host. Real `[ApiController]` classes under `Controllers/`, the SignalR hub, the exception-handling middleware, the two `ICurrentUser`/`IPackageStockNotifier` implementations that need live `HttpContext`/SignalR access, and `Program.cs` — the single file that wires DI, authentication, CORS, rate limiting, the middleware pipeline, static file serving, migrations/seeding, and background-job hosting. There is no `Api/DTOs` content in practice — the folder exists but every actual DTO lives in `BusinessLogic/DTOs` (see §2.2) and controllers just `using NetromEcoMeal.DTOs;`.

### 2.4 NetromEcoMeal.Tests

xUnit. Unit tests (mocked dependencies, sometimes a real EF Core InMemory-provider `DbContext`), API-integration tests that boot the real `Api` host via `WebApplicationFactory<Program>` against a Testcontainers-backed Postgres, repository/DB-integration tests against the same real Postgres, and the architecture test above. See [§13](#13-automated-tests).

---

## 3. Data Model & Entities

The domain model itself — every entity below — has not changed shape across the migration from Blazor Server to this layered Web API; only how it's exposed over the wire changed (entities never cross the API boundary directly, DTOs do — see §6). Each entity has been re-verified against the current `NetromEcoMeal.DataAccess/Entities/*.cs` for this document.

### Entity Overview

```
ApplicationUser (IdentityUser)
  │
  ├──< Order >──── Business ──── BusinessType
  │      │  │          │
  │      │  └──── Payment  (0..1 — set once Stripe Checkout confirms payment)
  │      ├──< OrderPickupPass         (created on Confirm; split into several by the customer —
  │      │                             see §3 OrderPickupPass)
  │      ├──── RescueCircle           (0..1 — set when this order was started as a shared basket
  │      │                             instead of a solo checkout; see §3 RescueCircle)
  │      └──< OrderPackage >──── Package ──── PackageType
  │                                  │
  │                                  └──── PackageTemplate  (0..1, TemplateId — the template that
  │                                                           generated this instance, if any)
  │
  ├──< Favorite >──── Business        (one per (UserId, BusinessId), unique index)
  ├──< Review >────── Business        (one per (BusinessId, UserId), unique index)
  ├──< Notification
  ├──< PushSubscription               (one per subscribed browser/device, unique on Endpoint)
  ├──< StandingOrder >──── Business   (optional PackageTypeId/DietaryTag narrowing — see §3 StandingOrder)
  ├──< KitchenTip >──────── Business  (moderated through the Report/hide pipeline — see §3 KitchenTip)
  ├──< StoreCreditEntry               (ledger row; positive grants credit, negative spends it)
  └──< PendingCheckout ──── Business  (no FK/nav to Order — bridges checkout to Stripe; see §3 PendingCheckout)

RescueCircle ──< RescueCircleParticipant >── ApplicationUser  (one row per joiner, organizer
                                                                included; unique on (RescueCircleId, UserId))
Business ──< BusinessStaff >── ApplicationUser  (many-to-many: staff a business, unique on (BusinessId, UserId))
Business ──< BusinessHours            (0..7 rows, one per DayOfWeek, unique on (BusinessId, DayOfWeek))
Business ──< BusinessClosure          (0..N holiday date-range overrides)
Business ──── Brand                   (0..1, BrandId — groups several locations under one shared page; see §3 Brand)
ApplicationUser ──< BrandFavorite >──── Brand   (one per (UserId, BrandId), unique index — mirrors Favorite/Business)
Order ──── Status                     (Pending | Confirmed | Completed | Cancelled | NoShow)
PackageTemplate ──< Package           (0..N generated instances, one per calendar day)
Referral ──── ApplicationUser (Referrer) / ApplicationUser (Referred)  (unique on ReferredUserId — see §3 Referral)

AuditLog                              (no FK/nav to its target — polymorphic Business/Package/KitchenTip/User, denormalized TargetName)
Report ──── ApplicationUser (Reporter)  (TargetId/TargetType also polymorphic, no FK to the target)
```

### Entities

#### ApplicationUser
```csharp
public class ApplicationUser : IdentityUser
{
    public required string Name { get; set; }
    public bool ShowOnLeaderboard { get; set; }
    public string? ReferralCode { get; set; }
    public ICollection<Order> Orders { get; set; } = [];
}
```
Extends Identity's built-in user with the fields Identity doesn't provide: a display name, an opt-in flag for the `/impact` leaderboard, and a referral code. Everything else (email, password hash, roles, `SecurityStamp`) comes from `IdentityUser`/`IdentityDbContext` for free — `SecurityStamp` in particular is what makes JWT revocation possible (§7). `ShowOnLeaderboard` defaults `false` — a customer flips it via `ImpactController.SetMyOptInStatus` (`ImpactService.SetMyOptInStatusAsync`, through `UserManager<ApplicationUser>`, not a repository — there's no `ApplicationUser`-specific repository, Identity's own store already covers CRUD). `OrderRepository.GetTopRescuersAsync` filters on it directly, so a customer who never opts in never appears on the leaderboard, full stop.

`ReferralCode` is `null` until the account's first call to `ReferralsController.GetMyReferralInfo` — `ReferralService.GetMyReferralInfoAsync` generates and persists it lazily (`RandomNumberGenerator.GetHexString(8)`, retried on the rare collision) rather than at every account-creation path, so both self-registered and seeded/admin-created users end up with one the same way. Carries a nullable unique index — Postgres allows any number of `null` rows under a unique index, so accounts that have never generated a code don't collide with each other.

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
    public int? LoyaltyPunchThreshold { get; set; }
    public decimal? LoyaltyDiscountAmount { get; set; }
    public Guid? BrandId { get; set; }              // optional — groups this location under a chain, see §3 Brand
    public string? WebhookApiKeyHash { get; set; }  // SHA-256 hex hash only — never the plaintext key
    public DateTime? WebhookApiKeyLastUsedAt { get; set; }
    public Guid BusinessTypeId { get; set; }
    public BusinessType BusinessType { get; set; } = null!;
    public Brand? Brand { get; set; }
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

`Hours`/`Closures` back the weekly-schedule + holiday-closure feature (below) — both loaded via `AsSplitQuery()` on every `BusinessRepository.GetAllAsync`/`GetPagedAsync`/`GetByIdAsync` call alongside `Staff`, since three separate `Include`d collections on one query would otherwise multiply rows together (the "cartesian explosion" EF Core's `MultipleCollectionIncludeWarning` warns about for this shape).

`Latitude`/`Longitude` are both nullable — set by hand or via browser geolocation on the frontend's business form, read by `BusinessRepository.GetPagedAsync`'s `BusinessSortOptions.Distance` sort and the frontend's map view (see the Pagination Helper note in §4).

`LoyaltyPunchThreshold`/`LoyaltyDiscountAmount` are both nullable and both-or-neither — a manager sets both on the business form to turn the reward on, or leaves both blank to turn it off; `BusinessService.NormalizeLoyalty` (called from both `AddAsync` and `UpdateAsync`) forces the pair back to `(null, null)` if either one is missing rather than persisting a half-configured reward, and clamps whatever's left to `Constants.Loyalty`'s bounds (2–50 orders, 0.5 lei minimum). No new tracking table needed — `ILoyaltyService`/`LoyaltyService` (§5) recomputes a customer's progress straight from real `Completed` orders on every call.

`Status`/`RejectionReason`/`SubmittedByUserId` back the self-service approval workflow — an admin-created business (`BusinessService.AddAsync`) is always born `Approved`; a customer/manager self-service application (`BusinessService.ApplyAsync`) is born `PendingApproval` with `SubmittedByUserId` set to the applicant, and only an admin can move it to `Approved`/`Rejected` (`ApproveAsync`/`RejectAsync`). `IsHidden`/`HiddenReason` are a separate moderation flag an admin can toggle on any `Approved` business independent of its approval status (`HideAsync`/`UnhideAsync`) — a business can only be publicly visible to customers when `Status == Approved && !IsHidden` (`BusinessRepository.GetPagedAsync`/`GetAllAsync`'s `publicOnly` parameter).

`BrandId` is nullable with `OnDelete(DeleteBehavior.SetNull)` — deliberately not `Cascade` like `BusinessTypeId`'s convention default, since deleting a `Brand` should just un-group its locations rather than take them down. `WebhookApiKeyHash`/`WebhookApiKeyLastUsedAt` back the webhook intake endpoint (§5/§6); `WebhookApiKeyHash` carries a nullable unique index (same "any number of `null` rows, but a real value must be unique" convention as `ApplicationUser.ReferralCode` above) so two businesses can never collide on the same key.

#### Brand / BrandFavorite
```csharp
public class Brand
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<Business> Businesses { get; set; } = [];
}

public class BrandFavorite
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BrandId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Brand Brand { get; set; } = null!;
}
```
Multi-location grouping — a `Brand` is deliberately just a name and description, no `ImageUrl` of its own (each location already has one), so its admin CRUD (`BrandService`/`BrandsController`) reuses the exact add/rename/delete-blocked-while-in-use shape `BusinessTypeService`/`PackageTypeService` already established (`BrandRepository.IsInUseAsync` mirrors `IBusinessTypeRepository.IsInUseAsync`'s "at least one row still references this" check). `BrandService.GetPublicDetailAsync` is the one read path with any real logic: it loads only that brand's `Approved`/not-`IsHidden` locations (`BrandRepository.GetPublicLocationsAsync`, same visibility rule `BusinessRepository`'s `publicOnly` flag enforces) and aggregates a rating across all of them via the existing `IReviewRepository.GetByBusinessIdsAsync`.

`BrandFavorite` mirrors `Favorite` exactly (same shape, same unique index, same toggle-not-duplicate semantics in `BrandService.ToggleFavoriteAsync`/`BrandRepository`), just keyed on `BrandId` instead of `BusinessId` — kept as its own table rather than widening `Favorite` itself, since a favorite is always unambiguously "this business" or "this whole chain," never both.

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
The join table behind `Business.Staff` — replaced an earlier `Business.ManagerId` nullable-FK design that capped a manager at one business. Unique index on `(BusinessId, UserId)` so the same pairing can't be added twice; both FKs cascade-delete, so removing a business or a user account cleans up their staff rows instead of leaving orphans or blocking the delete. Admin-only to add/remove (`BusinessesController.AddStaff`/`RemoveStaff`).

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
`BusinessHours` is a fixed weekly schedule — up to one row per `DayOfWeek` (unique index on `(BusinessId, DayOfWeek)`), always written as a complete replacement of the week rather than a per-day upsert: `IBusinessService.SetHoursAsync` → `IBusinessRepository.SetHoursAsync` deletes every existing row for the business and re-inserts whatever list it's given. A business with zero `BusinessHours` rows means "hours never configured" — distinct from every day being marked `IsClosed`, and treated as "unknown" (not "closed") by the open/closed calculation below. `BusinessClosure` is the opposite shape: an open-ended list of independent date ranges a manager adds/removes one at a time, each overriding `BusinessHours` for its `[StartDate, EndDate]` window regardless of what that weekday's hours say.

Both are cascade-deleted with their `Business`. Authorization mirrors `UpdateAsync`: admin or one of the business's own staff.

`Models.BusinessHoursStatus` is the pure open/closed calculation the frontend uses for a business's open/closed badge and hours panel — `IsOpenNow(hours, closures, localNow)` returns `null` (unknown, hide the indicator) when `hours` is empty, `false` when an active `BusinessClosure` covers `localNow`'s date or today's `BusinessHours` row is missing/closed/outside its open–close window, `true` otherwise. It takes the already-loaded collections rather than a `Business`/DbContext, so it's covered by plain unit tests (`Tests/Models/BusinessHoursStatusTests.cs`) with no database involved — including the overnight-window case (`CloseTime < OpenTime`, e.g. 22:00–02:00) where a naive `>= open && < close` check would wrongly read "closed" for the stretch after midnight. `localNow` is supplied by the caller as the viewer's browser-local time — this app has no per-business timezone field, so a business's hours are assumed to mean the same local time as everything else it shows.

#### BusinessType / PackageType / Status
```csharp
public class BusinessType { public Guid Id; public required string Name; }
public class PackageType  { public Guid Id; public required string Name; }
public class Status       { public Guid Id; public required string Name; }
```
Three near-identical lookup tables, seeded once by `DbSeeder` and never written to afterward except through their own admin CRUD (`BusinessTypeService`/`PackageTypeService`). `Status.Name` values are fixed by `Constants.OrderStatuses` (`Pending`/`Confirmed`/`Completed`/`Cancelled`/`NoShow`) and looked up by name everywhere rather than by a hardcoded `Guid` — the seed IDs exist only so re-seeding is idempotent. `NoShow` was added after the original four in a way that had to work around an old migration having hardcoded `InsertData` for those four (see §11) — `SeedStatusesAsync` adds whichever names are missing rather than bailing out when the table is merely non-empty.

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
    public DateTime? NearExpiryNudgeSentAt { get; set; }  // set once NearExpiryNudgeService has notified interested customers
    public DateTime? MarkdownDismissedAt { get; set; }    // set when a manager dismisses a markdown suggestion
    public DateTime? DonatedAt { get; set; }              // set when a manager marks a closed, completely-unsold package as donated
    public DateTime? DonationOfferedAt { get; set; }      // set once the sweep has notified staff — keeps it from re-notifying
    public Business Business { get; set; } = null!;
    public PackageType PackageType { get; set; } = null!;
    public PackageTemplate? Template { get; set; }
    public ICollection<OrderPackage> OrderPackages { get; set; } = [];
}
```
Restocking a package from `0` to a positive `Quantity` (or publishing a brand-new one) notifies everyone who's favorited that business — see `PackageService` in §5; there's no per-package "notify me" subscription, so Favorites doubles as the closest proxy.

`Quantity` carries an EF Core **shadow property row-version** — `modelBuilder.Entity<Package>().Property<uint>("xmin").IsRowVersion()` maps Postgres's native `xmin` system column as an optimistic-concurrency token, with zero extra columns to migrate. Two managers confirming orders against the same package's last unit at the same time get a `DbUpdateConcurrencyException` on the loser, translated by `OrderService` into "Stock for this order just changed — please refresh and try again" instead of silently overselling. `DietaryTags` is stored as a plain `List<string>` — EF Core maps this to a Postgres `text[]` column with no extra configuration needed.

`TemplateId` is nullable and `OnDelete(DeleteBehavior.SetNull)` — deleting the owning `PackageTemplate` unlinks any instances it already generated instead of deleting them, since they're real packages that may already have orders against them.

`IsHidden`/`HiddenReason` are the package-level counterpart to `Business.IsHidden` moderation — an admin or the package's own business staff can toggle it (`PackageService.HideAsync`/`UnhideAsync`, same `EnsureCanManageBusinessAsync` authorization as every other write on this entity) to pull a specific package off the storefront without touching the rest of the business.

`DonatedAt`/`DonationOfferedAt` back "mark as donated" — a distinct outcome for a package whose pickup window closed with **zero** units ever ordered (`IPackageRepository.HasAnyOrdersAsync` is false), instead of it just silently vanishing from every live query the way an unsold-but-partially-ordered package already did. `OrderLifecycleSweepService` calls `PackageService.NotifyDonationCandidatesAsync()` every tick, which finds every still-live (`!IsHidden`), closed (`PickupEnd < now`), completely-unsold, not-yet-donated package (`IPackageRepository.GetDonationCandidatesAsync`) and notifies that business's staff once each (`DonationOfferedAt` set so it isn't repeated); `PackagesController` surfaces the same candidate set, and a manager can call `MarkAsDonatedAsync`, which re-validates both guards server-side (throws `InvalidOperationException` if the window's still open or the package did have an order) rather than trusting the client's own filtering. A donated package's `WeightKg` counts toward every platform "food saved" aggregate (`OrderService.GetTotalKgSavedAsync`, `ImpactService.GetBusinessWidgetStatsAsync`) right alongside Completed-order pickups, via `IPackageRepository.GetDonatedWeightKgAsync(businessId?)`, without ever touching `Order`/`Status` — so it can never inflate sell-through or any other revenue-shaped metric.

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
Backs the "repeat this every day" option when creating a package — opting in calls `PackageTemplateService.CreateFromPackageAsync`, which copies the just-created package's fields into a new template, derives `PickupStartTimeUtc`/`PickupEndTimeUtc` from that package's `PickupStart`/`PickupEnd` time-of-day, and links the two via `Package.TemplateId`. `PickupEndTimeUtc <= PickupStartTimeUtc` means the window crosses midnight (end falls the next day) — handled by `PackageTemplateGenerationService`'s `GenerateDueInstancesAsync` (§10) when combining the stored time-of-day with a calendar date.

`LastGeneratedDate` is what makes generation idempotent regardless of the background sweep's cadence — a template only ever produces one `Package` per UTC calendar day, tracked here rather than derived by querying for an existing instance. `IsActive` is the pause/resume toggle; a paused template stops generating but its already-created instances are untouched. Managed with the same admin-or-own-business-manager authorization as `Package` itself.

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
    public string? LogisticsNote { get; set; }       // free-text note for the business
    public required ApplicationUser User { get; set; }
    public Business Business { get; set; } = null!;
    public Status Status { get; set; } = null!;
    public ICollection<OrderPackage> OrderPackages { get; set; } = [];
    public Payment? Payment { get; set; }           // null until CheckoutService confirms the Stripe payment
    public ICollection<OrderPickupPass> PickupPasses { get; set; } = [];  // created on Confirm — see §3 OrderPickupPass
    public RescueCircle? RescueCircle { get; set; } // set when this order started as a shared basket — see §3 RescueCircle
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
`OrderNumber` is **not** application-assigned — `EcoMealDbContext` maps it to `nextval('order_numbers')` via `HasDefaultValueSql`, backed by a real Postgres sequence (`modelBuilder.HasSequence<int>("order_numbers")`). This means two concurrent checkouts can never collide on the same human-friendly ticket number the way an app-side `MAX(OrderNumber) + 1` could. `OrderNumber` also carries its own unique index as a belt-and-suspenders check. `OrderPackage` does **not** snapshot the package's name/price at order time — it stays live-joined to `Package`, so a manager editing a package's name after the fact would (in principle) change how historical orders render. This hasn't come up as a real problem in practice since packages are re-created daily rather than edited after orders exist against them.

An `Order` now only ever exists once its Stripe Checkout payment is confirmed — see `PendingCheckout` and `Payment` below, and `CheckoutService` in §5, for the bridge that makes that true. `LogisticsNote` is a free-text accessibility/logistics note the customer can leave at checkout — trimmed and clamped to `Constants.OrderLogistics.MaxNoteLength` by `OrderService.PlaceOrderAsync`. Since a solo `Order` doesn't exist until Stripe confirms payment, it rides along on `PendingCheckout.LogisticsNote` (below) until `CheckoutService.CompleteCheckoutAsync` copies it onto the real `Order`; a Rescue Circle order (created immediately) gets it straight through `PlaceOrderAsync`'s own parameter.

#### OrderPickupPass
```csharp
public class OrderPickupPass
{
    public Guid Id { get; set; }
    public required Guid OrderId { get; set; }
    public required string Label { get; set; }       // "Pickup pass" (single) or "Pass 1"/"Pass 2"/...
    public DateTime CreatedAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public Order Order { get; set; } = null!;
}
```
Replaces an older implicit one-QR-per-order model with a first-class child entity, so a group order can be picked up by whoever from the group gets there first instead of forcing a single designated person to hold the only QR code. `OrderService.ApplyStatusChangeAsync` creates exactly one default pass the moment an order becomes Confirmed; the customer can later replace that set with up to `Constants.PickupPasses.MaxPasses` fresh ones via `OrderService.SplitPickupPassesAsync` — always a full replace, never a partial edit, which is safe because a Confirmed order's passes are guaranteed unredeemed (see next paragraph). Each pass encodes a QR of `/orders/validate/{orderId}/{passId}`, resolved by the frontend.

`OrderService.RedeemPickupPassAsync` is the only place `RedeemedAt` gets set, and it always also completes the *whole* order in the same call (`ApplyStatusChangeAsync(order, Completed)`) — first scan wins for everyone in the group. That's why a Confirmed order's passes are always unredeemed: the instant any one of them is redeemed, the order stops being Confirmed, so `SplitPickupPassesAsync` never has to reason about orphaning an already-used pass.

New passes are added via `dbContext.OrderPickupPasses.Add(...)`, **not** the `order.PickupPasses` collection navigation — a real bug hit during development. A brand-new entity discovered only through navigation-fixup, whose primary key is a client-assigned, non-default `Guid` (every entity in this app sets `Id = Guid.NewGuid()` itself rather than letting the database generate it), gets tracked as `EntityState.Modified` instead of `Added`. Npgsql then emits a no-op `UPDATE ... WHERE "Id" = @id` for a row that was never inserted, which Postgres correctly reports as 0 rows affected, and EF Core surfaces as a `DbUpdateConcurrencyException` — a confusing failure mode for what looks like a normal `Add()`. A mocked `IOrderRepository` (as `OrderServiceTests` uses everywhere else) can't catch this, since its `SaveChangesAsync()` never touches a real database; the regression test lives in `OrderServicePickupPassIntegrationTests` (§13), which runs the real `OrderService` against a real Postgres container instead.

Orders confirmed before this feature existed have zero pass rows. Rather than a one-time data migration, `GetMyOwnedOrderAsync`/`GetOwnedOrderAsync` — the two lookups behind every pickup-pass/QR-validation read — call a private `BackfillPickupPassIfNeededAsync` first, which lazily adds the single default pass a Confirmed order would otherwise have gotten at confirmation time. A legacy single-segment QR code (no `PassId`) is resolved by the frontend to the order's one pass when there's exactly one; anything other than exactly one pass renders "couldn't tell which pass this refers to" rather than guessing.

#### RescueCircle / RescueCircleParticipant
```csharp
public class RescueCircle
{
    public Guid Id { get; set; }
    public required Guid OrderId { get; set; }
    public required string OrganizerId { get; set; }
    public required int ParticipantCount { get; set; }
    public required decimal TotalAmount { get; set; }
    public required string Status { get; set; }      // Open | Cancelled — see Constants.RescueCircleStatuses
    public DateTime CreatedAt { get; set; }
    public Order Order { get; set; } = null!;
    public ApplicationUser Organizer { get; set; } = null!;
    public ICollection<RescueCircleParticipant> Participants { get; set; } = [];
}

public class RescueCircleParticipant
{
    public Guid Id { get; set; }
    public required Guid RescueCircleId { get; set; }
    public required string UserId { get; set; }
    public required decimal ShareAmount { get; set; }
    public DateTime JoinedAt { get; set; }
    public string? StripeCheckoutSessionId { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public DateTime? RefundedAt { get; set; }
    public RescueCircle RescueCircle { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
```
A shared basket split across several payers against one underlying `Order` — the frontend's "split this with friends" flow starts one (`RescueCircleService.StartCircleAsync`) in place of a solo Stripe checkout, `ParticipantCount` fixed at creation (`Constants.RescueCircles.MinParticipants`/`MaxParticipants`, 2–6, mirroring `PickupPasses`' own bounds since a fully-paid circle ends up generating exactly one pass per participant). `1:1` with `Order` (`HasOne(c => c.Order).WithOne(o => o.RescueCircle)`, unique index on `OrderId`, cascade-deletes with the order) — the underlying `Order` is placed immediately, the same way `OrderService.PlaceOrderAsync` always has (reserving stock, notifying staff), but stays `Pending` until every slot is paid rather than only after a single successful checkout.

`RescueCircleParticipant` is one row per joiner, the organizer included (added automatically when the circle is started) — unique on `(RescueCircleId, UserId)`, so joining twice just re-shows the existing slot. It carries its own Stripe fields rather than reusing `Payment`, since `Payment` is strictly one-per-`Order` (below) and here each participant pays a separate Checkout session for just their own `ShareAmount`. Every non-organizer slot gets the same round-cent `Math.Floor(TotalAmount / ParticipantCount * 100) / 100`; the organizer's own share (set once, at creation) absorbs whatever that division doesn't divide evenly, so the shares always sum to exactly `TotalAmount`.

`RescueCircle.Status` only ever tracks `Open`/`Cancelled` — "fully paid" is deliberately **not** its own stored status. It's derived everywhere it's needed (`Participants.Count == ParticipantCount && Participants.All(p => p.PaidAt is not null)`) so it can never drift out of sync with the participant rows that actually decide it. `OrderService.ApplyStatusChangeAsync`'s `Pending → Confirmed` transition refuses to confirm a Rescue Circle order until it holds, and once it holds, gives the order one `OrderPickupPass` per participant automatically instead of the usual single default pass — see §5 OrderService.

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
One row per `Order`, created by `CheckoutService.CompleteCheckoutAsync` the moment Stripe confirms a session as paid — a true 1:1 with `Order`, matching Stripe Checkout being one session per order, never split. Never deleted: `OrderService.RefundIfPaidAsync` flips `Status` to `Refunded` and stamps `RefundedAt` on cancellation instead of removing the row, or to `RefundFailed` (no `RefundedAt`) if the Stripe call itself errors, and deliberately leaves it `Succeeded` on a `NoShow` — the un-reversed charge **is** the no-show fee, with no separate fee-specific code needed.

#### PendingCheckout
```csharp
public class PendingCheckout
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    public required string LinesJson { get; set; }      // serialized cart lines at checkout time
    public string? LogisticsNote { get; set; }           // carries Order.LogisticsNote across to Stripe and back
    public decimal? CreditApplied { get; set; }          // store credit folded into the Stripe discount, if any
    public string? StripeCheckoutSessionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }            // set once CompleteCheckoutAsync has resolved this
    public Guid? ResultingOrderId { get; set; }           // null if the paid-for order couldn't be placed and was refunded instead
}
```
Bridges "customer clicked Pay" to "the `Order` actually exists." `CheckoutService.StartCheckoutAsync` parks the cart's lines here and sends the customer to Stripe's hosted Checkout page *before* any `Order` (and its stock reservation) is created — `CompleteCheckoutAsync`, called by the frontend's checkout-return page via `PaymentsController`, re-validates the Stripe session, only then calls `IOrderService.PlaceOrderAsync`, and stamps `ConsumedAt`/`ResultingOrderId`. No FK/navigation to `Order` — deliberately loose, since a `PendingCheckout` can resolve to no order at all. `ConsumedAt` being non-null is what makes completion idempotent: a page refresh on the return URL replays `CompleteCheckoutAsync` against the same row instead of double-spending one Stripe payment into two orders. Abandoned rows (`ConsumedAt` still null past `OrderExpiry.PendingCheckoutTimeout`) are hard-deleted by `OrderLifecycleSweepService` (§10) — nothing was ever charged, so there's nothing to refund, just tidying. `CreditApplied` carries how much `StartCheckoutAsync` folded into the Stripe discount from the customer's `StoreCreditEntry` balance, if any — `CompleteCheckoutAsync` debits the ledger by exactly this stored amount rather than whatever the live balance happens to be by the time Stripe redirects back.

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
    public Guid? PackageId { get; set; }             // optional tag to a specific package
    public Business Business { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public Package? Package { get; set; }
}
```
Unique index on `(BusinessId, UserId)` — resubmitting via `ReviewService.SubmitAsync` updates the existing row in place (and bumps `CreatedAt`) rather than creating a duplicate. Gated on `IOrderRepository.HasCompletedOrderAsync(userId, businessId)` — a customer must have at least one `Completed` order with that business before they can review it. `PackageId` narrows what the review is about without changing that scope. `OnDelete(DeleteBehavior.SetNull)` on the FK means deleting a package un-tags any review that pointed at it instead of deleting the review.

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
Unique index on `(UserId, BusinessId)`. `FavoriteRepository.AddAsync`/`RemoveAsync` persist immediately (their own `SaveChangesAsync` call) rather than following the stage-then-`SaveChangesAsync` split most other repositories use.

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
Created server-side by `OrderService` at every status transition (new order → the business's manager; confirmed/completed/cancelled/no-show → the customer), by `OrderLifecycleSweepService` on auto-cancel/pickup-reminder/no-show, and by `PackageService` on restock. Every one of those customer-facing order-lifecycle notifications also fires a best-effort email via `IAppEmailSender` — the in-app bell record is still the source of truth; the email is a delivery-channel add-on that never blocks the underlying transition if it fails. `NotificationService.CreateAsync` itself (not each caller) also fires a best-effort **web push** to every one of the target user's `PushSubscription` rows via `IPushSubscriptionService`/`IWebPushGateway` — centralized here rather than duplicated per caller like email. Indexed on `(UserId, CreatedAt)` since "this user's notifications, newest first" is the only query pattern.

#### PushSubscription
```csharp
public class PushSubscription
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string Endpoint { get; set; }
    public required string P256Dh { get; set; }
    public required string Auth { get; set; }
    public DateTime CreatedAt { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
```
One row per browser/device a user has enabled push on. `Endpoint` carries a unique index; `PushSubscriptionRepository.AddAsync` re-points an existing row to the calling user rather than fail that index if the same browser endpoint resubscribes under a different account. Cascade-deleted with its `User`. A subscription the push service reports as gone (HTTP 404/410 from `IWebPushGateway.SendAsync`) is deleted automatically on the next send attempt.

#### StandingOrder
```csharp
public class StandingOrder
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required Guid BusinessId { get; set; }
    public Guid? PackageTypeId { get; set; }
    public string? DietaryTag { get; set; }
    public required decimal MaxWeeklySpend { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Business Business { get; set; } = null!;
    public PackageType? PackageType { get; set; }
}
```
"Auto-reserve my usual from this kitchen whenever it goes live, up to X lei/week" — a customer's saved preference. `PackageTypeId`/`DietaryTag` are both optional narrowing filters — `null` means "any package/tag from this kitchen matches" — validated at creation case-insensitively against `Constants.DietaryTags.All`.

Matching happens only against **freshly generated** packages, not by re-scanning every live package on some interval — `PackageTemplateGenerationService`'s existing daily sweep (§10) is the trigger it rides: `IPackageTemplateService.GenerateDueInstancesAsync` returns the actual generated `Package` instances, handed straight to `IStandingOrderService.MatchNewPackagesAsync`. For each generated package, every active `StandingOrder` at that business is checked (type/tag narrowing, then the customer's own trailing-7-day spend on matching packages there, against `MaxWeeklySpend`), and a match still under budget fires the existing bell + email channels.

Since `Order` only ever exists once Stripe confirms payment, there's no way to silently reserve stock server-side — a match notification instead points the customer at the business page with a `?reserve={packageId}` hint the frontend resolves into adding that exact package to the cart on load. Nothing is ever auto-charged — the customer still opens the basket and pays themselves. Cascade-deletes with its `User` or `Business`; `OnDelete(DeleteBehavior.SetNull)` on `PackageType` widens a standing order back to "any type" instead of deleting it if that type is later removed.

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
The trust-and-safety record — who promoted/demoted a user, who created/edited/deleted/staffed a business (including its hours/closures), who approved/rejected/hid/unhid a business or package, who dismissed/actioned a report, who confirmed/completed/cancelled/no-showed an order. Deliberately has **no** FK/navigation to its target: `TargetType`/`TargetId` are polymorphic (`User`, `Business`, `Package`, `Order`, `BusinessType`, `PackageType`, or `KitchenTip` — `Constants.AuditTargetTypes`), so `TargetName` is captured as plain text at write time instead of joined at read time. Written exclusively by `IAuditLogService.LogAsync`, called from *inside* `BusinessService`/`PackageService`/`UserService`/`ReportService`/`OrderService` after a mutation has already succeeded, never exposed on a controller as a standalone write. `LogAsync` resolves `ActorUserId`/`ActorName` itself via `ICurrentUser` + `UserManager<ApplicationUser>`. The background sweeps (§10) mutate `Order.StatusId` directly instead and don't call it, so they never log to `AuditLog` at all. Indexed on `CreatedAt` — newest-first is the only sort.

#### Report
```csharp
public class Report
{
    public Guid Id { get; set; }
    public required string ReporterUserId { get; set; }
    public ApplicationUser Reporter { get; set; } = null!;
    public required string TargetType { get; set; }   // Business, Package, or KitchenTip
    public required Guid TargetId { get; set; }
    public required string Reason { get; set; }
    public required string Status { get; set; }        // see Constants.ReportStatuses
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
}
```
A customer-submitted flag on a business, package, or kitchen tip — the "report" half of a hide/report flow instead of only hard delete. `TargetId`/`TargetType` are polymorphic like `AuditLog`'s, so there's no FK to the target — `ReportService` resolves target names at read time via `IBusinessService`/`IPackageService`/`IKitchenTipService.GetNamesByIdsAsync`/`GetSnippetsByIdsAsync` instead, one batched call per target type rather than a `GetByIdAsync` per report. `SubmitAsync` is open to any signed-in user; `DismissAsync`/`TakeActionAsync`/`GetOpenAsync` are admin-only. `TakeActionAsync` doesn't hide the target itself — it delegates to `IBusinessService.HideAsync`/`IPackageService.HideAsync`/`IKitchenTipService.HideAsync` (using the report's own `Reason` as the hide reason) and lets those log their own `AuditLog` entry, then logs a second `ReportActionTaken` entry of its own.

#### KitchenTip
```csharp
public class KitchenTip
{
    public Guid Id { get; set; }
    public required Guid BusinessId { get; set; }
    public required string UserId { get; set; }
    public required string Tip { get; set; }          // 200-char cap — Constants.KitchenTips.MaxLength
    public DateTime CreatedAt { get; set; }
    public bool IsHidden { get; set; }
    public string? HiddenReason { get; set; }
    public Business Business { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
```
A short practical hint a customer leaves on a business page, deliberately separate from star `Review`s and — unlike `Review` — not gated on having ordered there. Moderated through the *exact* pipeline `Business`/`Package` already use: `AuditTargetTypes.KitchenTip` is a third value alongside `Business`/`Package`, and `IKitchenTipService.HideAsync`/`NotifyHiddenAsync` mirror `IBusinessService`/`IPackageService`'s own shape. No FK from `Report` to a `KitchenTip` — same polymorphic, denormalized-name approach as every other moderatable target. Indexed on `(BusinessId, CreatedAt)`. Both FKs cascade-delete.

#### Referral / StoreCreditEntry
```csharp
public class Referral
{
    public Guid Id { get; set; }
    public required string ReferrerUserId { get; set; }
    public required string ReferredUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RewardedAt { get; set; }
    public ApplicationUser Referrer { get; set; } = null!;
    public ApplicationUser Referred { get; set; } = null!;
}

public class StoreCreditEntry
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required decimal Amount { get; set; }      // positive grants credit, negative spends it
    public required string Reason { get; set; }
    public Guid? RelatedOrderId { get; set; }          // set only on a spend row — which checkout it was applied to
    public DateTime CreatedAt { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
```
`Referral` is created by `ReferralService.RegisterReferralAsync` (called from `AuthService.RegisterAsync`) whenever registration carries a referral code that resolves to a real `ApplicationUser.ReferralCode` — an unknown/blank code is silently ignored. Unique index on `ReferredUserId`: a referred user can only ever have been invited once. `RewardedAt` is set by `ReferralService.TryRewardFirstCompletionAsync`, called from `OrderService.ApplyStatusChangeAsync`'s `Completed` branch — it no-ops unless this is genuinely the referred user's first `Completed` order *ever* (platform-wide, not per-business).

`StoreCreditEntry` is a ledger, not a balance column — `IStoreCreditRepository.GetBalanceAsync` sums every row for a user on demand, the same "can't drift" reasoning `LoyaltyService`/`OrderService.GetTotalKgSavedAsync` already apply to their own recomputed-not-stored figures. `TryRewardFirstCompletionAsync` inserts one positive row for the referrer and one for the referred user; `CheckoutService.StartCheckoutAsync`/`CompleteCheckoutAsync` insert the matching negative spend row once Stripe actually confirms payment — never before, and never a real payout, only ever redeemed as a Stripe Coupon amount the same way `LoyaltyService`'s punch-card reward already is.

### DateTime handling

`EcoMealDbContext.OnModelCreating` installs a single `ValueConverter<DateTime, DateTime>` across **every** `DateTime` property in the model (via reflection over `modelBuilder.Model.GetEntityTypes()`), tagging any non-UTC-kind value as `DateTimeKind.Utc` on the way in and out:

```csharp
var utcDateTimeConverter = new ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
```
Npgsql rejects `Kind = Unspecified` for `timestamptz` columns outright — this converter sidesteps that without the app tracking timezones of its own; the DB always stores and returns UTC, and the *viewer's* local time is reconstructed client-side in the frontend. Applying it globally instead of per-property is what keeps every new `DateTime` field (pickup windows, `CreatedAt`, etc.) safe by default without a developer having to remember to opt in.

---

## 4. Repository Pattern

No generic base — each interface is purpose-built, since query shapes differ enough (pagination, business-scoping, `xmin` concurrency) that a generic base would need overrides for almost every method anyway. All "write" repositories follow the same convention: `AddAsync`/`DeleteAsync` only stage the change via `context.Businesses.Add(...)`, and a separate `SaveChangesAsync()` call actually persists it, so a service method can make several repository calls and commit them together in one transaction.

| Repository | Key methods | Notes |
|---|---|---|
| `IBusinessRepository` / `BusinessRepository` | `GetAllAsync`, `GetPagedAsync(search, businessTypeId, staffUserId?, sortBy?, favoritedByUserId?, customerLat?, customerLng?, statusFilter?, publicOnly?, dietaryTag?, maxPrice?)`, `GetByIdAsync`, `GetByStaffUserIdAsync`, `GetStaffAsync`, `IsStaffAsync`, `AddStaffAsync`, `RemoveStaffAsync`, `GetByApiKeyHashAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | `GetPagedAsync`'s search also matches **live packages'** name/description, so searching "bread" surfaces a bakery even if its own name/description doesn't mention bread. `sortBy: "closingSoon"` orders by each business's nearest live `PickupEnd`. `sortBy: "distance"` is the one mode EF/SQL can't express — see the Pagination Helper note below. `dietaryTag`/`maxPrice` narrow to businesses with at least one live package matching. `AddStaffAsync`/`RemoveStaffAsync` return `bool` (false on a duplicate pair or a no-op removal) instead of throwing. `GetByApiKeyHashAsync` backs the webhook intake endpoint (§5/§6) |
| `IPackageTemplateRepository` / `PackageTemplateRepository` | `GetAllAsync`, `GetByBusinessIdAsync`, `GetActiveAsync`, `GetByIdAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | `GetActiveAsync` is the batch load `PackageTemplateGenerationService` (§10) uses every sweep tick |
| `IBusinessTypeRepository` / `BusinessTypeRepository` | `GetAllAsync` (ordered by `Name`), `GetByIdAsync`, `IsInUseAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | Backs admin CRUD over `BusinessType` — `IsInUseAsync` guards deletion |
| `IFavoriteRepository` / `FavoriteRepository` | `GetFavoriteBusinessIdsAsync`, `IsFavoriteAsync`, `AddAsync`, `RemoveAsync`, `GetFavoritingUsersAsync` | `AddAsync`/`RemoveAsync` persist immediately — the one repository that breaks the stage-then-save convention. `GetFavoritingUsersAsync` feeds `PackageService`'s back-in-stock notifications |
| `INotificationRepository` / `NotificationRepository` | `GetRecentByUserIdAsync`, `GetUnreadCountAsync`, `MarkAsReadAsync`, `MarkAllAsReadAsync`, `CreateAsync` | Plain injected `EcoMealDbContext` — same shape as every other repository. `MarkAsReadAsync`/`MarkAllAsReadAsync` use `ExecuteUpdateAsync` — a single `UPDATE ... WHERE` round-trip, no load-then-save |
| `IOrderRepository` / `OrderRepository` | `GetAllAsync`, `GetByUserIdAsync`, `GetByBusinessIdAsync`, `GetPagedByUserIdAsync`, `GetPagedForManagementAsync(search, businessId?, status?)`, `GetInRangeAsync(businessId?, from?, to?)`, `GetByIdAsync`, `HasCompletedOrderAsync`, `GetCompletedPackagesAsync`, `GetTotalWeightSavedKgAsync`, `GetTopRescuersAsync(rangeStart, rangeEndExclusive, take)`, `GetStalePendingOrdersAsync`, `GetOverduePickupOrdersAsync`, `GetPickupReminderCandidatesAsync`, `GetPendingQuantitiesByPackageIdsAsync`, `GetCompletedOrderCountAsync(userId, businessId, rangeStart, rangeEndExclusive)`, `GetSpendInRangeAsync(userId, businessId, packageTypeId?, dietaryTag?, rangeStart, rangeEndExclusive)`, `GetBusinessImpactStatsAsync(businessId, monthStart)`, `GetCompletedOrderCreatedDatesAsync(userId)`, `GetTotalCompletedOrderCountAsync(userId)`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | The busiest repository — see `OrderService` in §5 for how its methods compose. `GetPagedForManagementAsync`'s order-number search strips a leading `#` and substring-matches the plain integer (`EF.Functions.ILike`) because Npgsql can't translate a zero-padded `ToString("000")` into SQL. `GetInRangeAsync` is unpaginated and date-bounded — feeds both the CSV export and the dashboard trend chart. `GetTopRescuersAsync` groups `OrderPackages` by `(Order.UserId, Order.User.Name)`, requiring `Completed`, in-range, and `ShowOnLeaderboard`, all server-side. `GetCompletedOrderCreatedDatesAsync(userId)` feeds `StreakService`'s week-bucketing math. `GetTotalCompletedOrderCountAsync(userId)` is platform-wide — feeds `ReferralService`'s "genuinely their first ever completed order" gate |
| `IPackageRepository` / `PackageRepository` | `GetAllAsync`, `GetPagedAsync(search, businessId?, packageTypeId?)`, `GetByIdAsync`, `GetByIdsAsync`, `GetForAnalyticsAsync(businessId?, since)`, `GetNearExpiryUnclaimedAsync(now, closingBefore)`, `GetLiveCandidatesAsync(dietaryTag?)`, `GetMarkdownCandidatesAsync(businessId?, now, closingBefore)`, `GetSellThroughHistoryAsync(businessId, excludePackageId, since)`, `GetDonationCandidatesAsync(businessId?, now)`, `HasAnyOrdersAsync(packageId)`, `GetDonatedWeightKgAsync(businessId?)`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | `GetByIdsAsync` is the batch-load path `PlaceOrderAsync` uses to re-validate every package referenced in a submitted cart in one query — the bulk-action toolbar reuses it too. `GetAllAsync`/`GetPagedAsync` call `.AsNoTracking()` for pure read/display paths; every write re-fetches through `GetByIdAsync`/`GetByIdsAsync` instead. `GetLiveCandidatesAsync` feeds `BasketPlannerAgent`'s `search_live_packages` tool. `GetMarkdownCandidatesAsync` adds `MarkdownDismissedAt == null` and a wider closing bound. `GetSellThroughHistoryAsync` is server-side filtered to one business's own **closed** packages since a lookback date — feeds `MarkdownPricingAgent`'s `get_sell_through_history` tool. `GetDonationCandidatesAsync(businessId?, now)` is `PickupEnd < now`, `DonatedAt == null`, `!OrderPackages.Any()` — feeds both the manager UI's "mark as donated" prompt and the sweep's notification pass. `HasAnyOrdersAsync(packageId)` is `MarkAsDonatedAsync`'s own server-side re-check of that guard |
| `IPackageTypeRepository` / `PackageTypeRepository` | `GetAllAsync` (ordered by `Name`), `GetByIdAsync`, `IsInUseAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | Same write side and `IsInUseAsync` reasoning as `IBusinessTypeRepository` above, over `Package.PackageTypeId` instead |
| `IReviewRepository` / `ReviewRepository` | `GetAllAsync`, `GetByBusinessIdAsync`, `GetByBusinessIdsAsync`, `GetByUserAndBusinessAsync`, `AddAsync`, `SaveChangesAsync` | `GetByBusinessIdsAsync` is the batch path the business-listing page uses to load ratings for an entire page of cards in one query. `GetByBusinessIdAsync` also `.Include(r => r.Package)` for the package-name pill on a business's review list |
| `IAuditLogRepository` / `AuditLogRepository` | `AddAsync`, `GetPagedAsync(action?, targetType?, search?)` | Plain injected `EcoMealDbContext` — writes are infrequent admin actions. `search` matches `ActorName`/`TargetName` via `EF.Functions.ILike` |
| `IReportRepository` / `ReportRepository` | `AddAsync`, `GetByIdAsync`, `GetByStatusAsync`, `SaveChangesAsync` | Both read methods `.Include(r => r.Reporter)` — the one navigation this entity actually has, since `TargetType`/`TargetId` are polymorphic with no FK to `Include` |
| `IPushSubscriptionRepository` / `PushSubscriptionRepository` | `GetByUserIdAsync`, `AddAsync`, `RemoveByEndpointAsync` | `AddAsync`/`RemoveByEndpointAsync` persist immediately, same shape as `IFavoriteRepository` |
| `IStandingOrderRepository` / `StandingOrderRepository` | `GetByUserIdAsync`, `GetByIdAsync`, `GetActiveByBusinessIdAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync` | `GetActiveByBusinessIdAsync` (`.Include(s => s.User)`) is the one method `StandingOrderService.MatchNewPackagesAsync` calls per distinct business in a generated batch, cached in a local dictionary rather than re-queried per package |
| `IKitchenTipRepository` / `KitchenTipRepository` | `GetVisibleByBusinessIdAsync`, `GetByIdAsync`, `GetSnippetsByIdsAsync`, `AddAsync`, `SaveChangesAsync` | `GetVisibleByBusinessIdAsync` filters `!IsHidden` itself and `.Include(t => t.User)`, newest first. `GetSnippetsByIdsAsync` truncates `Tip` to 40 chars for a batch of IDs — the moderation-queue name-resolution role `IBusinessService`/`IPackageService.GetNamesByIdsAsync` play for the other two target types |
| `IReferralRepository` / `ReferralRepository` | `GetPendingByReferredUserIdAsync`, `GetByReferrerUserIdAsync`, `AddAsync`, `SaveChangesAsync` | `GetPendingByReferredUserIdAsync` is `WHERE ReferredUserId = @id AND RewardedAt IS NULL` — relies on the unique index on `ReferredUserId` to mean at most one row ever matches |
| `IStoreCreditRepository` / `StoreCreditRepository` | `GetBalanceAsync`, `GetByUserIdAsync`, `AddAsync`, `SaveChangesAsync` | `GetBalanceAsync` is `SUM(Amount)` for a user, recomputed on every call rather than cached |
| `IBrandRepository` / `BrandRepository` | `GetAllAsync`, `GetByIdAsync`, `IsInUseAsync`, `AddAsync`, `DeleteAsync`, `SaveChangesAsync`, `GetPublicLocationsAsync(brandId)`, `GetFavoriteBrandIdsAsync`, `IsFavoriteAsync`, `AddFavoriteAsync`, `RemoveFavoriteAsync` | Same write-side/`IsInUseAsync` shape as `IBusinessTypeRepository` above, over `Business.BrandId` instead. `GetPublicLocationsAsync` applies the same `Status == Approved && !IsHidden` filter `BusinessRepository`'s `publicOnly` flag does. The four favorite methods mirror `IFavoriteRepository` exactly, just keyed on `BrandId` — folded into this repository rather than a separate `IBrandFavoriteRepository` |

### DI Registration (Program.cs)

Every repository is registered `AddScoped<TInterface, TImplementation>()` directly in `Program.cs`, right alongside its matching service — e.g. `builder.Services.AddScoped<IBusinessRepository, BusinessRepository>(); builder.Services.AddScoped<IBusinessService, BusinessService>();`. There is no `IDbContextFactory` registration anywhere — `builder.Services.AddDbContext<EcoMealDbContext>(options => options.UseNpgsql(connectionString))` is the only `DbContext` registration, giving every repository (including `NotificationRepository`, which under the previous Blazor Server host needed a factory to dodge per-circuit `DbContext` races — see the note on that class's history below) one `DbContext` scoped to the current HTTP request, same as everything else.

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

    // Maps every item in an already-materialized page to a different shape (e.g. entity → DTO)
    // without re-querying or re-paging.
    public PaginatedList<TResult> MapItems<TResult>(Func<T, TResult> map);
}
```
One `CountAsync` + one `Skip/Take` `ToListAsync`. The second overload exists specifically for `UserService.GetPagedAsync` — its query is a `join` producing an anonymous type (EF can't `OrderBy` a query already projected through a `record` constructor), so it orders/pages the anonymous shape and maps to `UserWithRole` only after materializing.

The third, synchronous `Create` overload backs `BusinessRepository.GetPagedAsync`'s `BusinessSortOptions.Distance` sort — Haversine distance to a runtime (customer) point isn't something Npgsql translates to SQL, so that one sort mode materializes the filtered-but-unsorted `IQueryable` in full, orders it in memory via `Models.GeoDistance.Km` (plain lat/lng trig, no external dependency), then hands the already-ordered `List<Business>` to this method for the same `Skip/Take` page-slicing every other sort gets. Fine at this dataset's size; would need revisiting (e.g. a PostGIS extension) at real scale.

`MapItems` is what every paged controller action calls to turn a `PaginatedList<Entity>` into a `PaginatedList<Dto>` before it leaves the process (`page.MapItems(OrderDto.FromEntity)`) — see §6.

---

## 5. Service Layer

Every service is `Scoped`, with three deliberate exceptions: `OrderLifecycleSweepService`, `PackageTemplateGenerationService`, and `NearExpiryNudgeSweepService` are `BackgroundService`s (effectively singletons — see §10). `IPackageStockNotifier` (`SignalRPackageStockNotifier`, in `Api/Services/`) is registered `AddSingleton` — a thin wrapper over `IHubContext<StockHub>`, which SignalR itself manages as a singleton, so there's no per-request state to scope here anyway (§9).

| Service Interface | Implementation | Responsibilities |
|---|---|---|
| `IAuthService` | `AuthService` | Thin wrapper over ASP.NET Identity's `UserManager`/`SignInManager` — `RegisterAsync` (self-registration always lands in the `Customer` role; only an admin can promote from there; an optional `referralCode` is forwarded to `IReferralService.RegisterReferralAsync` after the account/role are created, best-effort), `ConfirmEmailAsync`/`RequestPasswordResetAsync`/`ResetPasswordAsync`/`ResendConfirmationEmailAsync` for the email-confirmation and password-reset flows `Identity:RequireConfirmedAccount` unlocks (§7/§12), `ChangePasswordAsync(userId, currentPassword, newPassword)`. Login itself is handled directly in `AuthController` (§6) since it needs `SignInManager.CheckPasswordSignInAsync` plus `IJwtTokenService` to mint a token, not just `AuthService`. Builds absolute links for confirmation/reset emails off `App:BaseUrl` (the frontend's own origin — see §12), since neither a background sweep nor a stateless API request reliably has one to derive from otherwise |
| `IJwtTokenService` | `JwtTokenService` (`BusinessLogic`) | `GenerateTokenAsync(user)` — builds the signed JWT issued on login/register (§7): `NameIdentifier`/`Name`/`Email` claims, every role the user holds, and a `security_stamp` claim copied from `ApplicationUser.SecurityStamp` (rotating it via `UserManager.UpdateSecurityStampAsync` first if the user has never had one). Reads `Jwt:Key`/`Jwt:Issuer`/`Jwt:Audience`/`Jwt:ExpiresInDays` (default 7) from configuration and throws if the key is missing or under 32 characters (256 bits) — the same check `Program.cs` makes at startup, duplicated here since this is the other place that key is used |
| `IAppEmailSender` | `SmtpEmailSender` | One method, `SendEmailAsync(toEmail, subject, htmlBody)`, over `System.Net.Mail.SmtpClient`. Deliberately **not** ASP.NET Identity's `IEmailSender<TUser>`. Reads `Email:Smtp:*`/`Email:From*` straight off `IConfiguration`; when `Email:Smtp:Host` isn't set, it logs the email instead of sending — the same "optional infra, degrade gracefully" pattern `DbSeeder` uses for `SeedAdmin` |
| `IBusinessService` | `BusinessService` | CRUD (create/delete admin-only, update admin-or-own-staff, via `IsStaffAsync`) + `AddStaffAsync`/`RemoveStaffAsync` (admin-only) + `GetByStaffUserIdAsync`/`GetStaffAsync`/`IsStaffAsync` (the read side other services build on). `ApplyAsync` (any signed-in user — self-service business signup, born `PendingApproval`), `ApproveAsync`/`RejectAsync` (admin-only) and `HideAsync`/`UnhideAsync` (admin-only moderation) — all four log to `IAuditLogService` and notify the affected submitter/staff. Every write method logs its own `AuditLog` entry |
| `IBusinessTypeService` / `IPackageTypeService` | `BusinessTypeService` / `PackageTypeService` | Reads are pass-throughs; writes are admin-only (`ICurrentUser` + a shared admin-check pattern). `DeleteAsync` first checks `IsInUseAsync` and throws `InvalidOperationException` (mapped to `409 Conflict` by the exception middleware, §8) if any `Business`/`Package` still references the type — both FKs are required relationships with EF Core's convention-default `Cascade`, and an unguarded delete would silently take every business/package of that type down with it |
| `IImageUploadService` | `ImageUploadService` | `SaveAsync(content, originalFileName, subfolder)` — called by `UploadsController` (§6), which is already `[Authorize]` and allow-lists `subfolder` to `businesses`/`packages`. Validates the extension against `Constants.ImageUpload.AllowedExtensions` (throws `InvalidOperationException`) and streams the file to `wwwroot/uploads/{subfolder}/{new Guid}{ext}` via `IWebHostEnvironment.WebRootPath`, returning that path as the `/uploads/...` URL to store in `Package.ImageUrl`/`Business.ImageUrl`. See §8 for how `/uploads` is actually served |
| `IPackageAiAssistant` | `PackageAiAssistant` (`Services/AI/`) | One method, `DraftDescriptionAsync(name, packageTypeName, dietaryTags, cancellationToken)`, over an injected `IChatClient?` (`Microsoft.Extensions.AI`, backed by `OllamaSharp`'s `OllamaApiClient` against a self-hosted Ollama instance — no paid/hosted API anywhere in this path). No tool-calling, no JSON schema — a single `GetResponseAsync` call with a plain prompt, then `.Text.Trim()`. `chatClient` defaults to `null` and is only resolved from DI when `Ollama:BaseUrl` is configured (`Program.cs`), so this throws a friendly `InvalidOperationException` instead of a null-reference when AI isn't set up — same degrade-gracefully convention as `StripeGateway.EnsureConfigured` |
| `ISearchIntentParser` | `SearchIntentParser` (`Services/AI/`) | `ParseAsync(utterance, previousIntent?, cancellationToken)` turns a shopper's free-text query into a `SearchIntent` (`Keywords`/`DietaryTag`/`MaxPrice`/`ClosingSoon`/`NearMe`) the frontend applies against `BusinessesController.GetPaged`'s existing filters. Uses `ChatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema<RawSearchIntent>()` with `Temperature = 0` for deterministic structured output. The raw deserialized shape is a private `RawSearchIntent`, kept separate from the public `SearchIntent` so nothing unvalidated can leak through: `Validate()` re-checks `DietaryTag` against `Constants.DietaryTags.All` case-insensitively and clamps `MaxPrice` to positive values. `previousIntent`, when given, is serialized into the prompt as explicit context rather than replayed as chat history |
| `INearExpiryNudgeComposer` | `NearExpiryNudgeComposer` (`Services/AI/`) | One method, `ComposeAsync(packageName, businessName, quantity, timeUntilClose, matchedDietaryTag?, cancellationToken)` — pure text generation over the same injected `IChatClient?`. `matchedDietaryTag`, when given, is folded into the prompt rather than a separate templated sentence |
| `IBasketPlannerAgent` | `BasketPlannerAgent` (`Services/AI/`) | `ProposeBasketAsync(peopleCount, budget, dietaryTag?, cancellationToken)` — the first AI feature needing Ollama's tool-calling. Split into two chat turns: turn 1 wraps the injected `IChatClient?` with `.AsBuilder().UseFunctionInvocation().Build()` and gives it a genuine `AIFunctionFactory`-built `search_live_packages` tool (a local closure over `IPackageService.GetLiveCandidatesAsync`) — the model calls it as many times as it wants, populating a `candidatePool`; turn 2 goes back to the *plain* `chatClient` (tools off) with `ChatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema<RawBasketPlan>()`. `BuildValidatedPlan` then re-validates every field against `candidatePool` instead of trusting the JSON: an unresolvable `packageId` is dropped, `Quantity` is clamped to real stock, `LineTotal`/`TotalPrice` are recomputed from the real `Package.Price`. Two app-specific rules are enforced regardless of what the prompt asked for: a single-business-per-basket constraint (survivors spanning more than one `BusinessId` get trimmed to the higher-value kitchen, with a note appended) and a budget ceiling (items dropped from the end until the total fits) |
| `IMarkdownPricingAgent` | `MarkdownPricingAgent` (`Services/AI/`) | The highest-value/highest-risk `Services/AI/` class — a bad suggestion costs a manager real revenue, so it's never applied automatically, only shown as a dismissable suggestion. `SuggestMarkdownAsync(package, history, cancellationToken)` takes an already-fetched, already-authorized history list rather than querying anything itself. Same two-turn shape as `IBasketPlannerAgent`: turn 1 gives the model a `get_sell_through_history` tool over the pre-fetched list; turn 2 asks for `ForJsonSchema<RawMarkdownSuggestion>()`. `BuildValidatedSuggestion` discards a `SuggestedPrice` that isn't strictly below the current price, and clamps anything below `MarkdownSettings.MinPriceFraction` (30%) of the current price up to that floor |
| `INearExpiryNudgeService` | `NearExpiryNudgeService` | `SweepAsync()` — called by `NearExpiryNudgeSweepService` (§10), not by any controller. Loads packages closing within `PackageNudgeSettings.ClosingSoonWindow` (30 minutes) that still have stock and haven't been nudged yet, then for each one builds an audience from favoriters ∪ past `Completed`-order customers at that business — an AI-scored trigger instead of a hand-coded rule, unlike `PackageService.NotifyFavoritingCustomersAsync`. The audience is grouped by whichever of the package's `DietaryTags` also shows up in that customer's own completed-order history at the same business, so `INearExpiryNudgeComposer` is called once per distinct match, not once per customer. Marks `Package.NearExpiryNudgeSentAt` whether or not there was anyone to notify |
| `IFavoriteService` | `FavoriteService` | Customer-only, always scoped to the signed-in user. `ToggleFavoriteAsync` returns the new state so the caller doesn't need a second read |
| `INotificationService` | `NotificationService` | Scoped to the signed-in user for every read method; `CreateAsync(userId, message, url?)` is the one exception — other services call it to notify *someone else*. Also fires a best-effort web push via `IPushSubscriptionService` |
| `IPushSubscriptionService` | `PushSubscriptionService` | `SubscribeAsync`/`UnsubscribeAsync` scoped to the signed-in caller, same shape as `IFavoriteService`; `GetPublicKey()` hands the frontend the VAPID public key or `null` if push isn't configured; `SendToUserAsync(userId, message, url?)` is `NotificationService`'s one caller |
| `IWebPushGateway` | `WebPushGateway` | Thin wrapper over the `WebPush` NuGet package (VAPID-signed Web Push), same shape/degrade-gracefully pattern as `IStripeGateway` — except unlike Stripe/SMTP this needs no external account at all, just a self-generated EC key pair, so it ships pre-configured for local dev (§12). `SendAsync` returns `false` only on an HTTP 404/410 from the push service — any other failure is logged and swallowed |
| `IPackageService` | `PackageService` | CRUD, admin-or-own-business-manager on writes (`EnsureCanManageBusinessAsync`). `AddAsync`/`UpdateAsync` also notify (bell + email) every customer who's favorited the package's business when it's created in stock or restocked from `0`. `DuplicateManyAsync`/`AdjustQuantityManyAsync`/`ExtendPickupWindowManyAsync` back the bulk-action toolbar, each authorizing every affected package's business via `EnsureCanManageBusinessesAsync` before mutating. `GetForAnalyticsAsync(businessId?, since)` backs the Business Analytics card — admin-only for a `null` businessId, otherwise requires `IsStaffAsync` on the requested one. `HideAsync`/`UnhideAsync` — same `EnsureCanManageBusinessAsync` authorization, logs to `IAuditLogService`. `GetMarkdownCandidatesAsync`/`GetMarkdownSuggestionAsync`/`DismissMarkdownSuggestionAsync` — same admin-null/staff-scoped shape, hands the agent its data without exposing authorization/fetching concerns to it. `GetDonationCandidatesAsync`/`MarkAsDonatedAsync`/`NotifyDonationCandidatesAsync` — same shape again for the donation workflow (§3 Package). `AddFromWebhookAsync` — the webhook-specific entry point, see the `WebhookIntakeService` deep dive below |
| `IPackageTemplateService` | `PackageTemplateService` | Same admin-or-own-business-manager authorization shape as `IPackageService`. `CreateFromPackageAsync`, `SetActiveAsync`, `DeleteAsync` are the manager-facing CRUD; `GenerateDueInstancesAsync` is the one method with **no** current-user check — system-triggered by `PackageTemplateGenerationService` (§10) |
| `IReviewService` | `ReviewService` | `GetContextAsync(businessId)` → `ReviewContext(CanReview, MyReview, ReviewablePackages)` — `CanReview` is true once the customer has a completed order with the business, and only then is `ReviewablePackages` populated; `SubmitAsync(businessId, rating, comment, packageId?)` updates an existing review in place if one exists, otherwise inserts, and re-validates `packageId` against that same reviewable set |
| `IUserService` | `UserService` | Admin-only on every method. `UpdateRoleAsync` refuses to demote the platform's last remaining admin, and auto-releases whatever business a user staffed if they're moved away from `BusinessManager`. Logs a `RoleChanged` `AuditLog` entry on every successful change |
| `IAuditLogService` | `AuditLogService` | `LogAsync(action, targetType, targetId?, targetName, details?)` — called from *inside* `BusinessService`/`PackageService`/`UserService`/`ReportService` after a mutation succeeds, never exposed as a standalone write endpoint. `GetPagedAsync` (admin-only) backs the audit log page |
| `IReportService` | `ReportService` | `SubmitAsync` — open to any signed-in user. `GetOpenAsync`/`DismissAsync`/`TakeActionAsync` — admin-only. `GetOpenAsync` batch-resolves every report's target name via `IBusinessService`/`IPackageService`/`IKitchenTipService.GetNamesByIdsAsync` rather than one `GetByIdAsync` per report. `TakeActionAsync` delegates to the matching target service's `HideAsync` rather than mutating the target itself |
| `ICheckoutService` | `CheckoutService` (`Services/Payments/`) | Owns the "pay before the order exists" flow — see the deep dive below |
| `IImpactService` | `ImpactService` | Backs the public impact page. `GetMonthlyLeaderboardAsync(take = 20)` needs no auth — same "public aggregate, no per-user data exposed" reasoning as `OrderService.GetTotalKgSavedAsync` — and computes `[monthStart, monthStart.AddMonths(1))` in UTC before delegating to `IOrderRepository.GetTopRescuersAsync`. `GetMyOptInStatusAsync`/`SetMyOptInStatusAsync(bool)` read/write the current signed-in user's own `ApplicationUser.ShowOnLeaderboard` via `UserManager<ApplicationUser>`. `GetBusinessWidgetStatsAsync(businessId)` backs the public per-business impact widget (§8) — `null` unless the business exists **and** `Status == Approved` **and** `!IsHidden`, otherwise combines order-pickup kg with donated-package kg into one `BusinessImpactWidgetDto`, running the total through `Constants/ImpactEquivalency.cs`'s `Co2eKg`/`KmNotDriven`/`LitersOfWater` helpers |
| `IStripeGateway` | `StripeGateway` (`Services/Payments/`) | Thin wrapper over the Stripe SDK (`CreateCheckoutSessionAsync`, `GetSessionStatusAsync`, `RefundAsync`). Kept separate from `ICheckoutService` so `OrderService` (needs to issue refunds) doesn't have to depend on the order-creation side of checkout, and `ICheckoutService` (needs to place orders) doesn't have to depend back on `OrderService`'s refund path. Reads `Stripe:SecretKey`/`Stripe:Currency`; `EnsureConfigured` turns a missing key into "payments aren't configured yet" instead of a raw Stripe SDK exception. `CreateCheckoutSessionAsync` takes optional `discountAmount`/`discountLabel` — when given, it creates a one-off Stripe `Coupon` (`AmountOff`, `Duration = "once"`) and attaches it through `SessionCreateOptions.Discounts`, since Stripe's `price_data.unit_amount` can't go negative |
| `IRescueCircleService` | `RescueCircleService` | Owns the "shared basket, split payment" flow — see the deep dive below |
| `ILoyaltyService` | `LoyaltyService` | Deliberately stateless — `GetMyProgressAsync(businessId)` and `EvaluateDiscountAsync(userId, businessId)` (system-facing, no current-user check — `CheckoutService` already knows who's checking out) both recompute a customer's this-calendar-month `Completed`-order count on every call rather than tracking a counter that could drift. `EvaluateDiscountAsync` checks whether `completedThisMonth + 1` lands on a `LoyaltyPunchThreshold` multiple |
| `IStandingOrderService` | `StandingOrderService` | Customer-only CRUD plus one system-facing method, `MatchNewPackagesAsync(generatedPackages)` — called only by `PackageTemplateGenerationService` (§10) right after a sweep tick actually generates something |
| `IKitchenTipService` | `KitchenTipService` | `GetVisibleByBusinessIdAsync`/`GetSnippetsByIdsAsync` are plain pass-throughs. `SubmitAsync(businessId, tip)` — customer-only, trims and clamps to 200 chars. `HideAsync(tipId, reason, notify)`/`NotifyHiddenAsync` mirror `IBusinessService`/`IPackageService`'s own shape |
| `IReferralService` | `ReferralService` | `GetMyReferralInfoAsync()` — lazily generates `ApplicationUser.ReferralCode` on first call and returns it alongside the caller's store-credit balance and referral history. `RegisterReferralAsync(referralCode?, referredUserId)` — best-effort, called from `AuthService.RegisterAsync`. `TryRewardFirstCompletionAsync(userId)` — system-facing, called from `OrderService.ApplyStatusChangeAsync`'s `Completed` branch. `GetAvailableBalanceAsync(userId)`/`DebitAsync(userId, amount, orderId)` — system-facing, `CheckoutService`'s two hooks into the ledger |
| `IBrandService` | `BrandService` | CRUD is admin-only, same reasoning as `IBusinessTypeService`/`IPackageTypeService`. `GetPublicDetailAsync(id)` is the one read with real logic. `GetMyFavoriteBrandIdsAsync`/`ToggleFavoriteAsync` are customer-only, same shape as `IFavoriteService` |
| `IStreakService` | `StreakService` | Deliberately stateless, same reasoning as `ILoyaltyService`. `GetStreakWeeksAsync(userId)` buckets completed-order dates into Monday-start weeks and walks backward counting consecutive ones, starting from *last* week instead of *this* week whenever the current week has no `Completed` order yet |
| `IWebhookIntakeService` | `WebhookIntakeService` | The one service with no `ICurrentUser` dependency at all — see the deep dive below |
| `ICurrentUser` | `HttpCurrentUser` (`Api/Services/`) | See §7 |
| `IPackageStockNotifier` | `SignalRPackageStockNotifier` (`Api/Services/`) | See §9 |
| `IOrderService` | `OrderService` | The biggest service — see the deep dive below |

### ICurrentUser

```csharp
public interface ICurrentUser
{
    Task<(bool IsAdmin, string? UserId)> GetCurrentUserAsync();
    Task<bool> IsInRoleAsync(string role);
}
```
Defined in `BusinessLogic/Services/Interfaces/`, implemented by `HttpCurrentUser` in `Api/Services/` — a trivial wrapper over `IHttpContextAccessor.HttpContext.User`, the real `ClaimsPrincipal` the JWT Bearer middleware populates for every authenticated request (§7). This is the one seam that lets `BusinessLogic` stay host-agnostic: every service that needs "who is calling, and are they an admin" takes a constructor dependency on `ICurrentUser` rather than on `HttpContext` directly, so the architecture test in §1 can guarantee `BusinessLogic` never references anything ASP.NET-Core-component-model-specific. Most services layer their own admin-only guard directly on top of `ICurrentUser.GetCurrentUserAsync()` rather than through a shared helper method, since the check itself is a one-line `if (!isAdmin) throw new UnauthorizedAccessException(...)`.

### OrderService — deep dive

`OrderService` owns every rule around placing, confirming, completing, cancelling, marking-no-show, and reporting on orders. Its constructor pulls in `IOrderRepository`, `IPackageRepository`, `IBusinessService`, `INotificationService`, `IAppEmailSender`, `IStripeGateway` (refunds only — issuing a charge is `CheckoutService`'s job), `IAuditLogService`, `IReferralService` (the `Completed`-transition reward hook below), the raw `EcoMealDbContext` (for ad-hoc queries that don't warrant a repository method), `ICurrentUser`, `IConfiguration` (for `App:BaseUrl`), `IPackageStockNotifier` (§9), and `ILogger<OrderService>`.

**`PlaceOrderAsync(businessId, lines, logisticsNote?)`** — customer-only:
1. Resolves the caller via `ICurrentUser`; throws `UnauthorizedAccessException` if not signed in or not a `Customer`.
2. **Rate limit**: counts the caller's own orders with `CreatedAt >= UtcNow - OrderRateLimit.Window` (10 minutes); at `OrderRateLimit.MaxOrdersPerWindow` (5) or more, throws `InvalidOperationException` naming the limit and window.
3. For each requested line, loads the `Package` and checks `line.Quantity <= package.Quantity - pendingElsewhere`, where `pendingElsewhere` is the sum of **other customers'** `Pending`-order quantities against that same package:
   ```csharp
   var pendingElsewhere = await dbContext.OrderPackages
       .Where(op => op.PackageId == package.Id && op.Order.Status.Name == OrderStatuses.Pending)
       .SumAsync(op => (int?)op.Quantity) ?? 0;
   ```
   This is the mechanism that prevents overselling **before** confirmation — `Package.Quantity` itself only drops once an order is actually confirmed, so without this check two customers could both "successfully" place a Pending order for the last unit.

   The same reservation math is also exposed **read-only** for the storefront's "X left" display — `GetPendingReservedQuantitiesAsync(packageIds)` (no auth check, same public audience as browsing packages) bulk-sums Pending quantity per package for a batch of IDs.
4. Inserts the `Order` (status `Pending`, `OrderNumber` left for the DB sequence) with its `OrderPackage` rows and `LogisticsNote` (trimmed and clamped, `null` if blank), and notifies every one of the business's staff of a new order needing confirmation.

**Business-scoped reads/writes take an explicit `businessId`.** Since a manager can staff more than one business (`BusinessStaff`), there's no single implicit "their business" — `GetOrdersForManagementAsync`, `GetOrdersForManagementPagedAsync`, and `GetOrdersInRangeAsync` all take `Guid? businessId` from the caller. Two private helpers keep that resolution in one place: `GetOwnedOrderAsync(orderId)` checks `IBusinessService.IsStaffAsync(order.BusinessId, userId)` against the order's own business; `ResolveManagerBusinessIdAsync(userId, requestedBusinessId)` requires the caller to pass a `businessId` and rejects it with `UnauthorizedAccessException` if the manager isn't staff there. Admins skip both checks.

**`ApplyStatusChangeAsync(order, statusName)`** — the single choke point shared by manager/admin status changes (`UpdateStatusAsync`) *and* customer self-cancellation (`CancelMyOrderAsync`):
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
- **Pending → Confirmed**: re-checks every line's requested quantity against the package's *current* `Quantity`, then decrements it. A concurrent confirm of the same package by another manager races on the `xmin` row-version and surfaces as "Stock for this order just changed — please refresh and try again." If the order has a `RescueCircle`, this transition first refuses to proceed unless every participant slot is paid; once that holds, the usual single default `OrderPickupPass` is skipped in favor of one pass per participant.
- **Confirmed → NoShow is guarded by the same "pickup window fully closed" rule the automatic sweep uses**: a manager marking this by hand at the counter can't do it early.
- **Confirmed → Cancelled restores stock; Confirmed → NoShow (this manual path) does not.** The business already prepared and held the stock, so releasing it back into `Package.Quantity` would let the same physical unit be sold to someone else. Pending → Cancelled needs no stock restoration either, since a Pending order never touched `Package.Quantity`.
- **Refund on Cancelled, never on NoShow**: any transition to `Cancelled` calls `RefundIfPaidAsync`, which looks up the order's `Succeeded` `Payment` and issues a real Stripe refund before flipping it to `Refunded`. `NoShow` deliberately skips this — the un-reversed charge **is** the no-show fee. A failed/unconfigured Stripe call flips `Payment.Status` to `RefundFailed` instead of silently looking like a normal `Paid` order. A Rescue Circle order has no single charge to refund — `RefundIfPaidAsync` checks for a `RescueCircle` first and, if there is one, refunds whichever participants actually paid individually, flips the circle's own `Status` to `Cancelled`, and updates its summary `Payment` row.
- Every successful transition logs an `AuditLog` entry and fires a customer-facing notification (bell + best-effort email).
- On a successful transition to `Completed`, also calls `IReferralService.TryRewardFirstCompletionAsync(order.UserId)`.

**`ExpireStalePendingOrdersAsync()`/`ExpireNoShowOrdersAsync()`/`SendPickupRemindersAsync()`** — all three system-triggered (no current-user check), called from `OrderLifecycleSweepService` (§10). See that section for the sweep's own description of each.

**`GetOrdersInRangeAsync(from?, to?, businessId?)`** — unpaginated, date-bounded, scoped the same way `GetOrdersForManagementPagedAsync` is. Backs both the CSV export (`ExportsController`) and the dashboard trend chart.

**`GetTotalKgSavedAsync()`** — the one genuinely public read (no auth check at all): sums `Quantity * WeightKg` across every `OrderPackage` on a `Completed` order, platform-wide, **plus** `IPackageRepository.GetDonatedWeightKgAsync(null)` — every donated package's `WeightKg`, platform-wide.

### CheckoutService — deep dive

Owns the "pay before the order exists" flow, bridging the frontend's checkout flow to a real `Order` via Stripe Checkout.

**`StartCheckoutAsync(businessId, lines, logisticsNote?)`** — customer-only:
1. Re-validates every requested line the same way `OrderService.PlaceOrderAsync` step 3 does — a courtesy so nobody pays for stock that's already gone; `PlaceOrderAsync` re-checks this for real once payment actually succeeds, since stock can still move in the time the customer spends on Stripe's page.
2. Inserts a `PendingCheckout` row with the cart's lines serialized to JSON and `logisticsNote` trimmed/clamped the same way `PlaceOrderAsync` does, then calls `IStripeGateway.CreateCheckoutSessionAsync` (one Stripe line item per cart line, `ClientReferenceId` set to the `PendingCheckout.Id`) with success/cancel URLs built off `App:BaseUrl` (the frontend's origin). Before that call, asks `ILoyaltyService.EvaluateDiscountAsync(userId, businessId)` whether this checkout earns a punch-card reward, clamps it below the cart's own subtotal; also asks `IReferralService.GetAvailableBalanceAsync(userId)` for any spendable store credit, clamps it into whatever headroom the loyalty discount left, and folds both into one combined `discountAmount`/label — Stripe only supports one `Coupon` per session. The credit portion actually offered is stamped onto `PendingCheckout.CreditApplied` before the Stripe call, not after.
3. Stamps the returned Stripe session ID back onto the `PendingCheckout` row and returns Stripe's hosted checkout URL for the frontend to redirect the browser to.

**`CompleteCheckoutAsync(pendingCheckoutId, sessionId)`** — called by the frontend's checkout-return page via `PaymentsController`:
1. Loads the `PendingCheckout`; throws `UnauthorizedAccessException` if it belongs to a different signed-in user.
2. **Idempotent by `ConsumedAt`**: if this checkout was already resolved (a page refresh replaying the return), it re-loads and returns the same `Order` instead of processing the payment twice.
3. Re-validates the passed `sessionId` matches the stored one, then asks Stripe for the session's real status rather than trusting anything client-supplied.
4. Once Stripe confirms `IsPaid`, deserializes the parked cart lines and calls `IOrderService.PlaceOrderAsync` for real. **If that throws** (stock vanished, the rate limit was hit while the customer was on Stripe's page) the payment already went through, so it refunds via `IStripeGateway.RefundAsync` instead of keeping a charge for nothing.
5. On success, inserts the matching `Payment` row (`Status = Succeeded`) and stamps `PendingCheckout.ConsumedAt`/`ResultingOrderId`. If `PendingCheckout.CreditApplied` is set, also calls `IReferralService.DebitAsync(userId, creditApplied, order.Id)` here — only now that Stripe has actually confirmed payment.

**`ExpireStalePendingCheckoutsAsync()`** — system-triggered, called by `OrderLifecycleSweepService` (§10). Hard-deletes any `PendingCheckout` still unconsumed past `OrderExpiry.PendingCheckoutTimeout`.

### RescueCircleService — deep dive

Owns the "shared basket, split payment" orchestration. Depends on `IOrderService` (reuses `PlaceOrderAsync` rather than duplicating its rate-limit/stock rules), `IBusinessService`, `IPackageRepository`, `IStripeGateway`, `INotificationService`, the raw `EcoMealDbContext` (queries/writes `RescueCircle`/`RescueCircleParticipant` directly, same "no dedicated repository" choice as `CheckoutService` makes for `PendingCheckout`), `ICurrentUser`, and `IConfiguration`.

**`StartCircleAsync(businessId, lines, participantCount, logisticsNote?)`** — customer-only, becomes the organizer: validates `participantCount` against `Constants.RescueCircles.MinParticipants`/`MaxParticipants` (2–6); prices the split and rejects with `InvalidOperationException` if either share would land under `Checkout.MinChargeableAmount` **before** placing anything; calls `IOrderService.PlaceOrderAsync` for the underlying `Order`; inserts the `RescueCircle` row (`Status = Open`) and the organizer's own `RescueCircleParticipant`; starts the organizer's own Stripe Checkout session for just their share.

**`JoinOrPayAsync(circleId)`** — if the caller already has a participant row, restarts *their own* checkout; otherwise adds them as a new participant and starts their checkout.

**`CompleteShareCheckoutAsync(circleId, participantId, sessionId)`** — idempotent and session-id-validated the same way `CheckoutService.CompleteCheckoutAsync` is. Once every slot is paid, inserts the circle's summary `Payment` row and notifies the organizer.

**`LeaveCircleAsync(circleId)`** — a non-organizer backing out before the circle is fully paid: refunds their own share individually if already paid, then removes their row to free the slot.

**`GetSummaryAsync`/`GetMyCirclesAsync`/`GetDetailAsync`** are the three read paths exposed by `RescueCirclesController`.

### WebhookIntakeService — deep dive

Backs `WebhookController`'s `POST /api/webhooks/packages` (§6) — the one service in the app with no `ICurrentUser` dependency at all, since there's no signed-in user to ask about. Depends only on `IBusinessRepository`, `IPackageTypeRepository`, and `IPackageService`.

**`CreatePackageAsync(apiKey, request)`**:
1. Hashes `apiKey` (`ApiKeyHasher.Hash` — SHA-256, hex-encoded) and looks up the business via `IBusinessRepository.GetByApiKeyHashAsync`; a missing/unrecognized key throws `UnauthorizedAccessException` before anything else runs.
2. Re-checks the same visibility rule the public storefront enforces — `Status != Approved || IsHidden` also throws `UnauthorizedAccessException`.
3. Validates the request body (`Name` non-blank, `Price`/`WeightKg` positive, `Quantity` non-negative, `PickupEnd > PickupStart`, `PackageTypeId` resolves) — each failure throws `ArgumentException`, mapped by the controller to `400 Bad Request` rather than `401`.
4. Builds a `Package` from the request (`WebhookPackageRequest` — a dedicated DTO, not the raw entity, so a caller authenticated only as "this one business" can never set `BusinessId`, `IsHidden`, or anything moderation-related) and calls `IPackageService.AddFromWebhookAsync`.
5. Stamps `Business.WebhookApiKeyLastUsedAt` and saves.

`PackageService.AddAsync` is split into a shared private `PersistNewPackageAsync` (the stock broadcast + "new package available" favoriter notification) plus two public entry points — `AddAsync` (checks `EnsureCanManageBusinessAsync` first) and `AddFromWebhookAsync` (skips it, since `WebhookIntakeService` already authenticated the request a different way) — so a webhook-created package triggers exactly the same downstream effects as one entered through the management UI, with zero duplicated logic. The controller returns a flat `WebhookPackageResponse` record rather than the `Package` entity itself, specifically to avoid serializing a change-tracked entity graph (`Package.Business.Staff[...].Business...`) that would otherwise throw a reference-cycle exception once `NotifyFavoritingCustomersAsync` has loaded the same business (with its `Staff`) into the same `DbContext`.

---

## 6. Controllers & DTOs

Every controller under `NetromEcoMeal.Api/Controllers/` is a plain `[ApiController]` class with real `[HttpGet]`/`[HttpPost]`/`[HttpPut]` actions, mapped by `app.MapControllers()` in `Program.cs`. There is no in-process call path from anywhere to a controller — the frontend is the only caller, and it only ever reaches one over HTTP. Every action takes/returns a DTO (record types in `NetromEcoMeal.BusinessLogic/DTOs/`, namespace `NetromEcoMeal.DTOs`), never an entity — `OrderDto.FromEntity(order)`, `BusinessDto.FromEntity(business)`, and so on are the mapping boundary every controller calls at the edge.

```csharp
// OrdersController.cs
[Route("api/orders")]
[ApiController]
[Authorize]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderDto>> PlaceOrder([FromBody] PlaceOrderRequestDto request)
    {
        var order = await orderService.PlaceOrderAsync(request.BusinessId, request.Lines, request.LogisticsNote);
        return CreatedAtAction(nameof(GetMyOrder), new { orderId = order.Id }, OrderDto.FromEntity(order));
    }
    // ...
}
```
A controller's job is almost entirely translation: call the service, map the result through a DTO, return the right `ActionResult`. Authorization failures, business-rule conflicts, and not-found cases are **not** individually `try/catch`-handled per action — services keep throwing the same `UnauthorizedAccessException`/`InvalidOperationException`/`KeyNotFoundException` they always have, and `ExceptionHandlingMiddleware` (§8) maps them to the right HTTP status uniformly, so controller actions read as a single expression returning `Ok(...)` in the common case. `OrderDto` itself shows the DTO convention: it's a flattened, read-optimized shape (`WeightKg`/`PickupStart`/`PickupEnd` pulled up onto each line so the frontend can compute its own "kg saved"/pickup-window labels without a second endpoint) built by a static `FromEntity` factory, never a direct entity serialization.

### Controller map

| Controller | Route | Area |
|---|---|---|
| `AuthController` | `api/auth` | Login/Register/ConfirmEmail/ForgotPassword/ResetPassword/ChangePassword/ResendConfirmation — issues JWTs |
| `UsersController` | `api/users` | Admin-only user listing and role management |
| `BusinessesController` | `api/businesses` | CRUD, staffing, hours/closures, approval/moderation, public browsing |
| `BusinessTypesController` | `api/business-types` | Lookup CRUD |
| `BrandsController` | `api/brands` | Multi-location grouping CRUD, public detail, favorites |
| `PackagesController` | `api/packages` | CRUD, bulk actions, moderation, markdown/donation workflows |
| `PackageTypesController` | `api/package-types` | Lookup CRUD, plus an `[AllowAnonymous]` discovery route for webhook integrators |
| `PackageTemplatesController` | `api/package-templates` | Recurring-package CRUD |
| `OrdersController` | `api/orders` | Place/manage/status-transition orders, pickup passes, public kg-saved/reserved-quantity reads |
| `ExportsController` | `api/orders` (bolted-on absolute routes) | CSV exports for orders and payments |
| `PaymentsController` | `api/payments` | Stripe Checkout session creation/completion |
| `RescueCirclesController` | `api/rescue-circles` | Shared-basket split-payment flow |
| `ReviewsController` | `api/reviews` | Submit/read reviews |
| `FavoritesController` | `api/favorites` | Toggle/list favorited businesses |
| `NotificationsController` | `api/notifications` | Bell notifications, read/unread |
| `PushSubscriptionsController` | `api/push-subscriptions` | Web Push subscribe/unsubscribe |
| `UploadsController` | `api/uploads` | Business/package photo upload |
| `AuditLogController` | `api/audit-log` | Admin-only audit trail |
| `ReportsController` | `api/reports` | Submit/dismiss/action moderation reports |
| `ImpactController` | `api/impact` | Leaderboard, opt-in, public per-business widget |
| `LoyaltyController` | `api/loyalty` | Punch-card progress |
| `StandingOrdersController` | `api/standing-orders` | "Auto-reserve my usual" CRUD |
| `KitchenTipsController` | `api/kitchen-tips` | Submit/read kitchen tips |
| `ReferralsController` | `api/referrals` | Referral info, store-credit balance/history |
| `StreaksController` | `api/streaks` | Rescue-streak week count |
| `AiController` | `api/ai` | Search-intent parsing, basket planning, description drafting |
| `WebhookController` | `/api/webhooks` | External POS/inventory package intake — `X-Api-Key`, not JWT |

### The real exceptions to "JWT Bearer, same-origin"

A handful of controllers are structured differently because their caller or auth model genuinely differs:

| Controller / action | Why |
|---|---|
| `AuthController.Login`/`Register` | `[EnableRateLimiting("auth")]` — the one pair of actions behind the fixed-window rate limiter (§8); everything else only needs the standard JWT check |
| `ImpactController.GetBusinessWidget` | `[HttpGet("/api/businesses/{businessId:guid}/impact")]` (an absolute route override on an otherwise `api/impact`-rooted controller) + `[AllowAnonymous]` + `[EnableCors("PublicImpactWidget")]` — called cross-origin from `wwwroot/js/impact-widget.js` running on a business's own website, not from the SPA. This is the one real cross-origin consumer in the app, and the only reason the `PublicImpactWidget` CORS policy exists at all (§8) |
| `PackageTypesController`'s public route | `[HttpGet("/api/package-types")]` + `[AllowAnonymous]` next to the ordinary authenticated `GetAll` — lets an external POS/inventory integrator discover valid `PackageTypeId`s before it ever POSTs a package, without a login |
| `WebhookController` | A whole standalone `[AllowAnonymous]` class, authenticated by an `X-Api-Key` header instead of a bearer token — see the `WebhookIntakeService` deep dive in §5. Keeps its own local `try/catch` (predates/sits alongside the global middleware) since `ArgumentException` isn't one of `ExceptionHandlingMiddleware`'s mapped cases and this controller wants a `400` for it specifically |
| `ExportsController` | Returns `File(bytes, "text/csv", ...)` rather than a DTO — the frontend downloads it through its HTTP client with a blob response type, since a JWT can't ride along on a plain browser-navigated `<a href>` link the way a cookie would |
| `UploadsController` | Takes `IFormFile` (`multipart/form-data`), not a JSON body — the one controller doing a raw file upload rather than a DTO round-trip |

---

## 7. Authentication & Authorization

### JWT Bearer, not cookies

There is no cookie-based auth and no antiforgery concept anywhere in this app — the frontend is a separate-origin SPA, so a bearer token in an `Authorization` header is the natural fit, and it means there is nothing for CSRF protection to protect (a cross-site request can't read or attach a token it was never handed). `Program.cs` configures it directly:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context => { /* ?access_token= for /hubs/* only — see §9 */ },
            OnTokenValidated = async context => { /* security_stamp check — below */ },
        };
    });
```
`ApplicationUser` carries Identity's standard roles and `SecurityStamp`; `AddIdentityCore<ApplicationUser>` (not the full `AddIdentity`) plus `.AddRoles<IdentityRole>().AddSignInManager().AddEntityFrameworkStores<EcoMealDbContext>().AddDefaultTokenProviders()` is what gets registered — `AddIdentityCore` because this host has no cookie/Razor-Pages UI for Identity to also wire up, just the pieces the API actually uses (password hashing, roles, `SignInManager.CheckPasswordSignInAsync`, token providers for email confirmation/password reset).

### Login, token issuance, and revocation

`AuthController.Login` calls `SignInManager.CheckPasswordSignInAsync` (not `PasswordSignInAsync` — there's no cookie to set) and, on success, calls `IJwtTokenService.GenerateTokenAsync(user)` to mint the token returned to the frontend. The token carries `NameIdentifier`/`Name`/`Email`/role claims plus a `security_stamp` claim copied from `ApplicationUser.SecurityStamp` at issuance time. JWTs can't be revoked directly once signed, so `OnTokenValidated` is where revocation actually happens:

```csharp
OnTokenValidated = async context =>
{
    var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var tokenStamp = context.Principal?.FindFirst("security_stamp")?.Value;
    if (userId is null || tokenStamp is null) { context.Fail("Invalid token."); return; }

    var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
    var user = await userManager.FindByIdAsync(userId);
    if (user is null || user.SecurityStamp != tokenStamp)
        context.Fail("Token has been invalidated.");
}
```
A password change calls `UserManager.ChangePasswordAsync`, which Identity itself bumps `SecurityStamp` for — every token issued before that moment now carries a `security_stamp` claim that no longer matches the DB, so the very next authenticated request with an old token fails here regardless of how long the token's own expiry still has to run. This is the only revocation mechanism in the app; there is no token blocklist, no short-lived-token-plus-refresh-token rotation — `Jwt:ExpiresInDays` (default 7) is a flat expiry, and a compromised token is only actually cut off early by a password change.

### SignalR's one exception

A browser's native WebSocket API can't set an `Authorization` header on the handshake request, so `@microsoft/signalr` instead appends the token as `?access_token=...`. `OnMessageReceived` reads it from there, but only for `/hubs/*` paths — an ordinary REST call still needs the real header:
```csharp
OnMessageReceived = context =>
{
    var accessToken = context.Request.Query["access_token"];
    if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
        context.Token = accessToken;
    return Task.CompletedTask;
}
```

### Roles

Three fixed roles in `Constants.AppRoles`: `Admin`, `Customer`, `BusinessManager`. Seeded once by `DbSeeder` (`RoleManager<IdentityRole>.CreateAsync` per role if it doesn't already exist). Self-registration (`AuthController.Register`) always lands a new account in `Customer` — the only way to become a `BusinessManager` or `Admin` is for an existing admin to promote them via `UsersController.UpdateRole` (`UserService.UpdateRoleAsync`), which additionally:
- Refuses to demote the platform's **last remaining admin** (counts users in the `Admin` role before allowing a role change away from it).
- Auto-releases every business a user staffed if they're moved to any role other than `BusinessManager` — otherwise a demoted manager would keep phantom `BusinessStaff` rows pointing at an account that can no longer act on them.

### Authorization patterns

| Pattern | Where | Effect |
|---|---|---|
| `[Authorize]` (no role) | Most controllers — `OrdersController`, `FavoritesController`, `NotificationsController`, `UploadsController`, `PaymentsController`, `RescueCirclesController`, … | Any authenticated user; the service layer underneath does the finer-grained role/ownership check |
| `[Authorize(Roles = AppRoles.Admin)]` | `UsersController`, `AuditLogController`, parts of `ReportsController` | Admin-only at the HTTP boundary |
| `[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.BusinessManager}")]` | `ExportsController` | Either management role, narrowed further inside to "admin sees everything, a manager only their own staffed business(es)" |
| `[AllowAnonymous]` on specific actions | `BusinessesController.GetPaged`/`GetById` (public browsing), `ImpactController`'s widget/leaderboard, `OrdersController`'s kg-saved/reserved-quantity reads, `PackageTypesController`'s discovery route, `WebhookController` entirely | Public reads, or (webhook) a different auth model entirely |
| Service-layer `ICurrentUser` check | Every service in §5 | The real enforcement point for ownership/role rules that `[Authorize(Roles = ...)]` alone can't express (e.g. "admin or one of *this specific business's* staff") |

`[Authorize]`/`[AllowAnonymous]` at the controller/action level is the first filter (rejects an unauthenticated or wrong-role caller before any service method runs); the service-layer `ICurrentUser` checks are the real one, since a role attribute alone can't express "this manager, but only for a business they actually staff."

---

## 8. Cross-Cutting Request Pipeline

Everything in this section is configured in one place, `NetromEcoMeal.Api/Program.cs` — there is no second place any of it could silently diverge from.

### Middleware order

```
UseSerilogRequestLogging()
UseMiddleware<ExceptionHandlingMiddleware>()
[Development: MapOpenApi + SwaggerUI]
(migrate + seed, once, before the pipeline starts serving)
[security headers — inline middleware]
UseHsts()                         (non-Development only)
UseHttpsRedirection()
UseStaticFiles(/uploads)
UseStaticFiles(/js)
UseCors("FrontendPolicy")
UseRateLimiter()
UseAuthentication()
UseAuthorization()
MapControllers()
MapHub<StockHub>("/hubs/stock")
```

### Exception-to-status mapping

`ExceptionHandlingMiddleware` is the single place a thrown exception becomes an HTTP response — no controller needs its own `try/catch` for the common cases:
```csharp
var (status, message) = ex switch
{
    UnauthorizedAccessException => (StatusCodes.Status403Forbidden, ex.Message),
    InvalidOperationException => (StatusCodes.Status409Conflict, ex.Message),
    KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
    Stripe.StripeException => (StatusCodes.Status409Conflict,
        "We couldn't process this payment request — please try a different basket total or try again shortly."),
    _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
};
```
Services keep throwing the same exceptions they always have — `UnauthorizedAccessException` from an `ICurrentUser`-backed check, `InvalidOperationException` for business-rule conflicts, `KeyNotFoundException` for "no such row." A raw `StripeException`'s own `Message` is deliberately never forwarded to the client — Stripe's wording isn't meant for an end user. Only a `500` gets logged with the full exception; the rest are expected, user-facing outcomes. If the response has already started streaming, the exception is rethrown rather than trying to rewrite headers that are already sent.

### CORS

Two distinct policies, because there are two distinct kinds of cross-origin caller:
```csharp
options.AddPolicy("PublicImpactWidget", policy => policy.AllowAnyOrigin().WithMethods("GET"));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:5174"];
options.AddPolicy("FrontendPolicy", policy =>
{
    if (builder.Environment.IsDevelopment())
        policy.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader();
    else
        policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
});
```
`PublicImpactWidget` is deliberately wide open (any origin, GET-only) because it's meant to be embedded on arbitrary third-party business websites via `impact-widget.js` — there's no way to know those origins ahead of time, and the route itself (`ImpactController.GetBusinessWidget`) exposes nothing sensitive. `FrontendPolicy` is what every other route runs under: wide open in `Development` (so any local dev port works without configuration), locked to an explicit allowlist (`Cors:AllowedOrigins`, config-driven) everywhere else. `app.UseCors("FrontendPolicy")` applies that policy pipeline-wide; `ImpactController.GetBusinessWidget` opts out with its own `[EnableCors("PublicImpactWidget")]`.

### Rate limiting

One named fixed-window limiter, applied only where brute-forcing matters:
```csharp
options.AddFixedWindowLimiter("auth", limiterOptions =>
{
    limiterOptions.PermitLimit = 10;
    limiterOptions.Window = TimeSpan.FromMinutes(1);
    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    limiterOptions.QueueLimit = 0;
});
options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
```
`AuthController.Login`/`Register` are decorated `[EnableRateLimiting("auth")]` — 10 requests per IP per minute, zero queueing (a request over the limit is rejected immediately with `429`, not held). Nothing else in the app carries this attribute; `OrderService.PlaceOrderAsync`'s own 10-minutes/5-orders rate limit (§5) is a separate, business-logic-level limiter keyed on the signed-in user rather than the IP-keyed middleware one.

### Security headers

An inline middleware (no package) sets the same headers on every response:
```csharp
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    if (!app.Environment.IsDevelopment())
    {
        ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    }
    await next();
});
```
`nosniff`/`X-Frame-Options`/`Referrer-Policy` apply everywhere, including `Development`, since they cost nothing locally. HSTS and a CSP only apply outside `Development` — HSTS makes no sense over plain HTTP on localhost, and a strict `default-src 'self'` CSP would also break the Swagger UI page the Development branch serves at `/swagger`.

### Static file serving

Two always-on `UseStaticFiles` mappings, both registered before `UseCors`/auth so a static asset request never needs a token:
- **`/uploads`** — `wwwroot/uploads`, backing `ImageUploadService`'s saved business/package photos. `WebRootPath` can be `null` in a test host that never published a `wwwroot` folder (`WebApplicationFactory`), so `Program.cs` falls back to `ContentRootPath/wwwroot` and creates the `uploads` directory if it doesn't exist yet. In Docker, this directory is a named volume (`ecomeal-test-uploads`) so uploaded photos survive an image rebuild.
- **`/js`** — `wwwroot/js`, serving exactly one file: `impact-widget.js`. Deliberately hosted by the Api (not the frontend's own static assets), because the widget script infers its own fetch base URL from the `<script src="...">` tag's own origin — a business embeds it as `<script src="{apiOrigin}/js/impact-widget.js" data-business-id="...">`, and serving it from anywhere else would make that inferred origin wrong for the `/api/businesses/{id}/impact` call the widget itself makes.

### Startup migration and seeding

```csharp
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EcoMealDbContext>();
    await dbContext.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}
```
Runs once, synchronously, before the pipeline starts accepting requests — the Api is the only host, so there's exactly one place this needs to happen. See §11 for what `DbSeeder` actually does.

---

## 9. SignalR — Live Stock

`StockHub` (`Api/Hubs/StockHub.cs`) is mapped at `/hubs/stock` and carries no `[Authorize]` — browsing a storefront page needs no login, and the hub's own group messages carry no sensitive data (only "this businessId's stock changed," never quantities or contents):
```csharp
public class StockHub : Hub
{
    public Task JoinBusinessGroup(Guid businessId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(businessId));
    public Task LeaveBusinessGroup(Guid businessId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(businessId));
    public static string GroupName(Guid businessId) => $"business:{businessId}";
}
```
The frontend's business-detail page joins that business's group on mount and leaves on unmount. `IPackageStockNotifier` (`BusinessLogic/Services/Interfaces/`) is the abstraction `OrderService`/`PackageService` call right after any `SaveChangesAsync` around a status/stock change worth refreshing a viewer's page for — confirm/cancel/no-show, both background-sweep expiry paths, and every `PackageService` write. Its one real implementation, `SignalRPackageStockNotifier` (`Api/Services/`), is a thin fire-and-forget wrapper over `IHubContext<StockHub>`:
```csharp
public class SignalRPackageStockNotifier(IHubContext<StockHub> hubContext) : IPackageStockNotifier
{
    public void NotifyBusinessChanged(Guid businessId) =>
        _ = hubContext.Clients.Group(StockHub.GroupName(businessId)).SendAsync("BusinessStockChanged", businessId);
}
```
Registered `AddSingleton` — `IHubContext<T>` is itself a SignalR-managed singleton, so there's no per-request state here to scope, and a failed push isn't worth blocking or failing the request that changed the stock for: the client still sees the change on its own next fetch/reload regardless. This is the same seam `ICurrentUser` is in §5/§7 — the interface lives in `BusinessLogic`, the implementation that needs real ASP.NET Core infrastructure lives in `Api`, so `BusinessLogic` itself never references SignalR.

---

## 10. Background Services

Three `BackgroundService`s, each registered via `AddHostedService<T>()` only when `BackgroundJobs:Enabled` is `true` (the default — see §12):

```csharp
if (builder.Configuration.GetValue("BackgroundJobs:Enabled", false))
{
    builder.Services.AddHostedService<OrderLifecycleSweepService>();
    builder.Services.AddHostedService<PackageTemplateGenerationService>();
    builder.Services.AddHostedService<NearExpiryNudgeSweepService>();
}
```
The Api is the only host, so this is on by default — the flag stays purely as a guard against a future horizontal scale-out: running the Api as multiple replicas would otherwise sweep orders and generate templates once per replica. `BackgroundService` instances are effectively singletons, so none of them can hold a `Scoped` service directly — each creates a fresh DI scope on every tick via `IServiceScopeFactory` instead. API-integration tests set `BackgroundJobs:Enabled=false` (§13) so a sweep never races the tests' own data against a real Postgres.

### OrderLifecycleSweepService

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    using var timer = new PeriodicTimer(OrderExpiry.SweepInterval);
    do
    {
        using var scope = scopeFactory.CreateScope();
        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var checkoutService = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
        var packageService = scope.ServiceProvider.GetRequiredService<IPackageService>();
        await checkoutService.ExpireStalePendingCheckoutsAsync();
        await orderService.ExpireStalePendingOrdersAsync();
        await orderService.SendPickupRemindersAsync();
        await orderService.ExpireNoShowOrdersAsync();
        await packageService.NotifyDonationCandidatesAsync();
    } while (await timer.WaitForNextTickAsync(stoppingToken));
}
```
Runs every `OrderExpiry.SweepInterval` (5 minutes) and does five things in order:
1. **Stale-checkout cleanup** — hard-deletes any `PendingCheckout` still unconsumed past `OrderExpiry.PendingCheckoutTimeout` (30 minutes): nobody was ever charged, so this is pure tidying, not a refund path.
2. **Stale-Pending expiry** — cancels any `Pending` order idle longer than `OrderExpiry.PendingTimeout` (30 minutes) or whose pickup window has already closed, refunding it since it *was* paid for at checkout time. A `Pending` order that's never confirmed still counts against a package's availability via the `pendingElsewhere` check in `OrderService.PlaceOrderAsync` (§5) — without this sweep, an abandoned checkout could tie up stock indefinitely.
3. **Pickup reminders** — emails/notifies `Confirmed` orders closing within `OrderExpiry.PickupReminderLeadTime` (30 minutes) that haven't been reminded yet.
4. **No-show detection** — moves `Confirmed` orders whose pickup window has fully closed to `NoShow`, restoring stock but deliberately *not* refunding. Unlike this automatic path, a manager marking the same transition by hand does **not** restore stock — the two no-show paths aren't symmetric.
5. **Donation-candidate notification** — `PackageService.NotifyDonationCandidatesAsync()` finds every package that closed with zero units ever ordered and hasn't been notified yet, notifies that business's staff once, and stamps `DonationOfferedAt`.

The whole tick body is one `try/catch`, logged and swallowed on failure so a bad tick doesn't take the loop down.

### PackageTemplateGenerationService

Same shape — `IServiceScopeFactory` + `PeriodicTimer`, one `try/catch`-wrapped tick body. Runs every `PackageTemplateGeneration.SweepInterval` (15 minutes):
```csharp
var generated = await templateService.GenerateDueInstancesAsync();
```
`GenerateDueInstancesAsync` loads every active template and, for any whose `LastGeneratedDate` isn't today (UTC), combines its `PickupStartTimeUtc`/`PickupEndTimeUtc` with today's date into a new `Package`, copying every other field straight from the template. If the combined window has already closed by the time a delayed sweep catches it, it stamps the date without generating a dead-on-arrival package. `GenerateDueInstancesAsync` returns the actual generated `List<Package>`, and whenever that list is non-empty the sweep hands it straight to `IStandingOrderService.MatchNewPackagesAsync` — the one place `StandingOrder` matching happens, deliberately not its own timer, since there's nothing to match against except packages this same tick just generated.

### NearExpiryNudgeSweepService

Same shape again. Runs every `PackageNudgeSettings.SweepInterval` (5 minutes) and calls `INearExpiryNudgeService.SweepAsync()` (§5). No per-candidate `try/catch` inside `SweepAsync` itself — if `INearExpiryNudgeComposer` throws (most commonly because `Ollama:BaseUrl` isn't configured), the whole tick's exception is caught here, logged, and nothing gets marked `NearExpiryNudgeSentAt`, so the same candidates are simply reconsidered on the next tick once Ollama is reachable again.

---

## 11. Database Seeding

`DbSeeder.SeedAsync(services, configuration)` runs once at startup, right after `dbContext.Database.MigrateAsync()` in `Program.cs` (§8). It's designed to be **safely re-run on every single startup**, not a one-time migration-adjacent script:

1. **Roles** — creates any of `AppRoles.AllRoles` that don't already exist.
2. **Seed admin & demo accounts** — reads `SeedAdmin:Email`/`SeedAdmin:Password` from configuration for the admin; if either is missing, logs a warning and skips (no admin account is seeded, the app still runs). Unconditionally creates a set of fixed demo customer/manager accounts (fixed passwords, not configuration-gated like the admin account) covering multi-business staffing and the impact leaderboard's opt-in/opt-out demo.
3. **Lookup tables** (`BusinessTypes`, `PackageTypes`, `Statuses`) — insert-only. `BusinessTypes`/`PackageTypes` are skipped entirely once the table has any rows. `Statuses` instead adds whichever of the five fixed names are missing, because a database that's run the old pre-`DbSeeder` migrations already has the original four `Status` rows from a hardcoded `InsertData` by the time this runs — a blanket "any rows exist" guard there would've silently skipped seeding `NoShow` forever (this is exactly the migration-vs-seeder class of bug `Tests/Database/DbSeederTests.cs` exists to catch — see §13, and the project memory on seeding architecture).
4. **Demo businesses & packages** — a fixed set of businesses/packages with **hardcoded GUIDs**, reconciled against what's already in the DB rather than blindly re-inserted: missing rows are added; an existing row's `ImageUrl` is only overwritten if blank or pointing at a retired placeholder host; a package's `PickupStart`/`PickupEnd` are only refreshed if the existing window has already expired, so the storefront always opens with live, orderable packages. Several packages are deliberately anchored to `DateTime.UtcNow` (rather than a fixed hour) so they're always inside a "closing soon"/near-expiry/markdown-candidate/donation-candidate window right after a fresh seed, whatever time of day the app starts. `WeightKg`, `DietaryTags`, geolocation, and loyalty punch-card fields are backfill-only (only set if currently empty/null), since the seeder can't distinguish "never set" from "an admin deliberately cleared it."
5. **Demo business staff** — staffs the demo managers across the demo businesses, demonstrating both shapes of the `BusinessStaff` many-to-many (one staffer across several businesses; several staff at one business).
6. **Demo recurring template** — turns one demo package into a `PackageTemplate` so the recurring-package feature isn't empty on a fresh database; `PackageTemplateGenerationService` (§10) owns that package's future daily instances from there.
7. **Demo business hours & closures** — a full weekly `BusinessHours` schedule per demo business, varied by business type, plus two `BusinessClosure` rows (one covering today, one starting a few weeks out).
8. **Demo customer/manager activity** — **only on a genuinely fresh database** (gated on no `Order` rows existing yet), creates orders spanning every status across the last two weeks, plus favorites, reviews, notifications, and matching `Payment` rows (since every real `Order` now only exists once Stripe confirms payment) — so every feature has real data to look at immediately after a fresh `docker compose up`. Also seeds historical, already-closed packages with partial sell-through so the Business Analytics card has a non-trivial rate to show, and historical completed orders spread across the last four calendar weeks so `StreakService`'s week-walking math shows a live streak.
9. **Approval/moderation demo data** — extra businesses in `PendingApproval`/`Rejected` states, one demo business/package backfilled `IsHidden`, and (fresh-database-only) a matching, internally-consistent set of `Report`/`AuditLog` rows so the approval queue, moderation state, and audit log aren't empty.
10. **Leaderboard demo data** — opts specific demo customers into `ShowOnLeaderboard`, with at least one customer deliberately left opted **out** despite having real completed-order history, so the opt-in filter visibly excludes someone with real activity, not just someone with none.
11. **Rescue Circle demo data** — seeds an in-progress circle (one unpaid seat, one unclaimed seat) and a fully-paid, `Confirmed` one (demonstrating the per-participant `OrderPickupPass`es).
12. **Standing order demo data** — one untyped/untagged standing order and one dietary-tag-narrowed one, at a business whose recurring template (point 6) makes the match loop at `PackageTemplateGenerationService` actually exercisable end to end.
13. **Donation demo data** — backfills one closed, unsold demo package as already donated, so impact aggregates have a non-zero donated contribution immediately, alongside a separate still-actionable donation candidate.
14. **Kitchen tip demo data** — a few tips, one seeded pre-hidden with a matching `Report`/`AuditLog` pair, so moderation demonstrates all three moderatable target types (Business/Package/KitchenTip).
15. **Referral demo data** — one already-rewarded referral (real non-zero store-credit balance visible immediately) and one still-pending referral.
16. **Brand & webhook demo data** — two fixed `Brand` rows grouping existing demo businesses (backfilling `BrandId` only where still `null`), plus a demo business's `WebhookApiKeyHash` stamped with the hash of a fixed, documented plaintext key (`DbSeeder.DemoWebhookApiKey`) so `POST /api/webhooks/packages` has something real to call against immediately — regenerating the key from the management UI permanently retires the seeded demo value for that business, exactly as it would for a real rotated key.

This reconciliation approach — add-if-missing, refresh-if-stale, backfill-if-empty, never overwrite a live/customized value — is what lets the exact same seeder run unconditionally on every container start (`docker-compose.test.yml`) without ever fighting real usage data.

---

## 12. Configuration Reference

| Key | Source | Purpose |
|---|---|---|
| `ConnectionStrings:EcoMealContext` | user-secrets (dev) / `docker-compose.test.yml` env (`ConnectionStrings__EcoMealContext`) | Npgsql connection string |
| `Jwt:Key` | user-secrets / docker-compose env (`Jwt__Key`), gitignored root `.env` via `${JWT_KEY:-dev-only-...}` | HMAC-SHA256 signing key for issued JWTs — must be at least 32 characters; `Program.cs` throws at startup otherwise |
| `Jwt:Issuer` / `Jwt:Audience` / `Jwt:ExpiresInDays` | `appsettings.json` | Fixed to `"NetromEcoMeal"`/`"NetromEcoMeal"`/`7` — no per-environment override needed |
| `SeedAdmin:Email` / `SeedAdmin:Password` | user-secrets / docker-compose env (`SeedAdmin__Email`/`SeedAdmin__Password`) | The one admin account `DbSeeder` creates if it doesn't already exist |
| `Identity:RequireConfirmedAccount` | user-secrets / docker-compose env (`Identity__RequireConfirmedAccount`) | Defaults `false`. When `true`, self-registration requires clicking an emailed confirmation link before `CheckPasswordSignInAsync` succeeds |
| `App:BaseUrl` | user-secrets / docker-compose env (`App__BaseUrl`) | The **frontend's** own origin — used to build links in confirmation/reset emails and Stripe success/cancel redirect URLs. Getting this backwards (pointing it at the Api's own origin instead) silently 404s every Stripe checkout redirect |
| `Frontend:BaseUrl` | docker-compose env (`Frontend__BaseUrl`) / `appsettings.Development.json` | Same frontend-origin value as `App:BaseUrl`, kept alongside it under its own, more self-describing key |
| `Cors:AllowedOrigins` | not set in dev (falls back to `localhost:5173`/`5174`); would be set per-deployment outside Development | Explicit origin allowlist for `FrontendPolicy` outside `Development`, where the policy instead allows any origin |
| `Email:Smtp:Host` / `Port` / `Username` / `Password` / `EnableSsl` | user-secrets / docker-compose env (double-underscore form) | SMTP settings for `SmtpEmailSender`. Leaving `Host` unset makes it log the email instead of sending |
| `Email:FromAddress` / `Email:FromName` | user-secrets / docker-compose env | The `From:` header on outgoing emails |
| `Stripe:SecretKey` | user-secrets / docker-compose env (`Stripe__SecretKey`, sourced from a gitignored root `.env` via `${STRIPE_SECRET_KEY:-}`) | A Stripe **test-mode** secret key (`sk_test_...`). Empty by default — `StripeGateway.EnsureConfigured` then turns any checkout attempt into "payments aren't configured yet" instead of an SDK exception |
| `Stripe:Currency` | user-secrets / docker-compose env (`Stripe__Currency`) | Lowercase ISO currency code passed to Stripe Checkout; defaults to `ron` |
| `WebPush:PublicKey` / `WebPush:PrivateKey` / `WebPush:Subject` | `appsettings.Development.json` / docker-compose env (`WebPush__*`) | A VAPID EC key pair for Web Push, generated once via `WebPush.VapidHelper.GenerateVapidKeys()`. Unlike Stripe/SMTP this needs no external account, so it ships pre-configured with a real (if only locally-meaningful) key pair; leaving it unset makes `IWebPushGateway`'s configured check `false` and the frontend's push-enable toggle hides itself |
| `Ollama:BaseUrl` / `Ollama:ModelId` | user-secrets / docker-compose env (`Ollama__BaseUrl`/`Ollama__ModelId`) | Points at a free, self-hosted Ollama instance. `Program.cs` builds the `OllamaApiClient`'s `HttpClient` itself with a 5-minute `Timeout` rather than the SDK's default 100s — CPU-only local inference can take well over 100s just to process one large prompt. Empty by default; `IChatClient` is only registered when it's set, so every `Services/AI/` class degrades to a friendly "AI features aren't available yet" error otherwise. `ModelId` defaults to `qwen2.5:7b` if unset |
| `BackgroundJobs:Enabled` | `appsettings.Development.json` / docker-compose env, both `true`; test hosts set it `false` | Gates the three `AddHostedService<T>()` registrations in §10 |
| `Serilog:MinimumLevel:Default` / `:Override:*` | `appsettings.json` / `appsettings.Development.json` | Read by `builder.Host.UseSerilog(...)`'s `ReadFrom.Configuration`. Base config sets `Default: Information`; Development overrides it to `Debug`. Both override `Microsoft.AspNetCore`/`Microsoft.EntityFrameworkCore` down to `Warning` |
| `Serilog:WriteTo` | `appsettings.json` | Sink list — a `Console` sink with a compact `[HH:mm:ss LVL] Message` template. Adding a file/aggregator sink is a config-only change plus the matching `Serilog.Sinks.*` package |
| `Logging:LogLevel` | `appsettings.json` | Standard ASP.NET Core logging config; `Microsoft.AspNetCore` pinned to `Warning` |

No `appsettings.Production.json` exists — the only environment-specific file is `appsettings.Development.json`. `docker-compose.test.yml` (repo root) is explicitly a local test/demo harness, not a production deployment: it defines `db` (Postgres 17), `mailpit` (catches every outgoing email, UI at `:8025`), `ollama` (built from `Dockerfile.ollama`, which bakes `qwen2.5:7b` into the image at build time so no manual pull step is needed), `api` (built from `Backend/Dockerfile`, published port `8081`, `App__BaseUrl`/`Frontend__BaseUrl` pointed at the frontend's own published origin `http://localhost:5174` — not the Api's own), and `frontend` (nginx, published port `5174`, `VITE_API_URL` baked into the static bundle at *image build* time since it runs in the browser outside the Docker network). `Stripe__SecretKey` and `Jwt__Key` are sourced from a gitignored root `.env` (see the committed `.env.example`) via `${VAR:-fallback}` substitution — secrets never live in the committed compose file itself; the `Jwt__Key` fallback is a clearly-labeled dev-only value, fine for local use but meant to be overridden by a real secret (`openssl rand -base64 32`) for anything beyond that.

`Program.cs` also sets a fixed `CultureInfo("ro-RO")` as both `DefaultThreadCurrentCulture` and `DefaultThreadCurrentUICulture` at startup — every `ToString("C")` call across the backend formats as RON without any per-call culture handling, since the app has exactly one supported locale.

---

## 13. Automated Tests

`NetromEcoMeal.Tests` is a separate xUnit project referencing all three other projects (so its API-integration tests can boot `WebApplicationFactory<Program>`). As of this writing the suite has **383 tests**. Run everything with `dotnet test Backend/NetromEcoMeal.slnx`.

### Unit tests (`Services/`, `Models/`)

Mocked dependencies (Moq) for collaborators, sometimes backed by a real `EcoMealDbContext` on the EF Core **InMemory** provider (`Tests/TestSupport/InMemoryDb.cs`) when a service queries the context directly for something simple enough for InMemory to translate correctly (rate-limit counts, status lookups, pending-reservation sums). `ICurrentUser` is faked with a small `FakeCurrentUser` test double rather than mocked, since most tests just need it to report a fixed `(IsAdmin, UserId)` pair. Coverage spans every service in §5 — order placement/transitions/stock math, checkout's pay-before-order-exists bridge, Rescue Circle's split-payment orchestration, the bulk package-action toolbar, loyalty/streak/referral/standing-order matching math, the AI agents (with `IChatClient` mocked, never a real Ollama instance — `BasketPlannerAgentTests`/`MarkdownPricingAgentTests` are the two exceptions, where the mocked "model" sits underneath a real `FunctionInvokingChatClient` so a fabricated tool-call response genuinely invokes the real tool), image upload (the one test file touching real disk, pointed at a per-test temp directory), and the webhook intake auth/validation ladder. `Models/BusinessHoursStatusTests.cs` is pure-function coverage with no `DbContext` at all.

### Repository tests (`Repositories/`)

Run against a real `EcoMealDbContext` on the InMemory provider rather than mocks — exercising the repository's own query/persistence logic (`BusinessRepository`'s staffing/hours/closures/dietary-tag/price filters, `OrderRepository`'s leaderboard and business-impact aggregation, `PackageRepository`'s donation-candidate query) independent of the service-layer authorization the matching `*ServiceTests.cs` files cover with the repository mocked out instead.

### API-integration tests (`Api/`)

Boot the **real** `Api` host via `ApiFactory : WebApplicationFactory<Program>` — the actual `Program.cs` (migrations, seeding, full DI graph, JWT auth, rate limiting, CORS, the exception middleware) running against an isolated Postgres database from `PostgresFixture` (Testcontainers). `ApiFactory.ConfigureWebHost` uses `UseSetting` (not `ConfigureAppConfiguration`) to inject the test connection string/JWT key/etc. — `Program.cs` reads `builder.Configuration` before `WebApplicationFactory`'s own deferred configuration layering would otherwise take effect, so `UseSetting` is what actually overrides it. `BackgroundJobs:Enabled` is forced `false` so the hosted sweeps never race test data. Every class that boots an `ApiFactory` is in the `[CollectionDefinition(nameof(ApiCollection), DisableParallelization = true)]` collection — `Program.cs` sets Serilog's process-global bootstrap logger on every host startup, and two Api hosts booting concurrently in the same test process can intermittently fail DI validation racing that static assignment, so these classes run sequentially relative to each other while the rest of the suite still runs in parallel. Covers real end-to-end auth (`AuthControllerTests`, `AuthRateLimitTests` — proving the `"auth"` limiter actually trips at 10/minute), the SignalR notifier against a real hub connection (`SignalRPackageStockNotifierTests`), and cross-business authorization boundaries end-to-end (`CrossBusinessAuthorizationTests` — a manager staffing two businesses gets `200` for either, a manager staffing only one gets `403` for the other's management routes; a separate test asserts response DTOs never leak a sensitive field across the catalog/orders surface; another walks the generated OpenAPI document to assert every expected route group is actually present).

### Database-integration tests (`Database/`)

`DbSeederTests.cs` runs `DbSeeder.SeedAsync` against a **real Postgres** container, applying real EF migrations first via `MigrateAsync()` — exactly what `Program.cs` does on startup. This is deliberate, not incidental: an InMemory-provider test wouldn't replay real migration history, so it can't catch the class of bug this project has hit before (old hardcoded-`InsertData` migrations fighting `DbSeeder`'s own reconciliation — see §11 point 3). One Postgres container is shared per test class; each test gets its own logical database on it for isolation without paying container-startup cost per test. Requires Docker to be running locally. Fixed row-count assertions throughout exist specifically to catch a seed row silently failing to insert or a duplicate silently sneaking in on a re-run; every reconciled seed step (staffing, hours, standing orders, Rescue Circles, kitchen tips, referrals, brands, the webhook key hash) is asserted present after a first run and unchanged after a second one. `OrderServicePickupPassIntegrationTests.cs` sits alongside it on the same Postgres-via-Testcontainers setup — it's the regression test for the `dbContext.OrderPickupPasses.Add(...)` vs. `order.PickupPasses.Add(...)` bug described in §3 (OrderPickupPass): a mocked repository's no-op `SaveChangesAsync()` can't observe an `EntityState.Modified`-vs-`Added` mistake, since it never generates real SQL, so only a real `OrderRepository` against a real database reproduces it.

### Architecture test (`Architecture/LayeringTests.cs`)

```csharp
[Fact]
public void BusinessLogic_DoesNotReferenceBlazorComponentsOrJsInterop() =>
    AssertNoForbiddenReferences(typeof(AuthService).Assembly);

[Fact]
public void DataAccess_DoesNotReferenceBlazorComponentsOrJsInterop() =>
    AssertNoForbiddenReferences(typeof(EcoMealDbContext).Assembly);
```
Asserts, via reflection over each assembly's referenced-assembly list, that neither `NetromEcoMeal.BusinessLogic` nor `NetromEcoMeal.DataAccess` ever pulls in `Microsoft.AspNetCore.Components` or `Microsoft.JSInterop` — the one automated guard against regressing either project back toward a UI-framework-coupled shape (§1).

### What's exercised where

Postgres-only query behavior (`EF.Functions.ILike` in `OrderRepository`, the `xmin` optimistic-concurrency token on `Package`, the `order_numbers` sequence, the real migration history) is exercised by the database-integration and API-integration tests' real Postgres round-trip, never by the InMemory-backed unit tests. The InMemory provider is used only where a unit test needs *some* queryable `DbContext` behind a service, not where Postgres-specific SQL translation is actually the thing under test.
