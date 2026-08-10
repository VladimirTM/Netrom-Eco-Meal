using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
            // The InMemory provider doesn't actually enforce transactional atomicity, and by
            // default treats BeginTransactionAsync as an error rather than a silent no-op —
            // ReportService.TakeActionAsync (and anything else that wraps writes in a real
            // transaction against Postgres) would otherwise fail here even though the
            // underlying writes still happen and are what these tests assert on.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new EcoMealDbContext(options);
        context.Statuses.AddRange(TestStatusIds.All());
        context.SaveChanges();
        return context;
    }
}
