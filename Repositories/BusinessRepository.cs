using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
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

    public async Task<PaginatedList<Business>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId, string? managerId = null, string? sortBy = null)
    {
        var query = context.Businesses.Include(b => b.BusinessType).Include(b => b.Manager).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b =>
                EF.Functions.ILike(b.Name, $"%{search}%") ||
                EF.Functions.ILike(b.Description, $"%{search}%") ||
                EF.Functions.ILike(b.Address, $"%{search}%") ||
                // Also surfaces a kitchen by what's actually in its live packages (e.g. "bread").
                b.Packages.Any(p => p.PickupEnd > DateTime.UtcNow &&
                    (EF.Functions.ILike(p.Name, $"%{search}%") || EF.Functions.ILike(p.Description, $"%{search}%"))));

        if (businessTypeId.HasValue)
            query = query.Where(b => b.BusinessTypeId == businessTypeId);

        if (managerId is not null)
            query = query.Where(b => b.ManagerId == managerId);

        // Businesses with nothing live sort to the end regardless of sort mode.
        query = sortBy == BusinessSortOptions.ClosingSoon
            ? query.OrderBy(b => b.Packages.Where(p => p.PickupEnd > DateTime.UtcNow).Select(p => (DateTime?)p.PickupEnd).Min() ?? DateTime.MaxValue)
            : query.OrderBy(b => b.Name);

        return await PaginatedList<Business>.CreateAsync(query, pageIndex, pageSize);
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