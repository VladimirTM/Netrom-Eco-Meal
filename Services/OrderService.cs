using System.Net;
using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class OrderService(
    IOrderRepository orderRepository,
    IPackageRepository packageRepository,
    IBusinessService businessService,
    INotificationService notificationService,
    IAppEmailSender emailSender,
    EcoMealDbContext dbContext,
    CurrentUserAccessor currentUser) : IOrderService
{
    public async Task<Order> PlaceOrderAsync(Guid businessId, List<OrderLineRequest> lines)
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can place orders.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to place an order.");

        if (lines.Count == 0)
            throw new InvalidOperationException("Your cart is empty.");

        var user = await dbContext.Users.FindAsync(userId)
            ?? throw new UnauthorizedAccessException("Your account could not be found.");

        var windowStart = DateTime.UtcNow - OrderRateLimit.Window;
        var recentOrderCount = await dbContext.Orders.CountAsync(o => o.UserId == userId && o.CreatedAt >= windowStart);
        if (recentOrderCount >= OrderRateLimit.MaxOrdersPerWindow)
            throw new InvalidOperationException($"You've placed {OrderRateLimit.MaxOrdersPerWindow} orders in the last {OrderRateLimit.Window.TotalMinutes:0} minutes — please wait a bit before placing another.");

        var pendingStatus = await dbContext.Statuses.FirstOrDefaultAsync(s => s.Name == OrderStatuses.Pending)
            ?? throw new InvalidOperationException("Order status configuration is missing.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = user,
            BusinessId = businessId,
            StatusId = pendingStatus.Id,
            CreatedAt = DateTime.UtcNow,
            // OrderNumber comes from the order_numbers DB sequence on insert (see EcoMealDbContext).
        };

        var packageIds = lines.Select(l => l.PackageId).Distinct().ToList();
        var packages = (await packageRepository.GetByIdsAsync(packageIds)).ToDictionary(p => p.Id);

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Quantity must be greater than zero.");

            if (!packages.TryGetValue(line.PackageId, out var package) || package.BusinessId != businessId)
                throw new InvalidOperationException("One of the packages in your cart is no longer available.");

            // Package.Quantity only drops on confirm, so also subtract other customers' Pending reservations.
            var pendingElsewhere = await dbContext.OrderPackages
                .Where(op => op.PackageId == package.Id && op.Order.Status.Name == OrderStatuses.Pending)
                .SumAsync(op => (int?)op.Quantity) ?? 0;
            var available = package.Quantity - pendingElsewhere;

            if (line.Quantity > available)
                throw new InvalidOperationException($"Only {Math.Max(available, 0)} left of \"{package.Name}\" — please adjust your cart.");

            // Stock is reserved on confirm, not here — see UpdateStatusAsync.
            order.OrderPackages.Add(new OrderPackage
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PackageId = package.Id,
                Quantity = line.Quantity,
            });
        }

        await orderRepository.AddAsync(order);
        await orderRepository.SaveChangesAsync();

        var business = await businessService.GetByIdAsync(businessId);
        if (business?.ManagerId is not null)
        {
            await notificationService.CreateAsync(business.ManagerId,
                $"New order #{order.OrderNumber:000} from {user.Name} at {business.Name} needs confirmation.",
                "/orders/manage");
        }

        return order;
    }

    public async Task<List<Order>> GetMyOrdersAsync()
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can view their orders.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to view your orders.");

        return await orderRepository.GetByUserIdAsync(userId);
    }

    public async Task<List<Order>> GetOrdersForManagementAsync()
    {
        var (isAdmin, business) = await ResolveManagedBusinessAsync();
        return isAdmin ? await orderRepository.GetAllAsync() : await orderRepository.GetByBusinessIdAsync(business!.Id);
    }

    public async Task<PaginatedList<Order>> GetMyOrdersPagedAsync(int pageIndex, int pageSize, string? status)
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can view their orders.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to view your orders.");

        return await orderRepository.GetPagedByUserIdAsync(userId, pageIndex, pageSize, status);
    }

    public async Task<PaginatedList<Order>> GetOrdersForManagementPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessId, string? status)
    {
        var (isAdmin, business) = await ResolveManagedBusinessAsync();
        var effectiveBusinessId = isAdmin ? businessId : business!.Id;

        return await orderRepository.GetPagedForManagementAsync(pageIndex, pageSize, search, effectiveBusinessId, status);
    }

    public async Task<List<Order>> GetOrdersInRangeAsync(DateTime? from, DateTime? to, Guid? businessId = null)
    {
        var (isAdmin, business) = await ResolveManagedBusinessAsync();
        var effectiveBusinessId = isAdmin ? businessId : business!.Id;

        return await orderRepository.GetInRangeAsync(effectiveBusinessId, from, to);
    }

    public async Task<Order> UpdateStatusAsync(Guid orderId, string statusName)
    {
        var order = await GetOwnedOrderAsync(orderId);
        return await ApplyStatusChangeAsync(order, statusName);
    }

    // Read-only counterpart used by the QR pickup validation page.
    public async Task<Order> GetOrderForManagementAsync(Guid orderId) => await GetOwnedOrderAsync(orderId);

    public async Task<Order> GetMyOrderAsync(Guid orderId)
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can view their own orders.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to view this order.");

        var order = await orderRepository.GetByIdAsync(orderId)
            ?? throw new InvalidOperationException("This order no longer exists.");

        if (order.UserId != userId)
            throw new UnauthorizedAccessException("You can only view your own orders.");

        return order;
    }

    // Public aggregate for the home hero — no per-user data exposed, so no auth check needed.
    public async Task<decimal> GetTotalKgSavedAsync()
    {
        return await orderRepository.GetTotalWeightSavedKgAsync();
    }

    // No current-user auth (background-sweep call). Pending orders never touched Package.Quantity,
    // so cancelling needs no stock restore — it just stops counting against pendingElsewhere.
    public async Task<int> ExpireStalePendingOrdersAsync()
    {
        var cancelledStatus = await dbContext.Statuses.FirstOrDefaultAsync(s => s.Name == OrderStatuses.Cancelled)
            ?? throw new InvalidOperationException("Order status configuration is missing.");

        var staleOrders = await orderRepository.GetStalePendingOrdersAsync(DateTime.UtcNow - OrderExpiry.PendingTimeout);

        foreach (var order in staleOrders)
        {
            order.StatusId = cancelledStatus.Id;
            var message = $"Order #{order.OrderNumber:000} at {order.Business.Name} was automatically cancelled because it wasn't confirmed in time.";
            await notificationService.CreateAsync(order.UserId, message, "/orders");
            await SendCustomerEmailAsync(order, "Order cancelled", message);
        }

        if (staleOrders.Count > 0)
            await orderRepository.SaveChangesAsync();

        return staleOrders.Count;
    }

    // Confirmed orders whose pickup window fully closed without being marked Completed — restores
    // stock the same way a Confirmed->Cancelled transition does, since the reservation is being
    // released either way.
    public async Task<int> ExpireNoShowOrdersAsync()
    {
        var noShowStatus = await dbContext.Statuses.FirstOrDefaultAsync(s => s.Name == OrderStatuses.NoShow)
            ?? throw new InvalidOperationException("Order status configuration is missing.");

        var overdueOrders = await orderRepository.GetOverduePickupOrdersAsync(DateTime.UtcNow);

        foreach (var order in overdueOrders)
        {
            order.StatusId = noShowStatus.Id;
            foreach (var line in order.OrderPackages)
                line.Package.Quantity += line.Quantity;

            var message = $"Order #{order.OrderNumber:000} at {order.Business.Name} was marked as a no-show — the pickup window closed without it being picked up.";
            await notificationService.CreateAsync(order.UserId, message, "/orders");
            await SendCustomerEmailAsync(order, "Missed pickup", message);
        }

        if (overdueOrders.Count > 0)
            await orderRepository.SaveChangesAsync();

        return overdueOrders.Count;
    }

    // Reminds a customer shortly before a Confirmed order's pickup window closes. Idempotent via
    // PickupReminderSentAt, so the sweep can run every few minutes without double-reminding.
    public async Task<int> SendPickupRemindersAsync()
    {
        var now = DateTime.UtcNow;
        var candidates = await orderRepository.GetPickupReminderCandidatesAsync(now + OrderExpiry.PickupReminderLeadTime, now);

        foreach (var order in candidates)
        {
            order.PickupReminderSentAt = now;
            var message = $"Order #{order.OrderNumber:000} at {order.Business.Name} — your pickup window closes soon, don't forget to collect it.";
            await notificationService.CreateAsync(order.UserId, message, $"/orders/pickup/{order.Id}");
            await SendCustomerEmailAsync(order, "Pickup closes soon", message);
        }

        if (candidates.Count > 0)
            await orderRepository.SaveChangesAsync();

        return candidates.Count;
    }

    // Best-effort — a failed/unconfigured email should never break an order transition. The bell
    // notification (already created by the caller) remains the source of truth either way.
    private async Task SendCustomerEmailAsync(Order order, string subject, string message)
    {
        if (string.IsNullOrWhiteSpace(order.User.Email))
            return;

        var html = $"<p>Hi {WebUtility.HtmlEncode(order.User.Name)},</p><p>{WebUtility.HtmlEncode(message)}</p><p>— Eco Meal</p>";
        await emailSender.SendEmailAsync(order.User.Email, $"Eco Meal — {subject}", html);
    }

    public async Task<Dictionary<Guid, int>> GetPendingReservedQuantitiesAsync(IEnumerable<Guid> packageIds)
    {
        return await orderRepository.GetPendingQuantitiesByPackageIdsAsync(packageIds);
    }

    public async Task<Order> CancelMyOrderAsync(Guid orderId)
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can cancel their own orders.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to cancel an order.");

        var order = await orderRepository.GetByIdAsync(orderId)
            ?? throw new InvalidOperationException("This order no longer exists.");

        if (order.UserId != userId)
            throw new UnauthorizedAccessException("You can only cancel your own orders.");

        return await ApplyStatusChangeAsync(order, OrderStatuses.Cancelled);
    }

    // Shared by manager/admin status changes and customer self-cancellation — keeps the
    // transition rules and stock adjustments in exactly one place.
    private async Task<Order> ApplyStatusChangeAsync(Order order, string statusName)
    {
        var targetStatus = await dbContext.Statuses.FirstOrDefaultAsync(s => s.Name == statusName)
            ?? throw new InvalidOperationException("Order status configuration is missing.");

        var currentStatusName = order.Status.Name;
        var allowedTransition = (currentStatusName, statusName) switch
        {
            (OrderStatuses.Pending, OrderStatuses.Confirmed) => true,
            (OrderStatuses.Pending, OrderStatuses.Cancelled) => true,
            (OrderStatuses.Confirmed, OrderStatuses.Completed) => true,
            (OrderStatuses.Confirmed, OrderStatuses.Cancelled) => true,
            // Lets a manager mark a no-show by hand at the counter, in addition to the automatic
            // sweep once the pickup window fully closes (see ExpireNoShowOrdersAsync).
            (OrderStatuses.Confirmed, OrderStatuses.NoShow) => true,
            _ => false,
        };
        if (!allowedTransition)
            throw new InvalidOperationException($"An order can't move from {currentStatusName} to {statusName}.");

        if (statusName == OrderStatuses.Confirmed)
        {
            foreach (var line in order.OrderPackages)
            {
                if (line.Quantity > line.Package.Quantity)
                    throw new InvalidOperationException($"Only {line.Package.Quantity} left of \"{line.Package.Name}\" — can't confirm this order.");
            }

            foreach (var line in order.OrderPackages)
                line.Package.Quantity -= line.Quantity;
        }
        else if (currentStatusName == OrderStatuses.Confirmed && statusName is OrderStatuses.Cancelled or OrderStatuses.NoShow)
        {
            foreach (var line in order.OrderPackages)
                line.Package.Quantity += line.Quantity;
        }

        order.StatusId = targetStatus.Id;

        try
        {
            await orderRepository.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another confirm/cancel changed a package's stock (xmin token) — don't oversell.
            throw new InvalidOperationException("Stock for this order just changed — please refresh and try again.");
        }

        var (message, url, emailSubject) = statusName switch
        {
            OrderStatuses.Confirmed => ($"Order #{order.OrderNumber:000} at {order.Business.Name} was confirmed — show your QR code at pickup.", $"/orders/pickup/{order.Id}", "Order confirmed"),
            OrderStatuses.Completed => ($"Order #{order.OrderNumber:000} at {order.Business.Name} is complete — thanks for rescuing food!", "/orders", "Order complete"),
            OrderStatuses.Cancelled => ($"Order #{order.OrderNumber:000} at {order.Business.Name} was cancelled.", "/orders", "Order cancelled"),
            OrderStatuses.NoShow => ($"Order #{order.OrderNumber:000} at {order.Business.Name} was marked as a no-show — the pickup window closed without it being picked up.", "/orders", "Missed pickup"),
            _ => (null, null, null),
        };
        if (message is not null)
        {
            await notificationService.CreateAsync(order.UserId, message, url);
            await SendCustomerEmailAsync(order, emailSubject!, message);
        }

        return order;
    }

    // The one place "does this manager/admin own this order" is decided — shared by the
    // status-change path and the QR validation page's read, so the check can't drift out of sync.
    private async Task<Order> GetOwnedOrderAsync(Guid orderId)
    {
        var (isAdmin, business) = await ResolveManagedBusinessAsync();

        var order = await orderRepository.GetByIdAsync(orderId)
            ?? throw new InvalidOperationException("This order no longer exists.");

        if (!isAdmin && order.BusinessId != business!.Id)
            throw new UnauthorizedAccessException("You can only manage orders that belong to your business.");

        return order;
    }

    private async Task<(bool IsAdmin, Business? Business)> ResolveManagedBusinessAsync()
    {
        var (isAdmin, userId) = await currentUser.GetCurrentUserAsync();
        if (isAdmin)
            return (true, null);

        if (userId is null || !await currentUser.IsInRoleAsync(AppRoles.BusinessManager))
            throw new UnauthorizedAccessException("Only business managers can manage orders.");

        var business = await businessService.GetByManagerIdAsync(userId)
            ?? throw new UnauthorizedAccessException("You aren't assigned to manage a business yet.");

        return (false, business);
    }
}
