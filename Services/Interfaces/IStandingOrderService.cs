using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Customer-only CRUD, always scoped to the signed-in user — no "standing order on behalf of"
// path exists, same shape as IFavoriteService.
public interface IStandingOrderService
{
    public Task<List<StandingOrder>> GetMyStandingOrdersAsync();
    public Task<StandingOrder> CreateAsync(Guid businessId, Guid? packageTypeId, string? dietaryTag, decimal maxWeeklySpend);
    public Task UpdateAsync(Guid id, decimal maxWeeklySpend, bool isActive);
    public Task DeleteAsync(Guid id);

    // No current-user check (system-triggered, called by PackageTemplateGenerationService right
    // after generating today's instances) — matches each freshly generated package against every
    // active standing order at its business and notifies (never charges) a match still under budget.
    public Task<int> MatchNewPackagesAsync(IEnumerable<Package> generatedPackages);
}
