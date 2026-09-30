using Moq;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services;
using NetromEcoMeal.Services.Interfaces;
using NetromEcoMeal.Tests.TestSupport;

namespace NetromEcoMeal.Tests.Services;

// Covers Phase 1's multi-location brand grouping — admin-only CRUD/delete-guard shape mirrors
// BusinessTypeServiceTests, plus the customer-only favorite toggle FavoriteService already covers
// for a single Business, just keyed on BrandId instead.
public class BrandServiceTests
{
    private const string AdminId = "admin-1";
    private const string CustomerId = "customer-1";

    private sealed record Fixture(BrandService Service, Mock<IBrandRepository> Repo, Mock<IReviewRepository> Reviews, Mock<IAuditLogService> AuditLog);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var repo = new Mock<IBrandRepository>();
        // AddAsync/UpdateAsync now check for a name collision via GetAllAsync — an unconfigured
        // Moq setup for a Task<List<T>> return defaults to a null result, not an empty list, the
        // way a real repository against an empty/no-collision table actually behaves.
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var reviews = new Mock<IReviewRepository>();
        var auditLog = new Mock<IAuditLogService>();
        var currentUser = new FakeCurrentUser(userId, roles);
        var service = new BrandService(repo.Object, reviews.Object, currentUser, auditLog.Object);
        return new Fixture(service, repo, reviews, auditLog);
    }

    [Fact]
    public async Task AddAsync_NonAdmin_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.AddAsync(new Brand { Name = "Hat-Trick Bakeries" }));
    }

    [Fact]
    public async Task AddAsync_Admin_TrimsNameAndPersists()
    {
        var f = Build(AdminId, AppRoles.Admin);

        await f.Service.AddAsync(new Brand { Name = "  Hat-Trick Bakeries  " });

        f.Repo.Verify(r => r.AddAsync(It.Is<Brand>(b => b.Name == "Hat-Trick Bakeries")), Times.Once);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddAsync_Admin_DuplicateNameCaseInsensitive_ThrowsAndDoesNotPersist()
    {
        var f = Build(AdminId, AppRoles.Admin);
        f.Repo.Setup(r => r.GetAllAsync()).ReturnsAsync([new Brand { Id = Guid.NewGuid(), Name = "Hat-Trick Bakeries" }]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.AddAsync(new Brand { Name = "  hat-trick bakeries  " }));

        f.Repo.Verify(r => r.AddAsync(It.IsAny<Brand>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Admin_DuplicateNameOfAnotherBrand_ThrowsAndDoesNotRename()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var editing = new Brand { Id = Guid.NewGuid(), Name = "Hat-Trick Bakeries" };
        var other = new Brand { Id = Guid.NewGuid(), Name = "Golden Boot Bakeries" };
        f.Repo.Setup(r => r.GetByIdAsync(editing.Id)).ReturnsAsync(editing);
        f.Repo.Setup(r => r.GetAllAsync()).ReturnsAsync([editing, other]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.UpdateAsync(new Brand { Id = editing.Id, Name = "Golden Boot Bakeries" }));

        Assert.Equal("Hat-Trick Bakeries", editing.Name);
        f.Repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_StillInUse_ThrowsAndDoesNotDelete()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var brand = new Brand { Id = Guid.NewGuid(), Name = "Hat-Trick Bakeries" };
        f.Repo.Setup(r => r.GetByIdAsync(brand.Id)).ReturnsAsync(brand);
        f.Repo.Setup(r => r.IsInUseAsync(brand.Id)).ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.DeleteAsync(brand.Id));

        f.Repo.Verify(r => r.DeleteAsync(It.IsAny<Brand>()), Times.Never);
    }

    [Fact]
    public async Task GetPublicDetailAsync_UnknownBrand_ReturnsNull()
    {
        var f = Build(null);
        f.Repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Brand?)null);

        Assert.Null(await f.Service.GetPublicDetailAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPublicDetailAsync_AggregatesRatingAcrossPublicLocations()
    {
        var f = Build(null);
        var brand = new Brand { Id = Guid.NewGuid(), Name = "Hat-Trick Bakeries" };
        var location1 = new Business { Id = Guid.NewGuid(), Name = "Branch 1", Description = "d", Address = "a", BusinessTypeId = Guid.NewGuid() };
        var location2 = new Business { Id = Guid.NewGuid(), Name = "Branch 2", Description = "d", Address = "a", BusinessTypeId = Guid.NewGuid() };
        f.Repo.Setup(r => r.GetByIdAsync(brand.Id)).ReturnsAsync(brand);
        f.Repo.Setup(r => r.GetPublicLocationsAsync(brand.Id)).ReturnsAsync([location1, location2]);
        f.Reviews.Setup(r => r.GetByBusinessIdsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(location1.Id) && ids.Contains(location2.Id))))
            .ReturnsAsync([
                new Review { Id = Guid.NewGuid(), BusinessId = location1.Id, UserId = "u1", Rating = 5, CreatedAt = DateTime.UtcNow },
                new Review { Id = Guid.NewGuid(), BusinessId = location2.Id, UserId = "u2", Rating = 3, CreatedAt = DateTime.UtcNow },
            ]);

        var detail = await f.Service.GetPublicDetailAsync(brand.Id);

        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Locations.Count);
        Assert.Equal(2, detail.RatingCount);
        Assert.Equal(4, detail.AverageRating);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_NonCustomer_Throws()
    {
        var f = Build(AdminId, AppRoles.Admin);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.ToggleFavoriteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ToggleFavoriteAsync_NotYetFavorited_Adds()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var brandId = Guid.NewGuid();
        f.Repo.Setup(r => r.RemoveFavoriteAsync(CustomerId, brandId)).ReturnsAsync(false);

        var result = await f.Service.ToggleFavoriteAsync(brandId);

        Assert.True(result);
        f.Repo.Verify(r => r.AddFavoriteAsync(CustomerId, brandId), Times.Once);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_AlreadyFavorited_Removes()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var brandId = Guid.NewGuid();
        f.Repo.Setup(r => r.RemoveFavoriteAsync(CustomerId, brandId)).ReturnsAsync(true);

        var result = await f.Service.ToggleFavoriteAsync(brandId);

        Assert.False(result);
        f.Repo.Verify(r => r.AddFavoriteAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }
}
