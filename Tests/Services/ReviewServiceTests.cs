using Moq;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers ReviewService's authorization (customer-only, only after a completed order) and the
// package-level review tag added on top of that: GetContextAsync's ReviewablePackages list, and
// SubmitAsync silently dropping a PackageId that isn't actually one of them rather than erroring.
public class ReviewServiceTests
{
    private const string CustomerId = "customer-1";
    private static readonly Guid BusinessId = Guid.NewGuid();

    private sealed record Fixture(ReviewService Service, Mock<IReviewRepository> ReviewRepo, Mock<IOrderRepository> OrderRepo);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var reviewRepo = new Mock<IReviewRepository>();
        var orderRepo = new Mock<IOrderRepository>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId, roles));
        var service = new ReviewService(reviewRepo.Object, orderRepo.Object, currentUser);
        return new Fixture(service, reviewRepo, orderRepo);
    }

    [Fact]
    public async Task GetContextAsync_Anonymous_ReturnsCannotReview()
    {
        var f = Build(null);

        var context = await f.Service.GetContextAsync(BusinessId);

        Assert.False(context.CanReview);
        Assert.Empty(context.ReviewablePackages);
    }

    [Fact]
    public async Task GetContextAsync_CustomerWithoutCompletedOrder_ReturnsCannotReviewEmptyPackages()
    {
        var f = Build(CustomerId, Constants.AppRoles.Customer);
        f.OrderRepo.Setup(r => r.HasCompletedOrderAsync(CustomerId, BusinessId)).ReturnsAsync(false);

        var context = await f.Service.GetContextAsync(BusinessId);

        Assert.False(context.CanReview);
        Assert.Empty(context.ReviewablePackages);
        f.OrderRepo.Verify(r => r.GetCompletedPackagesAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetContextAsync_CustomerWithCompletedOrder_ReturnsReviewablePackages()
    {
        var f = Build(CustomerId, Constants.AppRoles.Customer);
        var package = TestData.Package(BusinessId);
        f.OrderRepo.Setup(r => r.HasCompletedOrderAsync(CustomerId, BusinessId)).ReturnsAsync(true);
        f.OrderRepo.Setup(r => r.GetCompletedPackagesAsync(CustomerId, BusinessId)).ReturnsAsync([package]);

        var context = await f.Service.GetContextAsync(BusinessId);

        Assert.True(context.CanReview);
        Assert.Single(context.ReviewablePackages);
        Assert.Equal(package.Id, context.ReviewablePackages[0].Id);
    }

    [Fact]
    public async Task SubmitAsync_NonCustomer_Throws()
    {
        var f = Build(CustomerId, Constants.AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SubmitAsync(BusinessId, 5, null));
    }

    [Fact]
    public async Task SubmitAsync_NoCompletedOrder_Throws()
    {
        var f = Build(CustomerId, Constants.AppRoles.Customer);
        f.OrderRepo.Setup(r => r.HasCompletedOrderAsync(CustomerId, BusinessId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SubmitAsync(BusinessId, 5, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task SubmitAsync_RatingOutOfRange_Throws(int rating)
    {
        var f = Build(CustomerId, Constants.AppRoles.Customer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SubmitAsync(BusinessId, rating, null));
    }

    [Fact]
    public async Task SubmitAsync_PackageIdInReviewableSet_KeepsIt()
    {
        var f = Build(CustomerId, Constants.AppRoles.Customer);
        var package = TestData.Package(BusinessId);
        f.OrderRepo.Setup(r => r.HasCompletedOrderAsync(CustomerId, BusinessId)).ReturnsAsync(true);
        f.OrderRepo.Setup(r => r.GetCompletedPackagesAsync(CustomerId, BusinessId)).ReturnsAsync([package]);
        f.ReviewRepo.Setup(r => r.GetByUserAndBusinessAsync(CustomerId, BusinessId)).ReturnsAsync((Review?)null);

        var review = await f.Service.SubmitAsync(BusinessId, 5, "Great!", package.Id);

        Assert.Equal(package.Id, review.PackageId);
    }

    [Fact]
    public async Task SubmitAsync_PackageIdNotInReviewableSet_SilentlyDropsIt()
    {
        var f = Build(CustomerId, Constants.AppRoles.Customer);
        var reviewablePackage = TestData.Package(BusinessId);
        var unrelatedPackageId = Guid.NewGuid();
        f.OrderRepo.Setup(r => r.HasCompletedOrderAsync(CustomerId, BusinessId)).ReturnsAsync(true);
        f.OrderRepo.Setup(r => r.GetCompletedPackagesAsync(CustomerId, BusinessId)).ReturnsAsync([reviewablePackage]);
        f.ReviewRepo.Setup(r => r.GetByUserAndBusinessAsync(CustomerId, BusinessId)).ReturnsAsync((Review?)null);

        var review = await f.Service.SubmitAsync(BusinessId, 5, "Great!", unrelatedPackageId);

        Assert.Null(review.PackageId);
    }

    [Fact]
    public async Task SubmitAsync_ExistingReview_UpdatesRatingCommentAndPackageId()
    {
        var f = Build(CustomerId, Constants.AppRoles.Customer);
        var package = TestData.Package(BusinessId);
        var existing = TestData.Review(BusinessId, CustomerId, rating: 3);
        f.OrderRepo.Setup(r => r.HasCompletedOrderAsync(CustomerId, BusinessId)).ReturnsAsync(true);
        f.OrderRepo.Setup(r => r.GetCompletedPackagesAsync(CustomerId, BusinessId)).ReturnsAsync([package]);
        f.ReviewRepo.Setup(r => r.GetByUserAndBusinessAsync(CustomerId, BusinessId)).ReturnsAsync(existing);

        var review = await f.Service.SubmitAsync(BusinessId, 5, "Updated!", package.Id);

        Assert.Same(existing, review);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Updated!", review.Comment);
        Assert.Equal(package.Id, review.PackageId);
        f.ReviewRepo.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }
}
