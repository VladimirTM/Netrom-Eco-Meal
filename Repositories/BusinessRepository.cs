using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public class BusinessRepository(EcoMealDbContext context) : IBusinessRepository
{
     public async Task<List<Business>> GetAllAsync()
    {
        return await context.Businesses.Include(b => b.BusinessType).Include(b => b.Manager).ToListAsync();
    }

    public async Task<PaginatedList<Business>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId)
    {
        var query = context.Businesses.Include(b => b.BusinessType).Include(b => b.Manager).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b =>
                EF.Functions.ILike(b.Name, $"%{search}%") ||
                EF.Functions.ILike(b.Description, $"%{search}%") ||
                EF.Functions.ILike(b.Address, $"%{search}%"));

        if (businessTypeId.HasValue)
            query = query.Where(b => b.BusinessTypeId == businessTypeId);

        return await PaginatedList<Business>.CreateAsync(query.OrderBy(b => b.Name), pageIndex, pageSize);
    }

    public async Task<Business?> GetByIdAsync(Guid id)
    {
        return await context.Businesses.Include(b => b.Manager).Include(b => b.BusinessType).FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Business?> GetByManagerIdAsync(string managerId)
    {
        return await context.Businesses.Include(b => b.BusinessType).FirstOrDefaultAsync(b => b.ManagerId == managerId);
    }

    public async Task AddAsync(Business business)
    {
        await context.Businesses.AddAsync(business);
    }
    
    public async Task DeleteAsync(Guid id)
    {
        var business = await context.Businesses.FindAsync(id);
        if (business is null)
            return;
        context.Businesses.Remove(business);
    }
    
    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}