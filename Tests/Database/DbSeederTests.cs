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
            new HashSet<string> { OrderStatuses.Pending, OrderStatuses.Confirmed, OrderStatuses.Completed, OrderStatuses.Cancelled },
            statusNames.ToHashSet());

        Assert.Equal(12, await db.Businesses.CountAsync());
        Assert.Equal(24, await db.Packages.CountAsync());
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
        Assert.NotNull(customer);
        Assert.NotNull(manager);
        Assert.Contains(AppRoles.Customer, await userManager.GetRolesAsync(customer));
        Assert.Contains(AppRoles.BusinessManager, await userManager.GetRolesAsync(manager));

        // The demo manager should be assigned to Stadionul de Gusturi.
        var managedBusiness = await db.Businesses.FindAsync(new Guid("44444444-0000-0000-0000-000000000001"));
        Assert.Equal(manager.Id, managedBusiness!.ManagerId);

        var orders = await db.Orders.Include(o => o.Status).Where(o => o.UserId == customer.Id).ToListAsync();
        Assert.Equal(6, orders.Count);
        Assert.Equal(3, orders.Count(o => o.Status.Name == OrderStatuses.Completed));
        Assert.Single(orders, o => o.Status.Name == OrderStatuses.Confirmed);
        Assert.Single(orders, o => o.Status.Name == OrderStatuses.Cancelled);
        Assert.Single(orders, o => o.Status.Name == OrderStatuses.Pending);

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
        Assert.Equal(12, await finalDb.Businesses.CountAsync());
        Assert.Equal(24, await finalDb.Packages.CountAsync());
        Assert.Equal(6, await finalDb.Orders.CountAsync());
        Assert.Equal(3, await finalDb.Favorites.CountAsync());
        Assert.Equal(2, await finalDb.Reviews.CountAsync());
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
