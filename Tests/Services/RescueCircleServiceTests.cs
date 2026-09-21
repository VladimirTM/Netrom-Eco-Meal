using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers the "shared basket, split payment" orchestration itself. IOrderService (which owns stock/
// rate-limit rules) and IStripeGateway are mocked; EcoMealDbContext is real (InMemory) since
// RescueCircleService queries/writes RescueCircle/RescueCircleParticipant rows directly.
public class RescueCircleServiceTests
{
    private const string OrganizerId = "organizer-1";
    private const string JoinerId = "joiner-1";
    private const string StrangerId = "stranger-1";

    private sealed record Fixture(
        RescueCircleService Service,
        Mock<IOrderService> OrderService,
        Mock<IBusinessService> BusinessService,
        Mock<IPackageRepository> PackageRepo,
        Mock<IStripeGateway> StripeGateway,
        Mock<INotificationService> NotificationService,
        EcoMealDbContext Db);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var db = InMemoryDb.Create();
        var orderService = new Mock<IOrderService>();
        var businessService = new Mock<IBusinessService>();
        var packageRepo = new Mock<IPackageRepository>();
        var stripeGateway = new Mock<IStripeGateway>();
        var notificationService = new Mock<INotificationService>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId, roles));
        var configuration = new ConfigurationBuilder().Build();

        var service = new RescueCircleService(
            orderService.Object, businessService.Object, packageRepo.Object, stripeGateway.Object,
            notificationService.Object, db, currentUser, configuration);

        return new Fixture(service, orderService, businessService, packageRepo, stripeGateway, notificationService, db);
    }

    private static RescueCircle SeedCircle(EcoMealDbContext db, Guid orderId, int participantCount, decimal totalAmount, string status = RescueCircleStatuses.Open)
    {
        var circle = new RescueCircle
        {
            Id = Guid.NewGuid(), OrderId = orderId, OrganizerId = OrganizerId,
            ParticipantCount = participantCount, TotalAmount = totalAmount, Status = status, CreatedAt = DateTime.UtcNow,
        };
        db.RescueCircles.Add(circle);
        return circle;
    }

    private static RescueCircleParticipant SeedParticipant(EcoMealDbContext db, Guid circleId, string userId, decimal share, bool paid = false, string? paymentIntentId = null)
    {
        var participant = new RescueCircleParticipant
        {
            Id = Guid.NewGuid(), RescueCircleId = circleId, UserId = userId, ShareAmount = share,
            JoinedAt = DateTime.UtcNow, PaidAt = paid ? DateTime.UtcNow : null,
            StripePaymentIntentId = paymentIntentId, StripeCheckoutSessionId = "cs_existing",
        };
        db.RescueCircleParticipants.Add(participant);
        return participant;
    }

    // ---- StartCircleAsync ----------------------------------------------------

    [Fact]
    public async Task StartCircleAsync_TooFewParticipants_Throws()
    {
        var f = Build(OrganizerId, AppRoles.Customer);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.StartCircleAsync(Guid.NewGuid(), [new OrderLineRequest(Guid.NewGuid(), 1)], RescueCircles.MinParticipants - 1));
    }

    [Fact]
    public async Task StartCircleAsync_TooManyParticipants_Throws()
    {
        var f = Build(OrganizerId, AppRoles.Customer);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.StartCircleAsync(Guid.NewGuid(), [new OrderLineRequest(Guid.NewGuid(), 1)], RescueCircles.MaxParticipants + 1));
    }

    [Fact]
    public async Task StartCircleAsync_ShareBelowMinChargeableAmount_ThrowsWithoutPlacingOrder()
    {
        var f = Build(OrganizerId, AppRoles.Customer);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        package.Price = 5.00m; // split 6 ways -> 0.83/share, under Checkout.MinChargeableAmount (2.00)
        f.PackageRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.StartCircleAsync(businessId, [new OrderLineRequest(package.Id, 1)], 6));

        // The whole point of pricing the split before placing anything: a rejected split must
        // leave no Order or RescueCircle behind for the organizer to be stuck with.
        f.OrderService.Verify(o => o.PlaceOrderAsync(It.IsAny<Guid>(), It.IsAny<List<OrderLineRequest>>(), It.IsAny<string?>()), Times.Never);
        Assert.False(await f.Db.RescueCircles.AnyAsync());
    }

    [Fact]
    public async Task StartCircleAsync_Success_CreatesCircleWithOrganizerAbsorbingRounding()
    {
        var f = Build(OrganizerId, AppRoles.Customer);
        var businessId = Guid.NewGuid();
        var business = TestData.Business(businessId);
        var package = TestData.Package(businessId);
        package.Price = 9.99m; // 2 x 9.99 = 19.98, split 4 ways = 4.995 -> organizer absorbs the odd half-cent
        var order = TestData.Order(TestData.User(OrganizerId), businessId, OrderStatuses.Pending, (package, 2));

        f.OrderService.Setup(o => o.PlaceOrderAsync(businessId, It.IsAny<List<OrderLineRequest>>())).ReturnsAsync(order);
        f.PackageRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);
        f.BusinessService.Setup(b => b.GetByIdAsync(businessId)).ReturnsAsync(business);
        f.StripeGateway
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Guid>(), business.Name, It.IsAny<List<CheckoutLineItem>>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_organizer", "https://checkout.stripe.com/cs_organizer"));

        var url = await f.Service.StartCircleAsync(businessId, [new OrderLineRequest(package.Id, 2)], 4);

        Assert.Equal("https://checkout.stripe.com/cs_organizer", url);
        var circle = await f.Db.RescueCircles.Include(c => c.Participants).SingleAsync();
        Assert.Equal(order.Id, circle.OrderId);
        Assert.Equal(4, circle.ParticipantCount);
        Assert.Equal(19.98m, circle.TotalAmount);
        var organizerParticipant = Assert.Single(circle.Participants);
        Assert.Equal(OrganizerId, organizerParticipant.UserId);
        // 3 other (unfilled) shares would be 4.99 each -> organizer's share absorbs the remainder.
        Assert.Equal(19.98m - 4.99m * 3, organizerParticipant.ShareAmount);
        Assert.Equal("cs_organizer", organizerParticipant.StripeCheckoutSessionId);
    }

    // ---- JoinOrPayAsync --------------------------------------------------------

    [Fact]
    public async Task JoinOrPayAsync_CircleFull_Throws()
    {
        var f = Build(StrangerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, participantCount: 2, totalAmount: 20m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m);
        SeedParticipant(f.Db, circle.Id, JoinerId, 10m);
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.JoinOrPayAsync(circle.Id));
    }

    [Fact]
    public async Task JoinOrPayAsync_NewParticipant_CreatesParticipantAndStartsCheckout()
    {
        var f = Build(JoinerId, AppRoles.Customer);
        var businessId = Guid.NewGuid();
        var business = TestData.Business(businessId);
        var order = TestData.Order(TestData.User(OrganizerId), businessId, OrderStatuses.Pending, []);
        order.Business = business;
        var circle = SeedCircle(f.Db, order.Id, participantCount: 2, totalAmount: 20m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m, paid: true);
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        f.StripeGateway
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Guid>(), business.Name, It.IsAny<List<CheckoutLineItem>>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_joiner", "https://checkout.stripe.com/cs_joiner"));

        var url = await f.Service.JoinOrPayAsync(circle.Id);

        Assert.Equal("https://checkout.stripe.com/cs_joiner", url);
        var participant = await f.Db.RescueCircleParticipants.SingleAsync(p => p.UserId == JoinerId);
        Assert.Equal(10m, participant.ShareAmount);
        Assert.Equal("cs_joiner", participant.StripeCheckoutSessionId);
    }

    [Fact]
    public async Task JoinOrPayAsync_AlreadyPaid_Throws()
    {
        var f = Build(JoinerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, participantCount: 2, totalAmount: 20m);
        SeedParticipant(f.Db, circle.Id, JoinerId, 10m, paid: true);
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.JoinOrPayAsync(circle.Id));
    }

    // ---- CompleteShareCheckoutAsync --------------------------------------------

    [Fact]
    public async Task CompleteShareCheckoutAsync_DifferentUser_Throws()
    {
        var f = Build(StrangerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, 2, 20m);
        var participant = SeedParticipant(f.Db, circle.Id, OrganizerId, 10m);
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.CompleteShareCheckoutAsync(circle.Id, participant.Id, "cs_existing"));
    }

    [Fact]
    public async Task CompleteShareCheckoutAsync_NotYetFullyPaid_MarksOnlyThatParticipant()
    {
        var f = Build(JoinerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, 2, 20m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m); // still unpaid
        var joiner = SeedParticipant(f.Db, circle.Id, JoinerId, 10m);
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        f.StripeGateway.Setup(s => s.GetSessionStatusAsync("cs_existing"))
            .ReturnsAsync(new StripeSessionStatus(true, "pi_joiner", 10m, "ron"));

        var result = await f.Service.CompleteShareCheckoutAsync(circle.Id, joiner.Id, "cs_existing");

        Assert.True(result.Success);
        Assert.Equal(1, result.Summary!.PaidCount);
        Assert.False(await f.Db.Payments.AnyAsync());
        f.NotificationService.Verify(n => n.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CompleteShareCheckoutAsync_LastShare_CreatesSummaryPaymentAndNotifiesOrganizer()
    {
        var f = Build(JoinerId, AppRoles.Customer);
        var businessId = Guid.NewGuid();
        var order = TestData.Order(TestData.User(OrganizerId), businessId, OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, 2, 20m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m, paid: true, paymentIntentId: "pi_organizer");
        var joiner = SeedParticipant(f.Db, circle.Id, JoinerId, 10m);
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        f.StripeGateway.Setup(s => s.GetSessionStatusAsync("cs_existing"))
            .ReturnsAsync(new StripeSessionStatus(true, "pi_joiner", 10m, "ron"));

        var result = await f.Service.CompleteShareCheckoutAsync(circle.Id, joiner.Id, "cs_existing");

        Assert.True(result.Success);
        Assert.Equal(2, result.Summary!.PaidCount);
        var payment = await f.Db.Payments.SingleAsync(p => p.OrderId == order.Id);
        Assert.Equal(20m, payment.Amount);
        Assert.Equal(PaymentStatuses.Succeeded, payment.Status);
        f.NotificationService.Verify(n => n.CreateAsync(OrganizerId, It.Is<string>(m => m.Contains("fully paid")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CompleteShareCheckoutAsync_AlreadyPaid_ReplaysWithoutReVerifying()
    {
        var f = Build(JoinerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, 2, 20m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m, paid: true, paymentIntentId: "pi_organizer");
        var joiner = SeedParticipant(f.Db, circle.Id, JoinerId, 10m, paid: true, paymentIntentId: "pi_joiner");
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        var result = await f.Service.CompleteShareCheckoutAsync(circle.Id, joiner.Id, "cs_existing");

        Assert.True(result.Success);
        f.StripeGateway.Verify(s => s.GetSessionStatusAsync(It.IsAny<string>()), Times.Never);
    }

    // ---- LeaveCircleAsync -------------------------------------------------------

    [Fact]
    public async Task LeaveCircleAsync_Organizer_Throws()
    {
        var f = Build(OrganizerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, 2, 20m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m);
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.LeaveCircleAsync(circle.Id));
    }

    [Fact]
    public async Task LeaveCircleAsync_UnpaidParticipant_RemovesRowWithoutRefunding()
    {
        var f = Build(JoinerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, 3, 30m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m, paid: true, paymentIntentId: "pi_organizer");
        SeedParticipant(f.Db, circle.Id, JoinerId, 10m);
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        await f.Service.LeaveCircleAsync(circle.Id);

        Assert.False(await f.Db.RescueCircleParticipants.AnyAsync(p => p.UserId == JoinerId));
        f.StripeGateway.Verify(s => s.RefundAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LeaveCircleAsync_PaidParticipant_RefundsThenRemovesRow()
    {
        var f = Build(JoinerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, 3, 30m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m, paid: true, paymentIntentId: "pi_organizer");
        SeedParticipant(f.Db, circle.Id, JoinerId, 10m, paid: true, paymentIntentId: "pi_joiner");
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        await f.Service.LeaveCircleAsync(circle.Id);

        f.StripeGateway.Verify(s => s.RefundAsync("pi_joiner"), Times.Once);
        Assert.False(await f.Db.RescueCircleParticipants.AnyAsync(p => p.UserId == JoinerId));
    }

    [Fact]
    public async Task LeaveCircleAsync_CircleFullyPaid_Throws()
    {
        var f = Build(JoinerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, 2, 20m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m, paid: true, paymentIntentId: "pi_organizer");
        SeedParticipant(f.Db, circle.Id, JoinerId, 10m, paid: true, paymentIntentId: "pi_joiner");
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.LeaveCircleAsync(circle.Id));
        f.StripeGateway.Verify(s => s.RefundAsync(It.IsAny<string>()), Times.Never);
    }

    // ---- GetDetailAsync / GetSummaryAsync ---------------------------------------

    [Fact]
    public async Task GetDetailAsync_Stranger_Throws()
    {
        var f = Build(StrangerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, 2, 20m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m);
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetDetailAsync(circle.Id));
    }

    [Fact]
    public async Task GetSummaryAsync_Stranger_SeesPublicSummaryOnly()
    {
        var f = Build(StrangerId, AppRoles.Customer);
        var order = TestData.Order(TestData.User(OrganizerId), Guid.NewGuid(), OrderStatuses.Pending, []);
        var circle = SeedCircle(f.Db, order.Id, 3, 30m);
        SeedParticipant(f.Db, circle.Id, OrganizerId, 10m, paid: true);
        order.Status = null!; // TestData.Order builds a fresh Status with an Id InMemoryDb already seeded — see CheckoutServiceTests.
        f.Db.Orders.Add(order);
        await f.Db.SaveChangesAsync();

        var summary = await f.Service.GetSummaryAsync(circle.Id);

        Assert.Equal(1, summary.JoinedCount);
        Assert.Equal(1, summary.PaidCount);
        Assert.False(summary.IsMine);
    }
}
