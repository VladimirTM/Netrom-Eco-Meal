using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync only stages the change — call SaveChangesAsync to persist.
public interface IKitchenTipRepository
{
    // Newest first, hidden ones excluded — the only read pattern a business page needs.
    public Task<List<KitchenTip>> GetVisibleByBusinessIdAsync(Guid businessId);
    public Task<KitchenTip?> GetByIdAsync(Guid id);
    public Task<Dictionary<Guid, string>> GetSnippetsByIdsAsync(IEnumerable<Guid> ids);
    public Task AddAsync(KitchenTip tip);
    public Task SaveChangesAsync();
}
