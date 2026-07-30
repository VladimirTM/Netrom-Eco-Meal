using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Database;

namespace Netrom_Eco_Meal.Tests.TestSupport;

// A fresh EF Core InMemory-backed EcoMealDbContext per call, pre-seeded with the four fixed
// order statuses OrderService looks up by name. Good enough for exercising OrderService's own
// LINQ queries (rate-limit counts, pending-reservation sums, status lookups) — the Postgres-only
// behavior (EF.Functions.ILike, xmin concurrency, the order_numbers sequence) is covered
// separately by the Testcontainers-backed seeding integration tests, not here.
public static class InMemoryDb
{
    public static EcoMealDbContext Create()
    {
        var options = new DbContextOptionsBuilder<EcoMealDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new EcoMealDbContext(options);
        context.Statuses.AddRange(TestStatusIds.All());
        context.SaveChanges();
        return context;
    }
}
