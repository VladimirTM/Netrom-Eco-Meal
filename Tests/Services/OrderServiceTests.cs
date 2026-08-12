using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;
using Netrom_Eco_Meal.Tests.TestSupport;

namespace Netrom_Eco_Meal.Tests.Services;

// Covers OrderService's status-transition/stock logic — the trickiest logic in the app per
// FEATURE_IDEAS.md's Phase 4. IOrderRepository/IPackageRepository/IBusinessService/
// INotificationService are mocked; EcoMealDbContext is a real InMemory-backed instance since
// OrderService queries it directly (rate limiting, status lookups, pending-reservation sums).
public class OrderServiceTests
{
    private const string CustomerId = "customer-1";
    private const string OtherCustomerId = "customer-2";
    private const string ManagerId = "manager-1";
    private const string ManagerId2 = "manager-2";
    private const string AdminId = "admin-1";

    private sealed record Fixture(
        OrderService Service,
        Mock<IOrderRepository> OrderRepo,
        Mock<IPackageRepository> PackageRepo,
        Mock<IBusinessService> BusinessService,
        Mock<INotificationService> NotificationService,
        Mock<IAppEmailSender> EmailSender,
        Mock<IStripeGateway> StripeGateway,
        Mock<IAuditLogService> AuditLogService,
        EcoMealDbContext Db);

    private static Fixture Build(string? userId, params string[] roles)
    {
        var db = InMemoryDb.Create();
        var orderRepo = new Mock<IOrderRepository>();
        var packageRepo = new Mock<IPackageRepository>();
        var businessService = new Mock<IBusinessService>();
        var notificationService = new Mock<INotificationService>();
        var emailSender = new Mock<IAppEmailSender>();
        var stripeGateway = new Mock<IStripeGateway>();
        var auditLogService = new Mock<IAuditLogService>();
        var currentUser = new CurrentUserAccessor(new FakeAuthenticationStateProvider(userId, roles));
        var configuration = new ConfigurationBuilder().Build();

        var service = new OrderService(
            orderRepo.Object, packageRepo.Object, businessService.Object, notificationService.Object,
            emailSender.Object, stripeGateway.Object, auditLogService.Object, db, currentUser, configuration, NullLogger<OrderService>.Instance);

        return new Fixture(service, orderRepo, packageRepo, businessService, notificationService, emailSender, stripeGateway, auditLogService, db);
    }

    // ---- PlaceOrderAsync ---------------------------------------------------

