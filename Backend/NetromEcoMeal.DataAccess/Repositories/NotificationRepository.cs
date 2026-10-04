using Microsoft.EntityFrameworkCore;
using NetromEcoMeal.Database;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;

namespace NetromEcoMeal.Repositories;

public class NotificationRepository(EcoMealDbContext context) : INotificationRepository
{
    public async Task<List<Notification>> GetRecentByUserIdAsync(string userId, int take)
    {
        return await context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkAsReadAsync(Guid id, string userId)
    {
        await context.Notifications
            .Where(n => n.Id == id && n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
    }

    public async Task CreateAsync(Notification notification)
    {
        await context.Notifications.AddAsync(notification);
        await context.SaveChangesAsync();
    }
}
