using Microsoft.Extensions.Configuration;
using Moq;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers NearExpiryNudgeService's audience-building and tag-matching logic. All dependencies are
// mocked — no InMemory EcoMealDbContext needed, since (unlike OrderService) this service only
// talks to the DB through repository interfaces.
public class NearExpiryNudgeServiceTests
{
    private sealed record Fixture(
        NearExpiryNudgeService Service,
        Mock<IPackageRepository> PackageRepo,
        Mock<IFavoriteRepository> FavoriteRepo,
        Mock<IOrderRepository> OrderRepo,
        Mock<INotificationService> NotificationService,
        Mock<IAppEmailSender> EmailSender,
        Mock<INearExpiryNudgeComposer> Composer);

    private static Fixture Build()
    {
        var packageRepo = new Mock<IPackageRepository>();
        var favoriteRepo = new Mock<IFavoriteRepository>();
        var orderRepo = new Mock<IOrderRepository>();
        var notificationService = new Mock<INotificationService>();
        var emailSender = new Mock<IAppEmailSender>();
        var composer = new Mock<INearExpiryNudgeComposer>();
        var configuration = new ConfigurationBuilder().Build();

        composer
            .Setup(c => c.ComposeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Closing soon — grab it before it's gone.");

        var service = new NearExpiryNudgeService(
            packageRepo.Object, favoriteRepo.Object, orderRepo.Object,
            notificationService.Object, emailSender.Object, composer.Object, configuration);

        return new Fixture(service, packageRepo, favoriteRepo, orderRepo, notificationService, emailSender, composer);
    }

    private static Package NearExpiryPackage(Guid businessId, string[]? dietaryTags = null)
    {
        var package = TestData.Package(businessId, quantity: 3);
        package.Business = TestData.Business(businessId);
        package.DietaryTags = dietaryTags is null ? [] : [..dietaryTags];
        return package;
    }

    [Fact]
    public async Task SweepAsync_NoCandidates_ReturnsZeroAndDoesNotSave()
    {
        var f = Build();
        f.PackageRepo.Setup(p => p.GetNearExpiryUnclaimedAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var sent = await f.Service.SweepAsync();

        Assert.Equal(0, sent);
        f.PackageRepo.Verify(p => p.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task SweepAsync_CandidateWithNoAudience_MarksSentWithoutNotifying()
    {
        var f = Build();
        var businessId = Guid.NewGuid();
        var package = NearExpiryPackage(businessId);
        f.PackageRepo.Setup(p => p.GetNearExpiryUnclaimedAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([package]);
        f.FavoriteRepo.Setup(r => r.GetFavoritingUsersAsync(businessId)).ReturnsAsync([]);
        f.OrderRepo.Setup(r => r.GetPastCustomersAsync(businessId)).ReturnsAsync([]);

        var sent = await f.Service.SweepAsync();

        Assert.Equal(0, sent);
        Assert.NotNull(package.NearExpiryNudgeSentAt);
        f.Composer.Verify(c => c.ComposeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        f.NotificationService.Verify(n => n.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        f.PackageRepo.Verify(p => p.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SweepAsync_FavoriterWithNoMatchingHistory_GetsGenericNudge()
    {
        var f = Build();
        var businessId = Guid.NewGuid();
        var package = NearExpiryPackage(businessId, ["Vegan"]);
        var favoriter = TestData.User("favoriter-1");
        f.PackageRepo.Setup(p => p.GetNearExpiryUnclaimedAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([package]);
        f.FavoriteRepo.Setup(r => r.GetFavoritingUsersAsync(businessId)).ReturnsAsync([favoriter]);
        f.OrderRepo.Setup(r => r.GetPastCustomersAsync(businessId)).ReturnsAsync([]);
        f.OrderRepo.Setup(r => r.GetCompletedPackagesAsync(favoriter.Id, businessId)).ReturnsAsync([]);

        var sent = await f.Service.SweepAsync();

        Assert.Equal(1, sent);
        f.Composer.Verify(c => c.ComposeAsync(package.Name, package.Business.Name, package.Quantity, It.IsAny<TimeSpan>(), null, It.IsAny<CancellationToken>()), Times.Once);
        f.NotificationService.Verify(n => n.CreateAsync(favoriter.Id, It.IsAny<string>(), $"/businesses/{businessId}"), Times.Once);
        f.EmailSender.Verify(e => e.SendEmailAsync(favoriter.Email!, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SweepAsync_PastCustomerWithMatchingDietaryTag_GetsPersonalizedGroupSeparately()
    {
        var f = Build();
        var businessId = Guid.NewGuid();
        var package = NearExpiryPackage(businessId, ["Vegan", "GlutenFree"]);
        var favoriterNoMatch = TestData.User("favoriter-1");
        var pastCustomerMatch = TestData.User("past-customer-1");
        var pastVeganPackage = TestData.Package(businessId);
        pastVeganPackage.DietaryTags = ["Vegan"];

        f.PackageRepo.Setup(p => p.GetNearExpiryUnclaimedAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([package]);
        f.FavoriteRepo.Setup(r => r.GetFavoritingUsersAsync(businessId)).ReturnsAsync([favoriterNoMatch]);
        f.OrderRepo.Setup(r => r.GetPastCustomersAsync(businessId)).ReturnsAsync([pastCustomerMatch]);
        f.OrderRepo.Setup(r => r.GetCompletedPackagesAsync(favoriterNoMatch.Id, businessId)).ReturnsAsync([]);
        f.OrderRepo.Setup(r => r.GetCompletedPackagesAsync(pastCustomerMatch.Id, businessId)).ReturnsAsync([pastVeganPackage]);

        var sent = await f.Service.SweepAsync();

        Assert.Equal(2, sent);
        // One AI call per distinct match — a plain nudge for the favoriter, a personalized one
        // for the past customer whose order history shares the "Vegan" tag with this package.
        f.Composer.Verify(c => c.ComposeAsync(package.Name, package.Business.Name, package.Quantity, It.IsAny<TimeSpan>(), null, It.IsAny<CancellationToken>()), Times.Once);
        f.Composer.Verify(c => c.ComposeAsync(package.Name, package.Business.Name, package.Quantity, It.IsAny<TimeSpan>(), "Vegan", It.IsAny<CancellationToken>()), Times.Once);
        f.NotificationService.Verify(n => n.CreateAsync(pastCustomerMatch.Id, It.IsAny<string>(), $"/businesses/{businessId}"), Times.Once);
    }

    [Fact]
    public async Task SweepAsync_UserFavoritesAndHasOrderHistory_NotifiedOnlyOnce()
    {
        var f = Build();
        var businessId = Guid.NewGuid();
        var package = NearExpiryPackage(businessId);
        var user = TestData.User("both-1");
        f.PackageRepo.Setup(p => p.GetNearExpiryUnclaimedAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([package]);
        f.FavoriteRepo.Setup(r => r.GetFavoritingUsersAsync(businessId)).ReturnsAsync([user]);
        f.OrderRepo.Setup(r => r.GetPastCustomersAsync(businessId)).ReturnsAsync([user]);
        f.OrderRepo.Setup(r => r.GetCompletedPackagesAsync(user.Id, businessId)).ReturnsAsync([]);

        var sent = await f.Service.SweepAsync();

        Assert.Equal(1, sent);
        f.NotificationService.Verify(n => n.CreateAsync(user.Id, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task SweepAsync_UserWithoutEmail_SkipsEmailButStillNotifies()
    {
        var f = Build();
        var businessId = Guid.NewGuid();
        var package = NearExpiryPackage(businessId);
        var user = new ApplicationUser { Id = "no-email-1", UserName = "no-email-1", Name = "No Email", Email = null };
        f.PackageRepo.Setup(p => p.GetNearExpiryUnclaimedAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([package]);
        f.FavoriteRepo.Setup(r => r.GetFavoritingUsersAsync(businessId)).ReturnsAsync([user]);
        f.OrderRepo.Setup(r => r.GetPastCustomersAsync(businessId)).ReturnsAsync([]);
        f.OrderRepo.Setup(r => r.GetCompletedPackagesAsync(user.Id, businessId)).ReturnsAsync([]);

        var sent = await f.Service.SweepAsync();

        Assert.Equal(1, sent);
        f.NotificationService.Verify(n => n.CreateAsync(user.Id, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        f.EmailSender.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
