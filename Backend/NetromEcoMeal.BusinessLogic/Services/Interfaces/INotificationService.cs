using NetromEcoMeal.Entities;

namespace NetromEcoMeal.Services.Interfaces;

// Scoped to the signed-in user, except CreateAsync — used by other services to notify anyone.
public interface INotificationService
{
    public Task<List<Notification>> GetMyNotificationsAsync(int take = 20);
    public Task<int> GetMyUnreadCountAsync();
    public Task MarkAsReadAsync(Guid id);
    public Task MarkAllAsReadAsync();
    public Task CreateAsync(string userId, string message, string? url = null);
}
