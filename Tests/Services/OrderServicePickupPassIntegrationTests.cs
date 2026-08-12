using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.Database;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Runs OrderService's pickup-pass logic through a REAL OrderRepository against real Postgres
// (Testcontainers), unlike OrderServiceTests which mocks IOrderRepository entirely. This class
// exists because a real regression slipped past the mocked tests: adding a new OrderPickupPass via
// order.PickupPasses.Add(...) (collection-navigation fixup) rather than
// dbContext.OrderPickupPasses.Add(...) gets tracked as EntityState.Modified instead of Added — the
// client-assigned, non-default Guid key looks like an existing row to EF Core when discovered only
// through graph traversal. That turns the INSERT into a no-op UPDATE, which Npgsql then reports as
// a DbUpdateConcurrencyException ("expected to affect 1 row(s), but actually affected 0"). A mocked
// IOrderRepository can't catch this — SaveChangesAsync is a no-op there, so the bug only surfaces
// against a real database with real Npgsql-generated SQL.
[Collection(nameof(PostgresCollection))]
public class OrderServicePickupPassIntegrationTests(PostgresFixture fixture)
{
    private async Task<(OrderService Service, EcoMealDbContext Db, Order Order)> BuildConfirmedOrderAsync(int passCount = 1, string actorRole = AppRoles.Customer)
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<EcoMealDbContext>().UseNpgsql(connectionString).Options;

        var db = new EcoMealDbContext(options);
        await db.Database.MigrateAsync();

        var confirmedStatus = new Status { Id = Guid.NewGuid(), Name = OrderStatuses.Confirmed };
        var completedStatus = new Status { Id = Guid.NewGuid(), Name = OrderStatuses.Completed };
        db.Statuses.AddRange(confirmedStatus, completedStatus);

        var businessType = new BusinessType { Id = Guid.NewGuid(), Name = "Test" };
        db.BusinessTypes.Add(businessType);
        var business = TestData.Business();
        business.BusinessTypeId = businessType.Id;
        db.Businesses.Add(business);

        var user = TestData.User();
        db.Users.Add(user);

        var packageType = new PackageType { Id = Guid.NewGuid(), Name = "Test" };
        db.PackageTypes.Add(packageType);
        var package = TestData.Package(business.Id);
        package.PackageTypeId = packageType.Id;
        db.Packages.Add(package);

        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = user.Id, User = user, BusinessId = business.Id,
            StatusId = confirmedStatus.Id, CreatedAt = DateTime.UtcNow,
        };
        order.OrderPackages.Add(new OrderPackage { Id = Guid.NewGuid(), OrderId = order.Id, PackageId = package.Id, Quantity = 1 });
        db.Orders.Add(order);

        var now = DateTime.UtcNow;
        for (var i = 1; i <= passCount; i++)
            db.OrderPickupPasses.Add(new OrderPickupPass { Id = Guid.NewGuid(), OrderId = order.Id, Label = $"Pass {i}", CreatedAt = now.AddMilliseconds(i) });

        await db.SaveChangesAsync();

        var orderRepository = new OrderRepository(db);
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(user.Id, actorRole));
        var service = new OrderService(
            orderRepository, Mock.Of<IPackageRepository>(), Mock.Of<IBusinessService>(), Mock.Of<INotificationService>(),
            Mock.Of<IAppEmailSender>(), Mock.Of<IStripeGateway>(), Mock.Of<IAuditLogService>(), db, currentUser,
            new ConfigurationBuilder().Build(), NullLogger<OrderService>.Instance);

        return (service, db, order);
    }

    [Fact]
    public async Task SplitPickupPassesAsync_RealDatabase_ReplacesPassesWithoutConcurrencyError()
    {
        var (service, db, order) = await BuildConfirmedOrderAsync(passCount: 1);

        var result = await service.SplitPickupPassesAsync(order.Id, 5);

        Assert.Equal(5, result.PickupPasses.Count);
        var persisted = await db.OrderPickupPasses.Where(p => p.OrderId == order.Id).ToListAsync();
        Assert.Equal(5, persisted.Count);
        Assert.Equal(["Pass 1", "Pass 2", "Pass 3", "Pass 4", "Pass 5"], persisted.Select(p => p.Label).OrderBy(l => l));
    }

    [Fact]
    public async Task SplitPickupPassesAsync_RealDatabase_CanBeCalledRepeatedly()
    {
        // Splitting twice in a row (2 passes, then 4) exercises delete-then-insert on the SAME
        // rows twice in the same test — the scenario that originally surfaced the bug.
        var (service, db, order) = await BuildConfirmedOrderAsync(passCount: 1);

        await service.SplitPickupPassesAsync(order.Id, 2);
        var result = await service.SplitPickupPassesAsync(order.Id, 4);

        Assert.Equal(4, result.PickupPasses.Count);
        var persisted = await db.OrderPickupPasses.Where(p => p.OrderId == order.Id).ToListAsync();
        Assert.Equal(4, persisted.Count);
    }

    [Fact]
    public async Task RedeemPickupPassAsync_RealDatabase_CompletesOrder()
    {
        var (service, db, order) = await BuildConfirmedOrderAsync(passCount: 1, actorRole: AppRoles.Admin);
        var pass = await db.OrderPickupPasses.FirstAsync(p => p.OrderId == order.Id);

        var result = await service.RedeemPickupPassAsync(order.Id, pass.Id);

        Assert.Equal(OrderStatuses.Completed, (await db.Statuses.FindAsync(result.StatusId))!.Name);
        var persisted = await db.OrderPickupPasses.FirstAsync(p => p.Id == pass.Id);
        Assert.NotNull(persisted.RedeemedAt);
    }
}
