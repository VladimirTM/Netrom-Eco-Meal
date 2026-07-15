using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public class OrderRepository(EcoMealDbContext context) : IOrderRepository
{
    public async Task<List<Order>> GetAllAsync()
    {
        return await OrdersWithIncludes().OrderByDescending(o => o.OrderNumber).ToListAsync();
    }

    public async Task<List<Order>> GetByUserIdAsync(string userId)
    {
        return await OrdersWithIncludes().Where(o => o.UserId == userId).OrderByDescending(o => o.OrderNumber).ToListAsync();
    }

    public async Task<List<Order>> GetByBusinessIdAsync(Guid businessId)
    {
        return await OrdersWithIncludes().Where(o => o.BusinessId == businessId).OrderByDescending(o => o.OrderNumber).ToListAsync();
    }

    public async Task<PaginatedList<Order>> GetPagedByUserIdAsync(string userId, int pageIndex, int pageSize, string? status)
    {
        var query = OrdersWithIncludes().Where(o => o.UserId == userId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.Status.Name == status);

        return await PaginatedList<Order>.CreateAsync(query.OrderByDescending(o => o.OrderNumber), pageIndex, pageSize);
    }

    public async Task<PaginatedList<Order>> GetPagedForManagementAsync(int pageIndex, int pageSize, string? search, Guid? businessId, string? status)
    {
        var query = OrdersWithIncludes().AsQueryable();

        if (businessId.HasValue)
            query = query.Where(o => o.BusinessId == businessId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.Status.Name == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Substring-match the plain order number — Npgsql can't translate the zero-padded
            // ToString("000") overload — and strip a leading "#" so "#011" still matches.
            var numberSearch = search.TrimStart('#');
            query = query.Where(o =>
                EF.Functions.ILike(o.OrderNumber.ToString(), $"%{numberSearch}%") ||
                EF.Functions.ILike(o.User.Name, $"%{search}%"));
        }

        return await PaginatedList<Order>.CreateAsync(query.OrderByDescending(o => o.OrderNumber), pageIndex, pageSize);
    }

    public async Task<Order?> GetByIdAsync(Guid id)
    {
        return await OrdersWithIncludes().FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<bool> HasCompletedOrderAsync(string userId, Guid businessId)
    {
        return await context.Orders.AnyAsync(o =>
            o.UserId == userId && o.BusinessId == businessId && o.Status.Name == OrderStatuses.Completed);
    }

    public async Task AddAsync(Order order)
    {
        await context.Orders.AddAsync(order);
    }

    public async Task DeleteAsync(Guid id)
    {
        var order = await context.Orders.FindAsync(id);
        if (order is null)
            return;
        context.Orders.Remove(order);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }

    private IQueryable<Order> OrdersWithIncludes() =>
        context.Orders
            .Include(o => o.User)
            .Include(o => o.Business)
            .Include(o => o.Status)
            .Include(o => o.OrderPackages)
            .ThenInclude(op => op.Package);
}
