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
}
