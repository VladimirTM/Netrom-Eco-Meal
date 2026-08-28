using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public class PackageRepository(EcoMealDbContext context) : IPackageRepository
{
    // AsNoTracking on both — pure read/display paths, writes re-fetch via GetByIdAsync/GetByIdsAsync.
    // Matters for correctness, not just perf: EcoMealDbContext lives for a whole Blazor circuit, so a
    // tracked re-query returns the same stale instances via EF's identity map — PackageStockBroadcaster's
    // live reload (BusinessDetail.razor) depends on this returning fresh Quantity every time.
    public async Task<List<Package>> GetAllAsync()
    {
        return await context.Packages.AsNoTracking().Include(p => p.PackageType).Include(p => p.Business).ToListAsync();
    }

    public async Task<PaginatedList<Package>> GetPagedAsync(int pageIndex, int pageSize, string? search, Guid? businessId, Guid? packageTypeId)
    {
        var query = context.Packages.AsNoTracking().Include(p => p.PackageType).Include(p => p.Business).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, $"%{search}%") ||
                EF.Functions.ILike(p.Description, $"%{search}%"));

        if (businessId.HasValue)
            query = query.Where(p => p.BusinessId == businessId);

        if (packageTypeId.HasValue)
            query = query.Where(p => p.PackageTypeId == packageTypeId);

        return await PaginatedList<Package>.CreateAsync(query.OrderBy(p => p.Name), pageIndex, pageSize);
    }

    public async Task<Package?> GetByIdAsync(Guid id)
    {
        return await context.Packages.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Package>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        return await context.Packages
            .Include(p => p.PackageType)
            .Include(p => p.Business)
            .Where(p => idList.Contains(p.Id))
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, string>> GetNamesByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        return await context.Packages.Where(p => idList.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
    }

    public async Task<List<Package>> GetForAnalyticsAsync(Guid? businessId, DateTime since)
    {
        var query = context.Packages
            .Include(p => p.OrderPackages)
                .ThenInclude(op => op.Order)
                    .ThenInclude(o => o.Status)
            .Where(p => p.PickupStart >= since)
            .AsQueryable();

        if (businessId.HasValue)
            query = query.Where(p => p.BusinessId == businessId);

        return await query.ToListAsync();
    }

    public async Task<List<Package>> GetNearExpiryUnclaimedAsync(DateTime now, DateTime closingBefore)
    {
        return await context.Packages
            .Include(p => p.Business)
            .Where(p => !p.IsHidden && p.Quantity > 0 && p.NearExpiryNudgeSentAt == null
                && p.PickupEnd > now && p.PickupEnd <= closingBefore)
            .ToListAsync();
    }

    public async Task<List<Package>> GetLiveCandidatesAsync(string? dietaryTag)
    {
        var now = DateTime.UtcNow;
        var query = context.Packages.AsNoTracking().Include(p => p.PackageType).Include(p => p.Business)
            .Where(p => !p.IsHidden && p.Quantity > 0 && p.PickupEnd > now);

        if (!string.IsNullOrWhiteSpace(dietaryTag))
            query = query.Where(p => p.DietaryTags.Contains(dietaryTag));

        return await query.OrderBy(p => p.Price).ToListAsync();
    }

    public async Task<List<Package>> GetMarkdownCandidatesAsync(Guid? businessId, DateTime now, DateTime closingBefore)
    {
        var query = context.Packages.AsNoTracking().Include(p => p.PackageType).Include(p => p.Business)
            .Where(p => !p.IsHidden && p.Quantity > 0 && p.MarkdownDismissedAt == null
                && p.PickupEnd > now && p.PickupEnd <= closingBefore);

        if (businessId.HasValue)
            query = query.Where(p => p.BusinessId == businessId);

        return await query.OrderBy(p => p.PickupEnd).ToListAsync();
    }

    public async Task<List<Package>> GetSellThroughHistoryAsync(Guid businessId, Guid excludePackageId, DateTime since)
    {
        var now = DateTime.UtcNow;
        return await context.Packages.AsNoTracking()
            .Include(p => p.PackageType)
            .Include(p => p.OrderPackages)
                .ThenInclude(op => op.Order)
                    .ThenInclude(o => o.Status)
            .Where(p => p.BusinessId == businessId && p.Id != excludePackageId && p.PickupEnd < now && p.PickupEnd >= since)
            .OrderByDescending(p => p.PickupEnd)
            .Take(MarkdownSettings.MaxHistoryRecords)
            .ToListAsync();
    }

    public async Task AddAsync(Package package)
    {
        await context.Packages.AddAsync(package);
    }
    
    public async Task DeleteAsync(Guid id)
    {
        var package = await context.Packages.FindAsync(id);
        if(package is null)
            return;
        context.Packages.Remove(package);
    }
    
    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}