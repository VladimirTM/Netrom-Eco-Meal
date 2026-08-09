using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Database;

// Runs DbSeeder against a real Postgres (via Testcontainers), the same way Program.cs does on
// startup: MigrateAsync() then SeedAsync(). This is the regression test for the migration-vs-
// seeder conflict this codebase hit before (old EF migrations hardcoded seed rows that DbSeeder's
// "if (await db.Packages.AnyAsync()) return;" guards would then silently defer to) — an
// InMemory-provider unit test wouldn't replay real migration history, so it can't catch that class
// of bug. Also covers idempotency: SeedAsync must be safe to run on every app startup.
[Collection(nameof(PostgresCollection))]
public class DbSeederTests(PostgresFixture fixture)
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SeedAdmin:Email"] = "admin@test.local",
            ["SeedAdmin:Password"] = "Admin123!",
        })
        .Build();

    private async Task<ServiceProvider> BuildSeededServicesAsync()
    {
        var connectionString = await fixture.CreateDatabaseAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<EcoMealDbContext>(o => o.UseNpgsql(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<EcoMealDbContext>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcoMealDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(scope.ServiceProvider, Configuration);

        return provider;
    }

    [Fact]
    public async Task SeedAsync_FreshDatabase_SeedsReferenceData()
    {
        await using var provider = await BuildSeededServicesAsync();
        await using var db = provider.GetRequiredService<EcoMealDbContext>();

        Assert.Equal(5, await db.BusinessTypes.CountAsync());
        Assert.Equal(5, await db.PackageTypes.CountAsync());

        var statusNames = await db.Statuses.Select(s => s.Name).ToListAsync();
        Assert.Equal(
            new HashSet<string> { OrderStatuses.Pending, OrderStatuses.Confirmed, OrderStatuses.Completed, OrderStatuses.Cancelled, OrderStatuses.NoShow },
            statusNames.ToHashSet());

        // 12 approved storefront kitchens + 2 Phase 9 self-service applications (1 pending, 1 rejected).
        Assert.Equal(14, await db.Businesses.CountAsync());
        // 24 live storefront packages + 9 historical ones backing the Phase 8 analytics card.
        Assert.Equal(33, await db.Packages.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_FreshDatabase_SeedsRolesAndAdminAccount()
    {
        await using var provider = await BuildSeededServicesAsync();
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in AppRoles.AllRoles)
            Assert.True(await roleManager.RoleExistsAsync(role));

        var admin = await userManager.FindByEmailAsync("admin@test.local");
        Assert.NotNull(admin);
        Assert.Contains(AppRoles.Admin, await userManager.GetRolesAsync(admin));
    }

    [Fact]
    public async Task SeedAsync_FreshDatabase_SeedsDemoCustomerAndManagerWithActivity()
    {
        await using var provider = await BuildSeededServicesAsync();
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<EcoMealDbContext>();

        var customer = await userManager.FindByEmailAsync("demo.customer@ecomeal.local");
        var manager = await userManager.FindByEmailAsync("demo.manager@ecomeal.local");
        var manager2 = await userManager.FindByEmailAsync("demo.manager2@ecomeal.local");
        Assert.NotNull(customer);
        Assert.NotNull(manager);
        Assert.NotNull(manager2);
        Assert.Contains(AppRoles.Customer, await userManager.GetRolesAsync(customer));
        Assert.Contains(AppRoles.BusinessManager, await userManager.GetRolesAsync(manager));
        Assert.Contains(AppRoles.BusinessManager, await userManager.GetRolesAsync(manager2));

        var stadionulId = new Guid("44444444-0000-0000-0000-000000000001");
        var varBistroId = new Guid("44444444-0000-0000-0000-000000000002");

        // Demonstrates both directions of the many-to-many: the first demo manager staffs two
        // businesses, and the second demo manager joins them at Stadionul de Gusturi.
        var staffedByManager = await db.BusinessStaff.Where(s => s.UserId == manager.Id).Select(s => s.BusinessId).ToListAsync();
        Assert.Contains(stadionulId, staffedByManager);
        Assert.Contains(varBistroId, staffedByManager);

        var stadionulStaffIds = await db.BusinessStaff.Where(s => s.BusinessId == stadionulId).Select(s => s.UserId).ToListAsync();
        Assert.Contains(manager.Id, stadionulStaffIds);
        Assert.Contains(manager2.Id, stadionulStaffIds);

        var orders = await db.Orders.Include(o => o.Status).Where(o => o.UserId == customer.Id).ToListAsync();
        // 7 original demo orders + 9 historical ones backing the Phase 8 analytics card.
        Assert.Equal(16, orders.Count);
        Assert.Equal(12, orders.Count(o => o.Status.Name == OrderStatuses.Completed));
        Assert.Single(orders, o => o.Status.Name == OrderStatuses.Confirmed);
        Assert.Single(orders, o => o.Status.Name == OrderStatuses.Cancelled);
        Assert.Single(orders, o => o.Status.Name == OrderStatuses.Pending);
        Assert.Single(orders, o => o.Status.Name == OrderStatuses.NoShow);

        // Every order should have been assigned a real OrderNumber by the DB sequence.
        Assert.All(orders, o => Assert.True(o.OrderNumber > 0));

        Assert.Equal(3, await db.Favorites.CountAsync(f => f.UserId == customer.Id));
        Assert.Equal(2, await db.Reviews.CountAsync(r => r.UserId == customer.Id));
        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == customer.Id));
        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == manager.Id));
    }

    [Fact]
    public async Task SeedAsync_DemoOrders_ReserveStockForConfirmedAndCompletedOnly()
    {
        await using var provider = await BuildSeededServicesAsync();
        await using var db = provider.GetRequiredService<EcoMealDbContext>();

        // "Golden Boot Surprise Bag" (b1) starts at 5, one Completed demo order takes 1 -> 4.
        var goldenBoot = await db.Packages.FindAsync(new Guid("55555555-0000-0000-0000-000000000001"));
        Assert.Equal(4, goldenBoot!.Quantity);

        // "Offside Veggie Box" (b9) starts at 15; the demo order against it is Cancelled, so stock
        // is untouched — Pending/Cancelled orders never reserve stock (see OrderService).
        var offsideVeggieBox = await db.Packages.FindAsync(new Guid("55555555-0000-0000-0000-000000000017"));
        Assert.Equal(15, offsideVeggieBox!.Quantity);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_IsIdempotent()
    {
        var connectionString = await fixture.CreateDatabaseAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<EcoMealDbContext>(o => o.UseNpgsql(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<EcoMealDbContext>()
            .AddDefaultTokenProviders();
        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EcoMealDbContext>();
            await db.Database.MigrateAsync();
            await DbSeeder.SeedAsync(scope.ServiceProvider, Configuration);
            await DbSeeder.SeedAsync(scope.ServiceProvider, Configuration);
        }

        await using var finalDb = provider.GetRequiredService<EcoMealDbContext>();
        Assert.Equal(14, await finalDb.Businesses.CountAsync());
        Assert.Equal(33, await finalDb.Packages.CountAsync());
        Assert.Equal(16, await finalDb.Orders.CountAsync());
        Assert.Equal(3, await finalDb.Favorites.CountAsync());
        Assert.Equal(2, await finalDb.Reviews.CountAsync());
        // Two demo managers each staff one or two of the demo businesses — must not double-insert.
        Assert.Equal(3, await finalDb.BusinessStaff.CountAsync());
        Assert.Equal(4, await finalDb.Reports.CountAsync());
        Assert.Equal(11, await finalDb.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_FreshDatabase_SeedsApprovalWorkflowDemoData()
    {
        await using var provider = await BuildSeededServicesAsync();
        await using var db = provider.GetRequiredService<EcoMealDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var customer = await userManager.FindByEmailAsync("demo.customer@ecomeal.local");
        Assert.NotNull(customer);

        var pending = await db.Businesses.FindAsync(new Guid("44444444-0000-0000-0000-000000000013"));
        Assert.NotNull(pending);
        Assert.Equal(BusinessStatuses.PendingApproval, pending!.Status);
        Assert.Equal(customer!.Id, pending.SubmittedByUserId);

        var rejected = await db.Businesses.FindAsync(new Guid("44444444-0000-0000-0000-000000000014"));
        Assert.NotNull(rejected);
        Assert.Equal(BusinessStatuses.Rejected, rejected!.Status);
        Assert.False(string.IsNullOrWhiteSpace(rejected.RejectionReason));
        Assert.Equal(customer.Id, rejected.SubmittedByUserId);

        // Approved storefront businesses default to Approved with no rejection reason.
        var stadionul = await db.Businesses.FindAsync(new Guid("44444444-0000-0000-0000-000000000001"));
        Assert.Equal(BusinessStatuses.Approved, stadionul!.Status);
    }

    [Fact]
    public async Task SeedAsync_FreshDatabase_SeedsModerationDemoData()
    {
        await using var provider = await BuildSeededServicesAsync();
        await using var db = provider.GetRequiredService<EcoMealDbContext>();

        var fanZoneGrill = await db.Businesses.FindAsync(new Guid("44444444-0000-0000-0000-000000000012"));
        Assert.NotNull(fanZoneGrill);
        Assert.True(fanZoneGrill!.IsHidden);
        Assert.False(string.IsNullOrWhiteSpace(fanZoneGrill.HiddenReason));
        // Approval status is untouched by moderation — Hidden is an orthogonal flag.
        Assert.Equal(BusinessStatuses.Approved, fanZoneGrill.Status);

        var redCardPastryBox = await db.Packages.FindAsync(new Guid("55555555-0000-0000-0000-000000000016"));
        Assert.NotNull(redCardPastryBox);
        Assert.True(redCardPastryBox!.IsHidden);
        Assert.False(string.IsNullOrWhiteSpace(redCardPastryBox.HiddenReason));
    }

    [Fact]
    public async Task SeedAsync_FreshDatabase_SeedsReportsAndAuditLog()
    {
        await using var provider = await BuildSeededServicesAsync();
        await using var db = provider.GetRequiredService<EcoMealDbContext>();

        var reports = await db.Reports.ToListAsync();
        Assert.Equal(4, reports.Count);
        Assert.Single(reports, r => r.Status == ReportStatuses.Open);
        Assert.Equal(2, reports.Count(r => r.Status == ReportStatuses.ActionTaken));
        Assert.Single(reports, r => r.Status == ReportStatuses.Dismissed);

        var auditLogs = await db.AuditLogs.ToListAsync();
        Assert.NotEmpty(auditLogs);
        Assert.Contains(auditLogs, a => a.Action == AuditActions.BusinessApplied);
        Assert.Contains(auditLogs, a => a.Action == AuditActions.BusinessRejected);
        Assert.Contains(auditLogs, a => a.Action == AuditActions.BusinessHidden);
        Assert.Contains(auditLogs, a => a.Action == AuditActions.PackageHidden);
        Assert.Contains(auditLogs, a => a.Action == AuditActions.ReportActionTaken);
        Assert.Contains(auditLogs, a => a.Action == AuditActions.ReportDismissed);
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
