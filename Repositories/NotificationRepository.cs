using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// Uses IDbContextFactory instead of an injected EcoMealDbContext, so the polling bell's queries
// can't race the routed page's — every method is self-contained (fetch + mutate + save in one).
public class NotificationRepository(IDbContextFactory<EcoMealDbContext> contextFactory) : INotificationRepository
{
    public async Task<List<Notification>> GetRecentByUserIdAsync(string userId, int take)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkAsReadAsync(Guid id, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Notifications
            .Where(n => n.Id == id && n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
    }

    public async Task CreateAsync(Notification notification)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Notifications.AddAsync(notification);
        await context.SaveChangesAsync();
    }
}
