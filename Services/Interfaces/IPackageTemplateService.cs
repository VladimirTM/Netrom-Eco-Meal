using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Write methods are restricted to admins and the template's own business manager.
public interface IPackageTemplateService
{
    public Task<List<PackageTemplate>> GetAllAsync();
    public Task<List<PackageTemplate>> GetByBusinessIdAsync(Guid businessId);
    public Task<PackageTemplate> CreateFromPackageAsync(Guid packageId, TimeSpan pickupStartTimeUtc, TimeSpan pickupEndTimeUtc);
    public Task SetActiveAsync(Guid id, bool isActive);
    public Task DeleteAsync(Guid id);

    // No current-user auth (background-sweep call) — generates today's Package instance for every
    // active template that hasn't already been generated today.
    public Task<int> GenerateDueInstancesAsync();
}
