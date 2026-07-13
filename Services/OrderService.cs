using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
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
            // OrderNumber is assigned by the DB's order_numbers sequence on insert (see EcoMealDbContext),
            // so concurrent checkouts can't race each other into the same number.
        };

        var packageIds = lines.Select(l => l.PackageId).Distinct().ToList();
        var packages = (await packageRepository.GetByIdsAsync(packageIds)).ToDictionary(p => p.Id);

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Quantity must be greater than zero.");

            if (!packages.TryGetValue(line.PackageId, out var package) || package.BusinessId != businessId)
                throw new InvalidOperationException("One of the packages in your cart is no longer available.");

            if (line.Quantity > package.Quantity)
                throw new InvalidOperationException($"Only {package.Quantity} left of \"{package.Name}\" — please adjust your cart.");

            // Stock isn't reserved until the business confirms the order — see UpdateStatusAsync.
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

    public async Task<Order> UpdateStatusAsync(Guid orderId, string statusName)
    {
        var (isAdmin, business) = await ResolveManagedBusinessAsync();

        var order = await orderRepository.GetByIdAsync(orderId)
            ?? throw new InvalidOperationException("This order no longer exists.");

        if (!isAdmin && order.BusinessId != business!.Id)
            throw new UnauthorizedAccessException("You can only manage orders that belong to your business.");

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
            // Another confirmation/cancellation changed one of these packages' stock in the
            // meantime (Package uses xmin as a concurrency token) — don't silently oversell.
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
