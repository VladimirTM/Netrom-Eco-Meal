using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Repositories;

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
}
