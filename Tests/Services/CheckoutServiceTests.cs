using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Services.Payments;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers the "pay before the Order exists" bridge — CheckoutService itself never talks to Stripe
// (IStripeGateway is mocked) or creates Orders directly (IOrderService is mocked), so this is
// purely about PendingCheckout bookkeeping, availability validation, and the refund-on-failure path.
public class CheckoutServiceTests
{
    private const string CustomerId = "customer-1";
    private const string OtherCustomerId = "customer-2";

    private sealed record Fixture(
        CheckoutService Service,
        Mock<IStripeGateway> StripeGateway,
        Mock<IOrderService> OrderService,
        Mock<IBusinessService> BusinessService,
        Mock<IPackageRepository> PackageRepo,
        EcoMealDbContext Db);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var db = InMemoryDb.Create();
        var stripeGateway = new Mock<IStripeGateway>();
        var orderService = new Mock<IOrderService>();
        var businessService = new Mock<IBusinessService>();
        var packageRepo = new Mock<IPackageRepository>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId, roles));
        var configuration = new ConfigurationBuilder().Build();

        var service = new CheckoutService(
            stripeGateway.Object, orderService.Object, businessService.Object, packageRepo.Object,
            db, currentUser, configuration);

        return new Fixture(service, stripeGateway, orderService, businessService, packageRepo, db);
    }

    // ---- StartCheckoutAsync -------------------------------------------------

    [Fact]
    public async Task StartCheckoutAsync_NonCustomer_Throws()
    {
        var f = Build(CustomerId, AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.StartCheckoutAsync(Guid.NewGuid(), [new OrderLineRequest(Guid.NewGuid(), 1)]));
    }

    [Fact]
    public async Task StartCheckoutAsync_EmptyCart_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.StartCheckoutAsync(Guid.NewGuid(), []));
    }

    [Fact]
    public async Task StartCheckoutAsync_InsufficientStock_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 2);
        f.BusinessService.Setup(b => b.GetByIdAsync(businessId)).ReturnsAsync(TestData.Business(businessId));
        f.PackageRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.StartCheckoutAsync(businessId, [new OrderLineRequest(package.Id, 3)]));

        Assert.Contains("Only 2 left", ex.Message);
    }

    [Fact]
    public async Task StartCheckoutAsync_Success_SavesPendingCheckoutAndReturnsStripeUrl()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var businessId = Guid.NewGuid();
        var business = TestData.Business(businessId);
        var package = TestData.Package(businessId, quantity: 5);
        f.BusinessService.Setup(b => b.GetByIdAsync(businessId)).ReturnsAsync(business);
        f.PackageRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);
        f.StripeGateway
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Guid>(), business.Name, It.IsAny<List<CheckoutLineItem>>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_test", "https://checkout.stripe.com/cs_test"));

        var url = await f.Service.StartCheckoutAsync(businessId, [new OrderLineRequest(package.Id, 2)]);

        Assert.Equal("https://checkout.stripe.com/cs_test", url);
        var pendingCheckout = await f.Db.PendingCheckouts.SingleAsync();
        Assert.Equal(CustomerId, pendingCheckout.UserId);
        Assert.Equal(businessId, pendingCheckout.BusinessId);
        Assert.Equal("cs_test", pendingCheckout.StripeCheckoutSessionId);
        Assert.Null(pendingCheckout.ConsumedAt);
    }

    // ---- CompleteCheckoutAsync -----------------------------------------------

    [Fact]
    public async Task CompleteCheckoutAsync_UnknownPendingCheckout_ReturnsFailure()
    {
        var f = Build(CustomerId, AppRoles.Customer);

        var result = await f.Service.CompleteCheckoutAsync(Guid.NewGuid(), "cs_test");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CompleteCheckoutAsync_DifferentUser_Throws()
    {
        var f = Build(OtherCustomerId, AppRoles.Customer);
        var pendingCheckout = new PendingCheckout
        {
            Id = Guid.NewGuid(), UserId = CustomerId, BusinessId = Guid.NewGuid(),
            LinesJson = "[]", StripeCheckoutSessionId = "cs_test", CreatedAt = DateTime.UtcNow,
        };
        f.Db.PendingCheckouts.Add(pendingCheckout);
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.CompleteCheckoutAsync(pendingCheckout.Id, "cs_test"));
    }

    [Fact]
    public async Task CompleteCheckoutAsync_PaymentNotPaid_ReturnsFailure()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var pendingCheckout = new PendingCheckout
        {
            Id = Guid.NewGuid(), UserId = CustomerId, BusinessId = Guid.NewGuid(),
            LinesJson = "[]", StripeCheckoutSessionId = "cs_test", CreatedAt = DateTime.UtcNow,
        };
        f.Db.PendingCheckouts.Add(pendingCheckout);
        await f.Db.SaveChangesAsync();
        f.StripeGateway.Setup(s => s.GetSessionStatusAsync("cs_test"))
            .ReturnsAsync(new StripeSessionStatus(false, null, 0m, "ron"));

        var result = await f.Service.CompleteCheckoutAsync(pendingCheckout.Id, "cs_test");

        Assert.False(result.Success);
        f.OrderService.Verify(o => o.PlaceOrderAsync(It.IsAny<Guid>(), It.IsAny<List<OrderLineRequest>>()), Times.Never);
    }

    [Fact]
    public async Task CompleteCheckoutAsync_Success_CreatesPaymentAndMarksConsumed()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 5);
        var user = TestData.User(CustomerId);
        var placedOrder = TestData.Order(user, businessId, OrderStatuses.Pending, (package, 2));

        var pendingCheckout = new PendingCheckout
        {
            Id = Guid.NewGuid(), UserId = CustomerId, BusinessId = businessId,
            LinesJson = $"[{{\"PackageId\":\"{package.Id}\",\"Quantity\":2}}]",
            StripeCheckoutSessionId = "cs_test", CreatedAt = DateTime.UtcNow,
        };
        f.Db.PendingCheckouts.Add(pendingCheckout);
        // The real OrderService.PlaceOrderAsync would persist the order through the same shared
        // DbContext CheckoutService reloads it from — simulate that here since PlaceOrderAsync
        // itself is mocked. Null out Status first: TestData.Order() builds a fresh Status instance
        // with the same Id InMemoryDb already seeded, which conflicts with the tracked one on Add.
        placedOrder.Status = null!;
        f.Db.Orders.Add(placedOrder);
        await f.Db.SaveChangesAsync();

        f.StripeGateway.Setup(s => s.GetSessionStatusAsync("cs_test"))
            .ReturnsAsync(new StripeSessionStatus(true, "pi_test", 20m, "ron"));
        f.OrderService.Setup(o => o.PlaceOrderAsync(businessId, It.Is<List<OrderLineRequest>>(l => l.Count == 1 && l[0].Quantity == 2)))
            .ReturnsAsync(placedOrder);

        var result = await f.Service.CompleteCheckoutAsync(pendingCheckout.Id, "cs_test");

        Assert.True(result.Success);
        Assert.Equal(placedOrder.Id, result.Order?.Id);

        var payment = await f.Db.Payments.SingleAsync(p => p.OrderId == placedOrder.Id);
        Assert.Equal(PaymentStatuses.Succeeded, payment.Status);
        Assert.Equal("pi_test", payment.StripePaymentIntentId);
        Assert.Equal(20m, payment.Amount);

        var reloadedCheckout = await f.Db.PendingCheckouts.SingleAsync(p => p.Id == pendingCheckout.Id);
        Assert.NotNull(reloadedCheckout.ConsumedAt);
        Assert.Equal(placedOrder.Id, reloadedCheckout.ResultingOrderId);
    }

    [Fact]
    public async Task CompleteCheckoutAsync_OrderPlacementFails_RefundsAndReturnsFailure()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var businessId = Guid.NewGuid();
        var pendingCheckout = new PendingCheckout
        {
            Id = Guid.NewGuid(), UserId = CustomerId, BusinessId = businessId,
            LinesJson = "[]", StripeCheckoutSessionId = "cs_test", CreatedAt = DateTime.UtcNow,
        };
        f.Db.PendingCheckouts.Add(pendingCheckout);
        await f.Db.SaveChangesAsync();

        f.StripeGateway.Setup(s => s.GetSessionStatusAsync("cs_test"))
            .ReturnsAsync(new StripeSessionStatus(true, "pi_test", 20m, "ron"));
        f.OrderService.Setup(o => o.PlaceOrderAsync(It.IsAny<Guid>(), It.IsAny<List<OrderLineRequest>>()))
            .ThrowsAsync(new InvalidOperationException("Stock vanished."));

        var result = await f.Service.CompleteCheckoutAsync(pendingCheckout.Id, "cs_test");

        Assert.False(result.Success);
        f.StripeGateway.Verify(s => s.RefundAsync("pi_test"), Times.Once);
        var reloadedCheckout = await f.Db.PendingCheckouts.SingleAsync(p => p.Id == pendingCheckout.Id);
        Assert.NotNull(reloadedCheckout.ConsumedAt);
        Assert.Null(reloadedCheckout.ResultingOrderId);
        Assert.False(await f.Db.Payments.AnyAsync());
    }

    [Fact]
    public async Task CompleteCheckoutAsync_AlreadyConsumed_ReplaysWithoutPlacingAnotherOrder()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var businessId = Guid.NewGuid();
        var user = TestData.User(CustomerId);
        var package = TestData.Package(businessId);
        var order = TestData.Order(user, businessId, OrderStatuses.Pending, (package, 1));
        order.Status = null!; // see comment in the Success test above
        f.Db.Orders.Add(order);

        var pendingCheckout = new PendingCheckout
        {
            Id = Guid.NewGuid(), UserId = CustomerId, BusinessId = businessId,
            LinesJson = "[]", StripeCheckoutSessionId = "cs_test", CreatedAt = DateTime.UtcNow,
            ConsumedAt = DateTime.UtcNow, ResultingOrderId = order.Id,
        };
        f.Db.PendingCheckouts.Add(pendingCheckout);
        await f.Db.SaveChangesAsync();

        var result = await f.Service.CompleteCheckoutAsync(pendingCheckout.Id, "cs_test");

        Assert.True(result.Success);
        Assert.Equal(order.Id, result.Order?.Id);
        f.OrderService.Verify(o => o.PlaceOrderAsync(It.IsAny<Guid>(), It.IsAny<List<OrderLineRequest>>()), Times.Never);
        f.StripeGateway.Verify(s => s.GetSessionStatusAsync(It.IsAny<string>()), Times.Never);
    }
}
