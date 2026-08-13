using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

// Reads are open to anyone (every business-browsing page needs the list); writes are admin-only,
// enforced in the implementation via CurrentUserAccessor.
public interface IBusinessTypeService
{
    public Task<List<BusinessType>> GetAllAsync();
    public Task AddAsync(BusinessType businessType);
    public Task UpdateAsync(BusinessType businessType);
    // Throws InvalidOperationException if a Business still references this type.
    public Task DeleteAsync(Guid id);
}
