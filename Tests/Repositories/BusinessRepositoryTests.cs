using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Repositories;

// Covers the actual many-to-many staff CRUD against a real InMemory-backed EcoMealDbContext —
// the cardinality this replaced Business.ManagerId's single unique-indexed FK with: one business
// can have several staff, and one staff member can be assigned to several businesses.
public class BusinessRepositoryTests
{
    [Fact]
    public async Task AddStaffAsync_NewPair_ReturnsTrueAndPersists()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        var user = TestData.User();
        db.Businesses.Add(business);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await repo.AddStaffAsync(business.Id, user.Id);

        Assert.True(result);
        Assert.True(await repo.IsStaffAsync(business.Id, user.Id));
    }

    [Fact]
    public async Task AddStaffAsync_DuplicatePair_ReturnsFalse()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        var user = TestData.User();
        db.Businesses.Add(business);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await repo.AddStaffAsync(business.Id, user.Id);

        var result = await repo.AddStaffAsync(business.Id, user.Id);

        Assert.False(result);
        Assert.Single(await repo.GetStaffAsync(business.Id));
    }

    [Fact]
    public async Task AddStaffAsync_OneBusinessTwoUsers_BothAreStaff()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        var user1 = TestData.User("user-a");
        var user2 = TestData.User("user-b");
        db.Businesses.Add(business);
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        await repo.AddStaffAsync(business.Id, user1.Id);
        await repo.AddStaffAsync(business.Id, user2.Id);

        var staff = await repo.GetStaffAsync(business.Id);
        Assert.Equal(2, staff.Count);
    }

    [Fact]
    public async Task AddStaffAsync_OneUserTwoBusinesses_BothAreReturned()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business1 = TestData.Business();
        var business2 = TestData.Business();
        var user = TestData.User();
        // GetByStaffUserIdAsync Includes BusinessType, a required navigation — without a matching
        // row EF's inner-join Include would silently drop the business from the results.
        db.BusinessTypes.Add(new BusinessType { Id = business1.BusinessTypeId, Name = "Type A" });
        db.BusinessTypes.Add(new BusinessType { Id = business2.BusinessTypeId, Name = "Type B" });
        db.Businesses.AddRange(business1, business2);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await repo.AddStaffAsync(business1.Id, user.Id);
        await repo.AddStaffAsync(business2.Id, user.Id);

        var businesses = await repo.GetByStaffUserIdAsync(user.Id);
        Assert.Equal(2, businesses.Count);
    }

    [Fact]
    public async Task RemoveStaffAsync_ExistingPair_ReturnsTrueAndRemoves()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        var user = TestData.User();
        db.Businesses.Add(business);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await repo.AddStaffAsync(business.Id, user.Id);

        var result = await repo.RemoveStaffAsync(business.Id, user.Id);

        Assert.True(result);
        Assert.False(await repo.IsStaffAsync(business.Id, user.Id));
    }

    [Fact]
    public async Task RemoveStaffAsync_NonExistentPair_ReturnsFalse()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);

        var result = await repo.RemoveStaffAsync(Guid.NewGuid(), "no-such-user");

        Assert.False(result);
    }

    [Fact]
    public async Task GetByStaffUserIdAsync_OnlyReturnsBusinessesTheUserStaffs()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var staffedBusiness = TestData.Business();
        var otherBusiness = TestData.Business();
        var user = TestData.User();
        db.BusinessTypes.Add(new BusinessType { Id = staffedBusiness.BusinessTypeId, Name = "Type A" });
        db.BusinessTypes.Add(new BusinessType { Id = otherBusiness.BusinessTypeId, Name = "Type B" });
        db.Businesses.AddRange(staffedBusiness, otherBusiness);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await repo.AddStaffAsync(staffedBusiness.Id, user.Id);

        var result = await repo.GetByStaffUserIdAsync(user.Id);

        Assert.Single(result);
        Assert.Equal(staffedBusiness.Id, result[0].Id);
    }

    // ---- SetHoursAsync / AddClosureAsync / RemoveClosureAsync ------------

    [Fact]
    public async Task SetHoursAsync_NoExistingRows_InsertsAll()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        db.Businesses.Add(business);
        await db.SaveChangesAsync();

        await repo.SetHoursAsync(business.Id, [
            new BusinessHours { BusinessId = business.Id, DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeOnly(9, 0), CloseTime = new TimeOnly(18, 0) },
            new BusinessHours { BusinessId = business.Id, DayOfWeek = DayOfWeek.Tuesday, IsClosed = true },
        ]);

        var stored = await db.BusinessHours.Where(h => h.BusinessId == business.Id).ToListAsync();
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task SetHoursAsync_CalledAgain_ReplacesPreviousRowsEntirely()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        await repo.SetHoursAsync(business.Id, [
            new BusinessHours { BusinessId = business.Id, DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeOnly(9, 0), CloseTime = new TimeOnly(18, 0) },
        ]);

        await repo.SetHoursAsync(business.Id, [
            new BusinessHours { BusinessId = business.Id, DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeOnly(10, 0), CloseTime = new TimeOnly(20, 0) },
            new BusinessHours { BusinessId = business.Id, DayOfWeek = DayOfWeek.Tuesday, OpenTime = new TimeOnly(10, 0), CloseTime = new TimeOnly(20, 0) },
        ]);

        var stored = await db.BusinessHours.Where(h => h.BusinessId == business.Id).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.Equal(new TimeOnly(10, 0), stored.Single(h => h.DayOfWeek == DayOfWeek.Monday).OpenTime);
    }

    [Fact]
    public async Task AddClosureAsync_Persists()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        db.Businesses.Add(business);
        await db.SaveChangesAsync();

        var closure = await repo.AddClosureAsync(new BusinessClosure
        {
            BusinessId = business.Id, StartDate = new DateOnly(2026, 8, 10), EndDate = new DateOnly(2026, 8, 12), Reason = "Holiday",
        });

        Assert.NotEqual(Guid.Empty, closure.Id);
        Assert.Single(await db.BusinessClosures.Where(c => c.BusinessId == business.Id).ToListAsync());
    }

    [Fact]
    public async Task RemoveClosureAsync_ExistingClosure_ReturnsTrueAndRemoves()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        var closure = await repo.AddClosureAsync(new BusinessClosure
        {
            BusinessId = business.Id, StartDate = new DateOnly(2026, 8, 10), EndDate = new DateOnly(2026, 8, 12),
        });

        var result = await repo.RemoveClosureAsync(business.Id, closure.Id);

        Assert.True(result);
        Assert.Empty(await db.BusinessClosures.Where(c => c.BusinessId == business.Id).ToListAsync());
    }

    // ---- GetPagedAsync dietaryTag filter (Phase 10) ------------

    [Fact]
    public async Task GetPagedAsync_DietaryTagFilter_OnlyReturnsBusinessesWithMatchingLivePackage()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var veganBusiness = TestData.Business();
        var otherBusiness = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = veganBusiness.BusinessTypeId, Name = "Type A" });
        db.BusinessTypes.Add(new BusinessType { Id = otherBusiness.BusinessTypeId, Name = "Type B" });
        db.Businesses.AddRange(veganBusiness, otherBusiness);
        var veganPackage = TestData.Package(veganBusiness.Id);
        veganPackage.DietaryTags = [DietaryTags.Vegan];
        var glutenPackage = TestData.Package(otherBusiness.Id);
        glutenPackage.DietaryTags = [DietaryTags.ContainsGluten];
        db.Packages.AddRange(veganPackage, glutenPackage);
        await db.SaveChangesAsync();

        var result = await repo.GetPagedAsync(1, 10, null, null, dietaryTag: DietaryTags.Vegan);

        Assert.Single(result.Items);
        Assert.Equal(veganBusiness.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task GetPagedAsync_DietaryTagFilter_IgnoresExpiredPackages()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = business.BusinessTypeId, Name = "Type A" });
        db.Businesses.Add(business);
        var expiredVeganPackage = TestData.Package(business.Id);
        expiredVeganPackage.DietaryTags = [DietaryTags.Vegan];
        expiredVeganPackage.PickupEnd = DateTime.UtcNow.AddHours(-1);
        db.Packages.Add(expiredVeganPackage);
        await db.SaveChangesAsync();

        var result = await repo.GetPagedAsync(1, 10, null, null, dietaryTag: DietaryTags.Vegan);

        Assert.Empty(result.Items);
    }

    // ---- GetPagedAsync maxPrice filter (Phase 2) ------------

    [Fact]
    public async Task GetPagedAsync_MaxPriceFilter_OnlyReturnsBusinessesWithMatchingLivePackage()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var cheapBusiness = TestData.Business();
        var pricyBusiness = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = cheapBusiness.BusinessTypeId, Name = "Type A" });
        db.BusinessTypes.Add(new BusinessType { Id = pricyBusiness.BusinessTypeId, Name = "Type B" });
        db.Businesses.AddRange(cheapBusiness, pricyBusiness);
        var cheapPackage = TestData.Package(cheapBusiness.Id);
        cheapPackage.Price = 15m;
        var pricyPackage = TestData.Package(pricyBusiness.Id);
        pricyPackage.Price = 45m;
        db.Packages.AddRange(cheapPackage, pricyPackage);
        await db.SaveChangesAsync();

        var result = await repo.GetPagedAsync(1, 10, null, null, maxPrice: 30m);

        Assert.Single(result.Items);
        Assert.Equal(cheapBusiness.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task GetPagedAsync_MaxPriceFilter_IgnoresExpiredPackages()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        db.BusinessTypes.Add(new BusinessType { Id = business.BusinessTypeId, Name = "Type A" });
        db.Businesses.Add(business);
        var expiredCheapPackage = TestData.Package(business.Id);
        expiredCheapPackage.Price = 10m;
        expiredCheapPackage.PickupEnd = DateTime.UtcNow.AddHours(-1);
        db.Packages.Add(expiredCheapPackage);
        await db.SaveChangesAsync();

        var result = await repo.GetPagedAsync(1, 10, null, null, maxPrice: 30m);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task RemoveClosureAsync_WrongBusinessId_ReturnsFalseAndDoesNotRemove()
    {
        await using var db = InMemoryDb.Create();
        var repo = new BusinessRepository(db);
        var business = TestData.Business();
        var otherBusiness = TestData.Business();
        db.Businesses.AddRange(business, otherBusiness);
        await db.SaveChangesAsync();
        var closure = await repo.AddClosureAsync(new BusinessClosure
        {
            BusinessId = business.Id, StartDate = new DateOnly(2026, 8, 10), EndDate = new DateOnly(2026, 8, 12),
        });

        var result = await repo.RemoveClosureAsync(otherBusiness.Id, closure.Id);

        Assert.False(result);
        Assert.Single(await db.BusinessClosures.Where(c => c.BusinessId == business.Id).ToListAsync());
    }
}
