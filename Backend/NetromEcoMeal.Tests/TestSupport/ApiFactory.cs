using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace NetromEcoMeal.Tests.TestSupport;

// Boots the real Api host (Program.cs — migrations, seeding, DI, JWT auth, rate limiting, the lot)
// against an isolated Postgres database from PostgresFixture. One instance per test class, built
// from a fresh logical database so tests in different classes never see each other's seeded rows
// or JWTs (a shared Jwt:Key is still fine across classes — only the DB differs).
public class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "test-only-ecomeal-jwt-signing-key-at-least-32-characters-long";
    public const string TestJwtIssuer = "NetromEcoMealTests";
    public const string TestJwtAudience = "NetromEcoMealTests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // UseSetting (not ConfigureAppConfiguration) — Program.cs reads builder.Configuration for
        // the connection string and Jwt:Key before WebApplicationFactory's deferred host gets a
        // chance to layer an extra ConfigureAppConfiguration source on top, so that override never
        // took effect. UseSetting writes straight into the host's own in-memory settings, which
        // WebApplicationBuilder always includes as a configuration source from the start.
        foreach (var (key, value) in new Dictionary<string, string?>
                 {
                     ["ConnectionStrings:EcoMealContext"] = connectionString,
                     ["Jwt:Key"] = TestJwtKey,
                     ["Jwt:Issuer"] = TestJwtIssuer,
                     ["Jwt:Audience"] = TestJwtAudience,
                     ["Jwt:ExpiresInDays"] = "7",
                     // Hosted sweep jobs would otherwise race the tests' own data against a real Postgres.
                     ["BackgroundJobs:Enabled"] = "false",
                     ["Identity:RequireConfirmedAccount"] = "false",
                     ["SeedAdmin:Email"] = "admin@ecomeal.local",
                     ["SeedAdmin:Password"] = "Admin123!",
                     ["App:BaseUrl"] = "http://localhost",
                     ["Stripe:SecretKey"] = "",
                     ["Ollama:BaseUrl"] = "",
                 })
            builder.UseSetting(key, value);
    }
}
