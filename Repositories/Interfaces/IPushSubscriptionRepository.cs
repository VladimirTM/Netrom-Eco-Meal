namespace Netrom_Eco_Meal.Repositories.Interfaces;

// AddAsync/RemoveByEndpointAsync persist immediately — same shape as IFavoriteRepository, no staged-save split.
public interface IPushSubscriptionRepository
{
    public Task<List<Entities.PushSubscription>> GetByUserIdAsync(string userId);
    public Task AddAsync(string userId, string endpoint, string p256dh, string auth);
    // Also used to prune a subscription the push service reports as gone (404/410).
    public Task RemoveByEndpointAsync(string endpoint);
}
