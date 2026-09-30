using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories;
using NetromEcoMeal.Tests.TestSupport;

namespace NetromEcoMeal.Tests.Repositories;

// Covers the Phase 11 /impact leaderboard query — the one piece of GetTopRescuersAsync that's
// easy to get subtly wrong: it must only ever count a Completed order, only within the given
// date range, and only for a user who has actually opted in (ShowOnLeaderboard), never someone
// who merely has real order history.
public class OrderRepositoryTests
{
    [Fact]
    public async Task GetTopRescuersAsync_OnlyIncludesOptedInUsersWithinRange()
    {
        await using var db = InMemoryDb.Create();
        var repo = new OrderRepository(db);
        var business = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = business.BusinessTypeId, Name = "Type A" });
        db.Businesses.Add(business);

        var optedIn = TestData.User("opted-in");
        optedIn.ShowOnLeaderboard = true;
        var optedOut = TestData.User("opted-out");
        optedOut.ShowOnLeaderboard = false;
        db.Users.AddRange(optedIn, optedOut);

        var package = TestData.Package(business.Id, weightKg: 2m);
        db.Packages.Add(package);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;

        Order MakeCompletedOrder(ApplicationUser user, int quantity, DateTime createdAt) => new()
        {
            Id = Guid.NewGuid(), UserId = user.Id, User = user, BusinessId = business.Id,
            StatusId = TestStatusIds.Completed, CreatedAt = createdAt,
            OrderPackages = { new OrderPackage { Id = Guid.NewGuid(), PackageId = package.Id, Quantity = quantity } },
        };

        db.Orders.AddRange(
            MakeCompletedOrder(optedIn, 3, now.AddDays(-1)),    // in range, opted in -> counts
            MakeCompletedOrder(optedOut, 5, now.AddDays(-1)),   // in range, opted out -> excluded
            MakeCompletedOrder(optedIn, 10, now.AddDays(-40))); // opted in but outside range -> excluded
        await db.SaveChangesAsync();

        var results = await repo.GetTopRescuersAsync(now.Date.AddDays(-7), now.AddDays(1), 10);

        var entry = Assert.Single(results);
        Assert.Equal(optedIn.Id, entry.UserId);
        Assert.Equal(6m, entry.KgSaved); // 3 units * 2kg
    }

    [Fact]
    public async Task GetTopRescuersAsync_IgnoresNonCompletedOrders()
    {
        await using var db = InMemoryDb.Create();
        var repo = new OrderRepository(db);
        var business = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = business.BusinessTypeId, Name = "Type A" });
        db.Businesses.Add(business);

        var user = TestData.User();
        user.ShowOnLeaderboard = true;
        db.Users.Add(user);

        var package = TestData.Package(business.Id, weightKg: 2m);
        db.Packages.Add(package);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(), UserId = user.Id, User = user, BusinessId = business.Id,
            StatusId = TestStatusIds.Confirmed, CreatedAt = now,
            OrderPackages = { new OrderPackage { Id = Guid.NewGuid(), PackageId = package.Id, Quantity = 4 } },
        });
        await db.SaveChangesAsync();

        var results = await repo.GetTopRescuersAsync(now.Date.AddDays(-7), now.AddDays(1), 10);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetBusinessImpactStatsAsync_ScopesToBusinessAndSplitsThisMonth()
    {
        await using var db = InMemoryDb.Create();
        var repo = new OrderRepository(db);
        var business = TestData.Business();
        var otherBusiness = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = business.BusinessTypeId, Name = "Type A" });
        db.BusinessTypes.Add(new BusinessType { Id = otherBusiness.BusinessTypeId, Name = "Type B" });
        db.Businesses.AddRange(business, otherBusiness);

        var user = TestData.User();
        db.Users.Add(user);

        var package = TestData.Package(business.Id, weightKg: 2m);
        var otherPackage = TestData.Package(otherBusiness.Id, weightKg: 2m);
        db.Packages.AddRange(package, otherPackage);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        Order MakeOrder(Guid businessId, Guid packageId, int quantity, Guid statusId, DateTime createdAt) => new()
        {
            Id = Guid.NewGuid(), UserId = user.Id, User = user, BusinessId = businessId,
            StatusId = statusId, CreatedAt = createdAt,
            OrderPackages = { new OrderPackage { Id = Guid.NewGuid(), PackageId = packageId, Quantity = quantity } },
        };

        db.Orders.AddRange(
            MakeOrder(business.Id, package.Id, 2, TestStatusIds.Completed, monthStart.AddDays(1)),   // this month -> counts both
            MakeOrder(business.Id, package.Id, 3, TestStatusIds.Completed, monthStart.AddMonths(-2)), // earlier month -> total only
            MakeOrder(business.Id, package.Id, 10, TestStatusIds.Confirmed, monthStart.AddDays(1)),   // not Completed -> excluded
            MakeOrder(otherBusiness.Id, otherPackage.Id, 10, TestStatusIds.Completed, monthStart.AddDays(1))); // other business -> excluded
        await db.SaveChangesAsync();

        var (totalKg, monthKg, completedOrders) = await repo.GetBusinessImpactStatsAsync(business.Id, monthStart);

        Assert.Equal(10m, totalKg);   // (2 + 3) units * 2kg
        Assert.Equal(4m, monthKg);    // 2 units * 2kg
        Assert.Equal(2, completedOrders);
    }
}
