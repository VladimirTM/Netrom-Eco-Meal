using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories;

public class OrderRepository(EcoMealDbContext context)
{
    public async Task<List<Order>> GetAllAsync()
    {
        return await context.Orders.ToListAsync();
    }
    
    public async Task<Order?> GetByIdAsync(Guid id)
    {
        return await context.Orders.FirstOrDefaultAsync(o => o.Id == id);
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
}