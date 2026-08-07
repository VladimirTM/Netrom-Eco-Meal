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
        return await context.Businesses.Include(b => b.BusinessType).Include(b => b.Staff).ThenInclude(s => s.User).ToListAsync();
    }

    public async Task<PaginatedList<Business>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessTypeId, string? staffUserId = null, string? sortBy = null, string? favoritedByUserId = null, double? customerLat = null, double? customerLng = null)
    {
        var query = context.Businesses.Include(b => b.BusinessType).Include(b => b.Staff).ThenInclude(s => s.User).AsQueryable();

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

        if (staffUserId is not null)
            query = query.Where(b => b.Staff.Any(s => s.UserId == staffUserId));

        if (favoritedByUserId is not null)
            query = query.Where(b => b.Favorites.Any(f => f.UserId == favoritedByUserId));

        // Haversine distance to a runtime point isn't something Npgsql translates to SQL, and the
        // dataset is small — materialize the filtered set and order/page it in memory instead.
        if (sortBy == BusinessSortOptions.Distance && customerLat.HasValue && customerLng.HasValue)
        {
            var all = await query.ToListAsync();
            var ordered = all
                .OrderBy(b => b.Latitude.HasValue && b.Longitude.HasValue
                    ? GeoDistance.Km(customerLat.Value, customerLng.Value, b.Latitude.Value, b.Longitude.Value)
                    : double.MaxValue)
                .ToList();

            return PaginatedList<Business>.Create(ordered, pageIndex, pageSize);
        }

        // Businesses with nothing live sort to the end regardless of sort mode.
        query = sortBy == BusinessSortOptions.ClosingSoon
            ? query.OrderBy(b => b.Packages.Where(p => p.PickupEnd > DateTime.UtcNow).Select(p => (DateTime?)p.PickupEnd).Min() ?? DateTime.MaxValue)
            : query.OrderBy(b => b.Name);

        return await PaginatedList<Business>.CreateAsync(query, pageIndex, pageSize);
    }

    public async Task<Business?> GetByIdAsync(Guid id)
    {
        return await context.Businesses.Include(b => b.Staff).ThenInclude(s => s.User).Include(b => b.BusinessType).FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<List<Business>> GetByStaffUserIdAsync(string userId)
    {
        return await context.Businesses.Include(b => b.BusinessType)
            .Where(b => b.Staff.Any(s => s.UserId == userId))
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<List<ApplicationUser>> GetStaffAsync(Guid businessId)
    {
        return await context.BusinessStaff
            .Where(s => s.BusinessId == businessId)
            .OrderBy(s => s.User.Name)
            .Select(s => s.User)
            .ToListAsync();
    }

    public async Task<bool> IsStaffAsync(Guid businessId, string userId)
    {
        return await context.BusinessStaff.AnyAsync(s => s.BusinessId == businessId && s.UserId == userId);
    }

    public async Task<bool> AddStaffAsync(Guid businessId, string userId)
    {
        if (await IsStaffAsync(businessId, userId))
            return false;

        context.BusinessStaff.Add(new BusinessStaff
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            UserId = userId,
            AssignedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveStaffAsync(Guid businessId, string userId)
    {
        var staff = await context.BusinessStaff.FirstOrDefaultAsync(s => s.BusinessId == businessId && s.UserId == userId);
        if (staff is null)
            return false;

        context.BusinessStaff.Remove(staff);
        await context.SaveChangesAsync();
        return true;
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