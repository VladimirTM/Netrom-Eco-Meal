using Moq;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Models;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Tests.Services;

// Covers Phase 1's webhook intake endpoint — the one write path in the app authenticated by a
// per-business API key instead of a signed-in user, so it needs its own auth-failure coverage
// PackageServiceTests' ICurrentUser-based tests don't exercise.
public class WebhookIntakeServiceTests
{
    private const string PlaintextKey = "eco_test_key";

    private sealed record Fixture(WebhookIntakeService Service, Mock<IBusinessRepository> Businesses, Mock<IPackageTypeRepository> PackageTypes, Mock<IPackageService> Packages);

    private static Fixture Build()
    {
        var businesses = new Mock<IBusinessRepository>();
        var packageTypes = new Mock<IPackageTypeRepository>();
        var packages = new Mock<IPackageService>();
        var service = new WebhookIntakeService(businesses.Object, packageTypes.Object, packages.Object);
        return new Fixture(service, businesses, packageTypes, packages);
    }

    private static Business MakeBusiness(string status = BusinessStatuses.Approved, bool isHidden = false) => new()
    {
        Id = Guid.NewGuid(), Name = "Stadionul de Gusturi", Description = "d", Address = "a",
        BusinessTypeId = Guid.NewGuid(), Status = status, IsHidden = isHidden,
        WebhookApiKeyHash = ApiKeyHasher.Hash(PlaintextKey),
    };

    private static WebhookPackageRequest MakeRequest(Guid packageTypeId) => new()
    {
        Name = "Surprise Bag", Price = 9.99m, Quantity = 5, WeightKg = 1.2m,
        PackageTypeId = packageTypeId, PickupStart = DateTime.UtcNow, PickupEnd = DateTime.UtcNow.AddHours(3),
    };