    [Fact]
    public async Task PlaceOrderAsync_NonCustomer_Throws()
    {
        var f = Build(AdminId, AppRoles.Admin);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.PlaceOrderAsync(Guid.NewGuid(), [new OrderLineRequest(Guid.NewGuid(), 1)]));
    }

    [Fact]
    public async Task PlaceOrderAsync_EmptyCart_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        f.Db.Users.Add(TestData.User(CustomerId));
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.PlaceOrderAsync(Guid.NewGuid(), []));
    }

    [Fact]
    public async Task PlaceOrderAsync_RateLimitExceeded_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var user = TestData.User(CustomerId);
        f.Db.Users.Add(user);
        for (var i = 0; i < OrderRateLimit.MaxOrdersPerWindow; i++)
        {
            f.Db.Orders.Add(new Order
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                BusinessId = Guid.NewGuid(),
                StatusId = TestStatusIds.Pending,
                OrderNumber = i + 1,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            });
        }
        await f.Db.SaveChangesAsync();

        var businessId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.PlaceOrderAsync(businessId, [new OrderLineRequest(Guid.NewGuid(), 1)]));

        Assert.Contains($"{OrderRateLimit.MaxOrdersPerWindow} orders", ex.Message);
    }

    [Fact]
    public async Task PlaceOrderAsync_QuantityZero_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        f.Db.Users.Add(TestData.User(CustomerId));
        await f.Db.SaveChangesAsync();

        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        f.PackageRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.PlaceOrderAsync(businessId, [new OrderLineRequest(package.Id, 0)]));
    }

    [Fact]
    public async Task PlaceOrderAsync_PackageFromDifferentBusiness_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        f.Db.Users.Add(TestData.User(CustomerId));
        await f.Db.SaveChangesAsync();

        var businessId = Guid.NewGuid();
        // The package belongs to a different business than businessId.
        var package = TestData.Package(Guid.NewGuid());
        f.PackageRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.PlaceOrderAsync(businessId, [new OrderLineRequest(package.Id, 1)]));

        Assert.Contains("no longer available", ex.Message);
    }

    [Fact]
    public async Task PlaceOrderAsync_AccountsForOtherCustomersPendingReservations_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var user = TestData.User(CustomerId);
        f.Db.Users.Add(user);

        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 5);
        f.PackageRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);

        // Someone else already has a Pending reservation for 4 of the 5 units.
        var otherUser = TestData.User(OtherCustomerId);
        f.Db.Users.Add(otherUser);
        var otherOrder = new Order
        {
            Id = Guid.NewGuid(),
            UserId = otherUser.Id,
            User = otherUser,
            BusinessId = businessId,
            StatusId = TestStatusIds.Pending,
            OrderNumber = 1,
            CreatedAt = DateTime.UtcNow,
        };
        otherOrder.OrderPackages.Add(new OrderPackage { Id = Guid.NewGuid(), OrderId = otherOrder.Id, PackageId = package.Id, Quantity = 4 });
        f.Db.Orders.Add(otherOrder);
        await f.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.PlaceOrderAsync(businessId, [new OrderLineRequest(package.Id, 2)]));

        Assert.Contains("Only 1 left", ex.Message);
    }

    [Fact]
    public async Task PlaceOrderAsync_Success_CreatesOrderAndNotifiesEveryStaffMember()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var user = TestData.User(CustomerId);
        f.Db.Users.Add(user);
        await f.Db.SaveChangesAsync();

        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 5);
        f.PackageRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);
        f.BusinessService.Setup(b => b.GetByIdAsync(businessId))
            .ReturnsAsync(TestData.Business(businessId));
        f.BusinessService.Setup(b => b.GetStaffAsync(businessId))
            .ReturnsAsync([TestData.User(ManagerId), TestData.User(ManagerId2)]);

        var order = await f.Service.PlaceOrderAsync(businessId, [new OrderLineRequest(package.Id, 2)]);

        Assert.Equal(TestStatusIds.Pending, order.StatusId);
        Assert.Single(order.OrderPackages);
        Assert.Equal(2, order.OrderPackages.First().Quantity);
        f.OrderRepo.Verify(r => r.AddAsync(order), Times.Once);
        f.OrderRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        f.NotificationService.Verify(n => n.CreateAsync(ManagerId, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        f.NotificationService.Verify(n => n.CreateAsync(ManagerId2, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PlaceOrderAsync_BusinessWithoutStaff_DoesNotNotify()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var user = TestData.User(CustomerId);
        f.Db.Users.Add(user);
        await f.Db.SaveChangesAsync();

        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 5);
        f.PackageRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([package]);
        f.BusinessService.Setup(b => b.GetByIdAsync(businessId))
            .ReturnsAsync(TestData.Business(businessId));
        f.BusinessService.Setup(b => b.GetStaffAsync(businessId)).ReturnsAsync([]);

        await f.Service.PlaceOrderAsync(businessId, [new OrderLineRequest(package.Id, 1)]);

        f.NotificationService.Verify(n => n.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---- Status transitions (confirm/complete/cancel) ----------------------

    [Fact]
    public async Task UpdateStatusAsync_Admin_ConfirmDecrementsStock()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 5);
        var order = TestData.Order(user, businessId, OrderStatuses.Pending, (package, 2));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await f.Service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed);

        Assert.Equal(TestStatusIds.Confirmed, result.StatusId);
        Assert.Equal(3, package.Quantity);
        f.OrderRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        f.NotificationService.Verify(n => n.CreateAsync(user.Id, It.Is<string>(m => m.Contains("confirmed")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_Confirm_InsufficientStock_Throws()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 1);
        var order = TestData.Order(user, businessId, OrderStatuses.Pending, (package, 2));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed));

        Assert.Contains("Only 1 left", ex.Message);
        // Untouched on failure.
        Assert.Equal(1, package.Quantity);
    }

    [Fact]
    public async Task UpdateStatusAsync_ConfirmedToCompleted_DoesNotChangeStock()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 3);
        var order = TestData.Order(user, businessId, OrderStatuses.Confirmed, (package, 2));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await f.Service.UpdateStatusAsync(order.Id, OrderStatuses.Completed);

        Assert.Equal(TestStatusIds.Completed, result.StatusId);
        Assert.Equal(3, package.Quantity);
    }

    [Fact]
    public async Task UpdateStatusAsync_ConfirmedToCancelled_RestoresStock()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 3);
        var order = TestData.Order(user, businessId, OrderStatuses.Confirmed, (package, 2));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await f.Service.UpdateStatusAsync(order.Id, OrderStatuses.Cancelled);

        Assert.Equal(TestStatusIds.Cancelled, result.StatusId);
        Assert.Equal(5, package.Quantity);
    }

    [Fact]
    public async Task UpdateStatusAsync_ConfirmedToCancelled_RefundsPaidOrder()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 3);
        var order = TestData.Order(user, businessId, OrderStatuses.Confirmed, (package, 2));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);
        f.Db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), OrderId = order.Id, Amount = 20m, Currency = "ron",
            StripeCheckoutSessionId = "cs_test", StripePaymentIntentId = "pi_test",
            Status = PaymentStatuses.Succeeded, CreatedAt = DateTime.UtcNow,
        });
        await f.Db.SaveChangesAsync();

        await f.Service.UpdateStatusAsync(order.Id, OrderStatuses.Cancelled);

        f.StripeGateway.Verify(s => s.RefundAsync("pi_test"), Times.Once);
        var payment = await f.Db.Payments.FirstAsync(p => p.OrderId == order.Id);
        Assert.Equal(PaymentStatuses.Refunded, payment.Status);
        Assert.NotNull(payment.RefundedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_ConfirmedToNoShow_RestoresStock()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 3);
        var order = TestData.Order(user, businessId, OrderStatuses.Confirmed, (package, 2));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await f.Service.UpdateStatusAsync(order.Id, OrderStatuses.NoShow);

        Assert.Equal(TestStatusIds.NoShow, result.StatusId);
        Assert.Equal(5, package.Quantity);
        f.NotificationService.Verify(n => n.CreateAsync(user.Id, It.Is<string>(m => m.Contains("no-show")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_ConfirmedToNoShow_DoesNotRefund()
    {
        // No-show deliberately keeps the charge — that's the no-show fee (see RefundIfPaidAsync).
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 3);
        var order = TestData.Order(user, businessId, OrderStatuses.Confirmed, (package, 2));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);
        f.Db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), OrderId = order.Id, Amount = 20m, Currency = "ron",
            StripeCheckoutSessionId = "cs_test", StripePaymentIntentId = "pi_test",
            Status = PaymentStatuses.Succeeded, CreatedAt = DateTime.UtcNow,
        });
        await f.Db.SaveChangesAsync();

        await f.Service.UpdateStatusAsync(order.Id, OrderStatuses.NoShow);

        f.StripeGateway.Verify(s => s.RefundAsync(It.IsAny<string>()), Times.Never);
        var payment = await f.Db.Payments.FirstAsync(p => p.OrderId == order.Id);
        Assert.Equal(PaymentStatuses.Succeeded, payment.Status);
    }

    [Theory]
    [InlineData(OrderStatuses.Pending, OrderStatuses.Completed)]
    [InlineData(OrderStatuses.Completed, OrderStatuses.Confirmed)]
    [InlineData(OrderStatuses.Cancelled, OrderStatuses.Confirmed)]
    public async Task UpdateStatusAsync_IllegalTransition_Throws(string from, string to)
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var order = TestData.Order(user, businessId, from, (package, 1));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.UpdateStatusAsync(order.Id, to));

        Assert.Contains($"can't move from {from} to {to}", ex.Message);
    }

    [Fact]
    public async Task UpdateStatusAsync_ManagerNotStaffOfOrdersBusiness_Throws()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);

        var user = TestData.User(CustomerId);
        var otherBusinessId = Guid.NewGuid();
        var package = TestData.Package(otherBusinessId);
        var order = TestData.Order(user, otherBusinessId, OrderStatuses.Pending, (package, 1));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);
        f.BusinessService.Setup(b => b.IsStaffAsync(otherBusinessId, ManagerId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed));
    }

    [Fact]
    public async Task UpdateStatusAsync_ManagerStaffOfOrdersBusiness_Succeeds()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);
        var businessId = Guid.NewGuid();
        f.BusinessService.Setup(b => b.IsStaffAsync(businessId, ManagerId)).ReturnsAsync(true);

        var user = TestData.User(CustomerId);
        var package = TestData.Package(businessId, quantity: 5);
        var order = TestData.Order(user, businessId, OrderStatuses.Pending, (package, 1));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await f.Service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed);

        Assert.Equal(TestStatusIds.Confirmed, result.StatusId);
    }

    [Fact]
    public async Task UpdateStatusAsync_ManagerStaffOfSecondBusiness_Succeeds()
    {
        // A single manager can now staff more than one business — confirming an order for
        // whichever one they're staff of (not just a single "own business") must work.
        var f = Build(ManagerId, AppRoles.BusinessManager);
        var firstBusinessId = Guid.NewGuid();
        var secondBusinessId = Guid.NewGuid();
        f.BusinessService.Setup(b => b.IsStaffAsync(firstBusinessId, ManagerId)).ReturnsAsync(true);
        f.BusinessService.Setup(b => b.IsStaffAsync(secondBusinessId, ManagerId)).ReturnsAsync(true);

        var user = TestData.User(CustomerId);
        var package = TestData.Package(secondBusinessId, quantity: 5);
        var order = TestData.Order(user, secondBusinessId, OrderStatuses.Pending, (package, 1));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await f.Service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed);

        Assert.Equal(TestStatusIds.Confirmed, result.StatusId);
    }

    [Fact]
    public async Task GetOrdersForManagementAsync_ManagerWithoutBusinessId_Throws()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.GetOrdersForManagementAsync(null));
    }

    [Fact]
    public async Task GetOrdersForManagementAsync_ManagerNotStaffOfRequestedBusiness_Throws()
    {
        var f = Build(ManagerId, AppRoles.BusinessManager);
        var businessId = Guid.NewGuid();
        f.BusinessService.Setup(b => b.IsStaffAsync(businessId, ManagerId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.GetOrdersForManagementAsync(businessId));
    }

    // ---- CancelMyOrderAsync -------------------------------------------------

    [Fact]
    public async Task CancelMyOrderAsync_NotOwner_Throws()
    {
        var f = Build(OtherCustomerId, AppRoles.Customer);
        var owner = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var order = TestData.Order(owner, businessId, OrderStatuses.Pending, (package, 1));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.CancelMyOrderAsync(order.Id));
    }

    [Fact]
    public async Task CancelMyOrderAsync_Owner_Succeeds()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var owner = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var order = TestData.Order(owner, businessId, OrderStatuses.Pending, (package, 1));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await f.Service.CancelMyOrderAsync(order.Id);

        Assert.Equal(TestStatusIds.Cancelled, result.StatusId);
        f.NotificationService.Verify(n => n.CreateAsync(owner.Id, It.Is<string>(m => m.Contains("cancelled")), It.IsAny<string>()), Times.Once);
    }

    // ---- Pickup passes (Phase 1: multiple pickup passes / split QR) --------

    [Fact]
    public async Task UpdateStatusAsync_Confirm_CreatesDefaultPickupPass()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 5);
        var order = TestData.Order(user, businessId, OrderStatuses.Pending, (package, 2));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        await f.Service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed);

        // order is a hand-built, untracked POCO (not loaded via the real DbContext), so EF's
        // relationship fixup can't populate order.PickupPasses from the DbSet.Add() call the way it
        // would for a real, tracked order. orderRepository.SaveChangesAsync() is also mocked (a
        // no-op), so a query against the DbSet wouldn't see it either — check the change tracker's
        // local (unsaved) view instead, via OrderPickupPasses.Local.
        var pass = Assert.Single(f.Db.OrderPickupPasses.Local, p => p.OrderId == order.Id);
        Assert.Null(pass.RedeemedAt);
    }

    [Fact]
    public async Task SplitPickupPassesAsync_NotOwner_Throws()
    {
        var f = Build(OtherCustomerId, AppRoles.Customer);
        var owner = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var order = TestData.Order(owner, businessId, OrderStatuses.Confirmed, (package, 1));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SplitPickupPassesAsync(order.Id, 3));
    }

    [Fact]
    public async Task SplitPickupPassesAsync_OrderNotConfirmed_Throws()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var owner = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var order = TestData.Order(owner, businessId, OrderStatuses.Pending, (package, 1));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SplitPickupPassesAsync(order.Id, 3));
        Assert.Contains("confirmed", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task SplitPickupPassesAsync_OutOfRange_Throws(int passCount)
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var owner = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var order = TestData.Order(owner, businessId, OrderStatuses.Confirmed, (package, 1));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SplitPickupPassesAsync(order.Id, passCount));
    }

    [Fact]
    public async Task SplitPickupPassesAsync_Success_ReplacesPassesWithFreshSet()
    {
        var f = Build(CustomerId, AppRoles.Customer);
        var owner = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var order = TestData.Order(owner, businessId, OrderStatuses.Confirmed, (package, 1));
        order.PickupPasses.Add(new OrderPickupPass { Id = Guid.NewGuid(), OrderId = order.Id, Label = "Pickup pass", CreatedAt = DateTime.UtcNow });
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        await f.Service.SplitPickupPassesAsync(order.Id, 3);

        // order is a hand-built, untracked POCO, so fixup can't populate order.PickupPasses from
        // the DbSet.Add() call, and SaveChangesAsync is mocked — check the change tracker's local
        // (unsaved) view instead (see UpdateStatusAsync_Confirm_CreatesDefaultPickupPass).
        var newPasses = f.Db.OrderPickupPasses.Local.Where(p => p.OrderId == order.Id).ToList();
        Assert.Equal(3, newPasses.Count);
        Assert.Equal(new[] { "Pass 1", "Pass 2", "Pass 3" }, newPasses.Select(p => p.Label).OrderBy(l => l));
        Assert.All(newPasses, p => Assert.Null(p.RedeemedAt));
    }

    [Fact]
    public async Task RedeemPickupPassAsync_UnknownPass_Throws()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var order = TestData.Order(user, businessId, OrderStatuses.Confirmed, (package, 1));
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.RedeemPickupPassAsync(order.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task RedeemPickupPassAsync_AlreadyRedeemed_Throws()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 3);
        var order = TestData.Order(user, businessId, OrderStatuses.Confirmed, (package, 2));
        var pass = new OrderPickupPass { Id = Guid.NewGuid(), OrderId = order.Id, Label = "Pickup pass", CreatedAt = DateTime.UtcNow, RedeemedAt = DateTime.UtcNow };
        order.PickupPasses.Add(pass);
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.RedeemPickupPassAsync(order.Id, pass.Id));
        Assert.Contains("already used", ex.Message);
    }

    [Fact]
    public async Task RedeemPickupPassAsync_OrderNotConfirmed_Throws()
    {
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var order = TestData.Order(user, businessId, OrderStatuses.Cancelled, (package, 1));
        var pass = new OrderPickupPass { Id = Guid.NewGuid(), OrderId = order.Id, Label = "Pickup pass", CreatedAt = DateTime.UtcNow };
        order.PickupPasses.Add(pass);
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.RedeemPickupPassAsync(order.Id, pass.Id));
    }

    [Fact]
    public async Task RedeemPickupPassAsync_Success_RedeemsPassAndCompletesWholeOrder()
    {
        // Redeeming one pass out of several completes the whole order — the "whoever gets there
        // first" semantics FEATURE_IDEAS.md's Phase 1 calls for.
        var f = Build(AdminId, AppRoles.Admin);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 3);
        var order = TestData.Order(user, businessId, OrderStatuses.Confirmed, (package, 2));
        var pass1 = new OrderPickupPass { Id = Guid.NewGuid(), OrderId = order.Id, Label = "Pass 1", CreatedAt = DateTime.UtcNow };
        var pass2 = new OrderPickupPass { Id = Guid.NewGuid(), OrderId = order.Id, Label = "Pass 2", CreatedAt = DateTime.UtcNow };
        order.PickupPasses.Add(pass1);
        order.PickupPasses.Add(pass2);
        f.OrderRepo.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await f.Service.RedeemPickupPassAsync(order.Id, pass1.Id);

        Assert.Equal(TestStatusIds.Completed, result.StatusId);
        Assert.NotNull(pass1.RedeemedAt);
        Assert.Null(pass2.RedeemedAt);
        // Completing an order never touches stock — it was already decremented at Confirm time.
        Assert.Equal(3, package.Quantity);
    }

    // ---- ExpireStalePendingOrdersAsync -------------------------------------

    [Fact]
    public async Task ExpireStalePendingOrdersAsync_CancelsAndNotifiesEach()
    {
        var f = Build(null);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var stale = new List<Order>
        {
            TestData.Order(user, businessId, OrderStatuses.Pending, (package, 1)),
            TestData.Order(user, businessId, OrderStatuses.Pending, (package, 1)),
        };
        f.OrderRepo.Setup(r => r.GetStalePendingOrdersAsync(It.IsAny<DateTime>())).ReturnsAsync(stale);

        var count = await f.Service.ExpireStalePendingOrdersAsync();

        Assert.Equal(2, count);
        Assert.All(stale, o => Assert.Equal(TestStatusIds.Cancelled, o.StatusId));
        f.OrderRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        f.NotificationService.Verify(n => n.CreateAsync(user.Id, It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExpireStalePendingOrdersAsync_RefundsPaidOrders()
    {
        // Pending orders are only ever created after a successful Stripe payment (see
        // CheckoutService), so an auto-expired one needs refunding just like a manual cancel.
        var f = Build(null);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var stale = TestData.Order(user, businessId, OrderStatuses.Pending, (package, 1));
        f.OrderRepo.Setup(r => r.GetStalePendingOrdersAsync(It.IsAny<DateTime>())).ReturnsAsync([stale]);
        f.Db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), OrderId = stale.Id, Amount = 10m, Currency = "ron",
            StripeCheckoutSessionId = "cs_test", StripePaymentIntentId = "pi_test",
            Status = PaymentStatuses.Succeeded, CreatedAt = DateTime.UtcNow,
        });
        await f.Db.SaveChangesAsync();

        await f.Service.ExpireStalePendingOrdersAsync();

        f.StripeGateway.Verify(s => s.RefundAsync("pi_test"), Times.Once);
        var payment = await f.Db.Payments.FirstAsync(p => p.OrderId == stale.Id);
        Assert.Equal(PaymentStatuses.Refunded, payment.Status);
    }

    [Fact]
    public async Task ExpireStalePendingOrdersAsync_NoStaleOrders_DoesNotSave()
    {
        var f = Build(null);
        f.OrderRepo.Setup(r => r.GetStalePendingOrdersAsync(It.IsAny<DateTime>())).ReturnsAsync([]);

        var count = await f.Service.ExpireStalePendingOrdersAsync();

        Assert.Equal(0, count);
        f.OrderRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
        f.NotificationService.Verify(n => n.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---- ExpireNoShowOrdersAsync --------------------------------------------

    [Fact]
    public async Task ExpireNoShowOrdersAsync_MarksOverdueOrdersAndRestoresStock()
    {
        var f = Build(null);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId, quantity: 3);
        var overdue = TestData.Order(user, businessId, OrderStatuses.Confirmed, (package, 2));
        f.OrderRepo.Setup(r => r.GetOverduePickupOrdersAsync(It.IsAny<DateTime>())).ReturnsAsync([overdue]);

        var count = await f.Service.ExpireNoShowOrdersAsync();

        Assert.Equal(1, count);
        Assert.Equal(TestStatusIds.NoShow, overdue.StatusId);
        Assert.Equal(5, package.Quantity);
        f.OrderRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        f.NotificationService.Verify(n => n.CreateAsync(user.Id, It.Is<string>(m => m.Contains("no-show")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ExpireNoShowOrdersAsync_NoOverdueOrders_DoesNotSave()
    {
        var f = Build(null);
        f.OrderRepo.Setup(r => r.GetOverduePickupOrdersAsync(It.IsAny<DateTime>())).ReturnsAsync([]);

        var count = await f.Service.ExpireNoShowOrdersAsync();

        Assert.Equal(0, count);
        f.OrderRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // ---- SendPickupRemindersAsync -------------------------------------------

    [Fact]
    public async Task SendPickupRemindersAsync_MarksSentAndNotifiesEach()
    {
        var f = Build(null);
        var user = TestData.User(CustomerId);
        var businessId = Guid.NewGuid();
        var package = TestData.Package(businessId);
        var order = TestData.Order(user, businessId, OrderStatuses.Confirmed, (package, 1));
        f.OrderRepo.Setup(r => r.GetPickupReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([order]);

        var count = await f.Service.SendPickupRemindersAsync();

        Assert.Equal(1, count);
        Assert.NotNull(order.PickupReminderSentAt);
        f.OrderRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        f.NotificationService.Verify(n => n.CreateAsync(user.Id, It.Is<string>(m => m.Contains("closes soon")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SendPickupRemindersAsync_NoCandidates_DoesNotSave()
    {
        var f = Build(null);
        f.OrderRepo.Setup(r => r.GetPickupReminderCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync([]);

        var count = await f.Service.SendPickupRemindersAsync();

        Assert.Equal(0, count);
        f.OrderRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }
}
