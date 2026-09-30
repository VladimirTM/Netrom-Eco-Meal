using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NetromEcoMeal.Database;

// Lets `dotnet ef` (migrations add/list/has-pending-model-changes) construct EcoMealDbContext
// outside the Web host's DI container, which normally supplies the connection string via
// appsettings/user-secrets. Only used at design time — never touched at app runtime.
public class EcoMealDbContextFactory : IDesignTimeDbContextFactory<EcoMealDbContext>
{
    public EcoMealDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__EcoMealContext")
            ?? "Host=localhost;Port=5432;Database=EcoMeal;Username=postgres;Password=postgres;Pooling=true;";

        var options = new DbContextOptionsBuilder<EcoMealDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new EcoMealDbContext(options);
    }
}
