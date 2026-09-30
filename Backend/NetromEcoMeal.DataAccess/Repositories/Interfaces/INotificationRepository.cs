using NetromEcoMeal.Entities;

namespace NetromEcoMeal.Repositories.Interfaces;

// Each method opens its own DbContext via IDbContextFactory instead of the circuit-scoped one.
public interface INotificationRepository
{
    public Task<List<Notification>> GetRecentByUserIdAsync(string userId, int take);
    public Task<int> GetUnreadCountAsync(string userId);
    public Task MarkAsReadAsync(Guid id, string userId);
    public Task MarkAllAsReadAsync(string userId);
    public Task CreateAsync(Notification notification);
}
