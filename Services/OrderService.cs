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

        var pendingStatus = await dbContext.Statuses.FirstOrDefaultAsync(s => s.Name == OrderStatuses.Pending)
            ?? throw new InvalidOperationException("Order status configuration is missing.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = user,
            BusinessId = businessId,
            StatusId = pendingStatus.Id,
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

    public async Task<Order> UpdateStatusAsync(Guid orderId, string statusName)
    {
        var (isAdmin, business) = await ResolveManagedBusinessAsync();

        var order = await orderRepository.GetByIdAsync(orderId)
            ?? throw new InvalidOperationException("This order no longer exists.");

        if (!isAdmin && order.BusinessId != business!.Id)
            throw new UnauthorizedAccessException("You can only manage orders that belong to your business.");

        return await ApplyStatusChangeAsync(order, statusName);
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
        else if (statusName == OrderStatuses.Cancelled && currentStatusName == OrderStatuses.Confirmed)
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
