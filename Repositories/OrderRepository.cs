using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
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

    public async Task<Order?> GetByIdAsync(Guid id)
    {
        return await OrdersWithIncludes().FirstOrDefaultAsync(o => o.Id == id);
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
