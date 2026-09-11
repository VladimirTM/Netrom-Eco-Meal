using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Repositories;

// Covers the Impact Phase 1 donation-candidate query: it must only ever surface a package whose
// pickup window has actually closed, that's never had a single unit ordered, isn't moderator-hidden,
// and hasn't already been marked as donated.
public class PackageRepositoryTests
{
    [Fact]
    public async Task GetDonationCandidatesAsync_OnlyIncludesClosedCompletelyUnsoldPackages()
    {
        await using var db = InMemoryDb.Create();
        var repo = new PackageRepository(db);
        var business = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = business.BusinessTypeId, Name = "Type A" });
        db.Businesses.Add(business);

        var now = DateTime.UtcNow;

        var candidate = TestData.Package(business.Id);
        candidate.PickupEnd = now.AddHours(-1);

        var stillOpen = TestData.Package(business.Id);
        stillOpen.PickupEnd = now.AddHours(1);

        var hiddenClosed = TestData.Package(business.Id);
        hiddenClosed.PickupEnd = now.AddHours(-1);
        hiddenClosed.IsHidden = true;

        var alreadyDonated = TestData.Package(business.Id);
        alreadyDonated.PickupEnd = now.AddHours(-1);
        alreadyDonated.DonatedAt = now.AddDays(-1);

        var closedButSold = TestData.Package(business.Id);
        closedButSold.PickupEnd = now.AddHours(-1);

        var user = TestData.User();
        db.Users.Add(user);
        db.Packages.AddRange(candidate, stillOpen, hiddenClosed, alreadyDonated, closedButSold);
        // StatusId only (not a nested Status navigation) — InMemoryDb.Create() already seeded the
        // fixed status rows, and setting a second Status object with the same Id would collide.
        db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(), UserId = user.Id, User = user, BusinessId = business.Id,
            StatusId = TestStatusIds.Completed, CreatedAt = DateTime.UtcNow,
            OrderPackages = { new OrderPackage { Id = Guid.NewGuid(), PackageId = closedButSold.Id, Quantity = 1 } },
        });
        await db.SaveChangesAsync();

        var results = await repo.GetDonationCandidatesAsync(null, now);

        var result = Assert.Single(results);
        Assert.Equal(candidate.Id, result.Id);
    }

    [Fact]
    public async Task GetDonationCandidatesAsync_ScopesToBusinessWhenGiven()
    {
        await using var db = InMemoryDb.Create();
        var repo = new PackageRepository(db);
        var businessA = TestData.Business();
        var businessB = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = businessA.BusinessTypeId, Name = "Type A" });
        db.BusinessTypes.Add(new BusinessType { Id = businessB.BusinessTypeId, Name = "Type B" });
        db.Businesses.AddRange(businessA, businessB);

        var now = DateTime.UtcNow;
        var candidateA = TestData.Package(businessA.Id);
        candidateA.PickupEnd = now.AddHours(-1);
        var candidateB = TestData.Package(businessB.Id);
        candidateB.PickupEnd = now.AddHours(-1);

        db.Packages.AddRange(candidateA, candidateB);
        await db.SaveChangesAsync();

        var results = await repo.GetDonationCandidatesAsync(businessA.Id, now);

        var result = Assert.Single(results);
        Assert.Equal(candidateA.Id, result.Id);
    }

    [Fact]
    public async Task GetDonatedWeightKgAsync_SumsOnlyDonatedPackages()
    {
        await using var db = InMemoryDb.Create();
        var repo = new PackageRepository(db);
        var business = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = business.BusinessTypeId, Name = "Type A" });
        db.Businesses.Add(business);

        var donated1 = TestData.Package(business.Id, weightKg: 1.5m);
        donated1.DonatedAt = DateTime.UtcNow.AddDays(-1);
        var donated2 = TestData.Package(business.Id, weightKg: 2m);
        donated2.DonatedAt = DateTime.UtcNow.AddDays(-2);
        var notDonated = TestData.Package(business.Id, weightKg: 5m);

        db.Packages.AddRange(donated1, donated2, notDonated);
        await db.SaveChangesAsync();

        var total = await repo.GetDonatedWeightKgAsync(null);

        Assert.Equal(3.5m, total);
    }

    [Fact]
    public async Task HasAnyOrdersAsync_ReflectsWhetherAnyOrderPackageExists()
    {
        await using var db = InMemoryDb.Create();
        var repo = new PackageRepository(db);
        var business = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = business.BusinessTypeId, Name = "Type A" });
        db.Businesses.Add(business);

        var ordered = TestData.Package(business.Id);
        var neverOrdered = TestData.Package(business.Id);
        var user = TestData.User();
        db.Users.Add(user);
        db.Packages.AddRange(ordered, neverOrdered);
        db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(), UserId = user.Id, User = user, BusinessId = business.Id,
            StatusId = TestStatusIds.Completed, CreatedAt = DateTime.UtcNow,
            OrderPackages = { new OrderPackage { Id = Guid.NewGuid(), PackageId = ordered.Id, Quantity = 1 } },
        });
        await db.SaveChangesAsync();

        Assert.True(await repo.HasAnyOrdersAsync(ordered.Id));
        Assert.False(await repo.HasAnyOrdersAsync(neverOrdered.Id));
    }
}
