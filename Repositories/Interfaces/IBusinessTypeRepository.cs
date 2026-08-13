using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync/DeleteAsync only stage changes — call SaveChangesAsync to persist.
public interface IBusinessTypeRepository
{
    public Task<List<BusinessType>> GetAllAsync();
    public Task<BusinessType?> GetByIdAsync(Guid id);
    // Whether any Business still references this type — the FK is a required relationship with
    // no explicit OnDelete configured, so EF Core's convention default is Cascade; deleting a
    // still-referenced type would silently take every business of that type down with it.
    public Task<bool> IsInUseAsync(Guid id);
    public Task AddAsync(BusinessType businessType);
    public Task DeleteAsync(BusinessType businessType);
    public Task SaveChangesAsync();
}