    [Fact]
    public async Task CreatePackageAsync_MissingKey_Throws()
    {
        var f = Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.CreatePackageAsync(null, MakeRequest(Guid.NewGuid())));
    }

    [Fact]
    public async Task CreatePackageAsync_UnknownKey_Throws()
    {
        var f = Build();
        f.Businesses.Setup(r => r.GetByApiKeyHashAsync(It.IsAny<string>())).ReturnsAsync((Business?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.CreatePackageAsync("wrong-key", MakeRequest(Guid.NewGuid())));
    }

    [Fact]
    public async Task CreatePackageAsync_HiddenBusiness_Throws()
    {
        var f = Build();
        var business = MakeBusiness(isHidden: true);
        f.Businesses.Setup(r => r.GetByApiKeyHashAsync(ApiKeyHasher.Hash(PlaintextKey))).ReturnsAsync(business);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.CreatePackageAsync(PlaintextKey, MakeRequest(Guid.NewGuid())));
    }

    [Fact]
    public async Task CreatePackageAsync_NotYetApprovedBusiness_Throws()
    {
        var f = Build();
        var business = MakeBusiness(status: BusinessStatuses.PendingApproval);
        f.Businesses.Setup(r => r.GetByApiKeyHashAsync(ApiKeyHasher.Hash(PlaintextKey))).ReturnsAsync(business);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.CreatePackageAsync(PlaintextKey, MakeRequest(Guid.NewGuid())));
    }

    [Fact]
    public async Task CreatePackageAsync_UnknownPackageType_Throws()
    {
        var f = Build();
        var business = MakeBusiness();
        f.Businesses.Setup(r => r.GetByApiKeyHashAsync(ApiKeyHasher.Hash(PlaintextKey))).ReturnsAsync(business);
        f.PackageTypes.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((PackageType?)null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            f.Service.CreatePackageAsync(PlaintextKey, MakeRequest(Guid.NewGuid())));
    }

    [Theory]
    [InlineData(0, 1, 1)]        // Price not positive
    [InlineData(1, -1, 1)]       // Negative quantity
    [InlineData(1, 1, 0)]        // WeightKg not positive
    // Same upper bounds PackageForm.tsx's [Range] attributes enforce (Constants.PackageLimits)
    // — this endpoint used to have no ceiling at all on any of the three.
    [InlineData(10001, 1, 1)]    // Price over the cap
    [InlineData(1, 1001, 1)]     // Quantity over the cap
    [InlineData(1, 1, 101)]      // WeightKg over the cap
    public async Task CreatePackageAsync_InvalidNumbers_Throws(decimal price, int quantity, decimal weightKg)
    {
        var f = Build();
        var business = MakeBusiness();
        var packageType = new PackageType { Id = Guid.NewGuid(), Name = "Surprise Bag" };
        f.Businesses.Setup(r => r.GetByApiKeyHashAsync(ApiKeyHasher.Hash(PlaintextKey))).ReturnsAsync(business);
        f.PackageTypes.Setup(r => r.GetByIdAsync(packageType.Id)).ReturnsAsync(packageType);

        var request = MakeRequest(packageType.Id);
        request.Price = price;
        request.Quantity = quantity;
        request.WeightKg = weightKg;

        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.CreatePackageAsync(PlaintextKey, request));
    }

    [Fact]
    public async Task CreatePackageAsync_UnknownDietaryTag_Throws()
    {
        // Every other field is checked against a closed vocabulary/limit; this used to be the
        // one field a caller could put anything in, including markup, and have it render
        // straight onto the public kitchen page as if it were a real diet/allergen tag.
        var f = Build();
        var business = MakeBusiness();
        var packageType = new PackageType { Id = Guid.NewGuid(), Name = "Surprise Bag" };
        f.Businesses.Setup(r => r.GetByApiKeyHashAsync(ApiKeyHasher.Hash(PlaintextKey))).ReturnsAsync(business);
        f.PackageTypes.Setup(r => r.GetByIdAsync(packageType.Id)).ReturnsAsync(packageType);

        var request = MakeRequest(packageType.Id);
        request.DietaryTags = ["Vegan", "NotARealTag"];

        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.CreatePackageAsync(PlaintextKey, request));
        f.Packages.Verify(svc => svc.AddFromWebhookAsync(It.IsAny<Package>()), Times.Never);
    }

    [Fact]
    public async Task CreatePackageAsync_DietaryTags_NormalizedToCanonicalCasingAndDeduplicated()
    {
        var f = Build();
        var business = MakeBusiness();
        var packageType = new PackageType { Id = Guid.NewGuid(), Name = "Surprise Bag" };
        f.Businesses.Setup(r => r.GetByApiKeyHashAsync(ApiKeyHasher.Hash(PlaintextKey))).ReturnsAsync(business);
        f.PackageTypes.Setup(r => r.GetByIdAsync(packageType.Id)).ReturnsAsync(packageType);

        var request = MakeRequest(packageType.Id);
        request.DietaryTags = ["vegan", "VEGAN", "gluten-free"];

        var package = await f.Service.CreatePackageAsync(PlaintextKey, request);

        Assert.Equal(["Vegan", "Gluten-Free"], package.DietaryTags);
    }

    [Fact]
    public async Task CreatePackageAsync_ValidRequest_CreatesPackageAndUpdatesLastUsed()
    {
        var f = Build();
        var business = MakeBusiness();
        var packageType = new PackageType { Id = Guid.NewGuid(), Name = "Surprise Bag" };
        f.Businesses.Setup(r => r.GetByApiKeyHashAsync(ApiKeyHasher.Hash(PlaintextKey))).ReturnsAsync(business);
        f.PackageTypes.Setup(r => r.GetByIdAsync(packageType.Id)).ReturnsAsync(packageType);

        var package = await f.Service.CreatePackageAsync(PlaintextKey, MakeRequest(packageType.Id));

        Assert.Equal(business.Id, package.BusinessId);
        Assert.Equal("Surprise Bag", package.Name);
        f.Packages.Verify(svc => svc.AddFromWebhookAsync(It.Is<Package>(pkg => pkg.BusinessId == business.Id)), Times.Once);
        Assert.NotNull(business.WebhookApiKeyLastUsedAt);
        f.Businesses.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
