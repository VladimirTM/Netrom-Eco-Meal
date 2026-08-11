using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;
using Netrom_Eco_Meal.Repositories.Interfaces;

namespace Netrom_Eco_Meal.Repositories;

// AddAsync/RemoveByEndpointAsync persist immediately — same shape as FavoriteRepository, no
// staged-then-saved split.
public class PushSubscriptionRepository(EcoMealDbContext context) : IPushSubscriptionRepository
{
    public async Task<List<Entities.PushSubscription>> GetByUserIdAsync(string userId)
    {
        return await context.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync();
    }

    public async Task AddAsync(string userId, string endpoint, string p256dh, string auth)
    {
        var existing = await context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (existing is not null)
        {
            // Same endpoint re-subscribing under a different account (e.g. a shared browser) —
            // re-point it rather than fail the unique index on Endpoint.
            existing.UserId = userId;
            existing.P256Dh = p256dh;
            existing.Auth = auth;
            await context.SaveChangesAsync();
            return;
        }

        await context.PushSubscriptions.AddAsync(new Entities.PushSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Endpoint = endpoint,
            P256Dh = p256dh,
            Auth = auth,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    public async Task RemoveByEndpointAsync(string endpoint)
    {
        var existing = await context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (existing is null)
            return;

        context.PushSubscriptions.Remove(existing);
        await context.SaveChangesAsync();
    }
}
