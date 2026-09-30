using Microsoft.EntityFrameworkCore;
using NetromEcoMeal.Database;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;

namespace NetromEcoMeal.Repositories;

// AddAsync only stages the change — call SaveChangesAsync to persist.
public class KitchenTipRepository(EcoMealDbContext context) : IKitchenTipRepository
{
    public async Task<List<KitchenTip>> GetVisibleByBusinessIdAsync(Guid businessId)
    {
        return await context.KitchenTips
            .Include(t => t.User)
            .Where(t => t.BusinessId == businessId && !t.IsHidden)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<KitchenTip?> GetByIdAsync(Guid id)
    {
        return await context.KitchenTips.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Dictionary<Guid, string>> GetSnippetsByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        var tips = await context.KitchenTips.Where(t => idList.Contains(t.Id)).ToListAsync();
        return tips.ToDictionary(t => t.Id, t => t.Tip.Length > 40 ? t.Tip[..40] + "…" : t.Tip);
    }

    public async Task AddAsync(KitchenTip tip)
    {
        await context.KitchenTips.AddAsync(tip);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
