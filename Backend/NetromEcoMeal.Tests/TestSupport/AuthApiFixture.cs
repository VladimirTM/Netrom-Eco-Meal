namespace NetromEcoMeal.Tests.TestSupport;

// One dedicated Postgres container + Api host per test class that uses it (IClassFixture<T>), not
// shared with the broader PostgresCollection — these tests exercise rate limiting and JWT
// revocation, both of which are per-app-instance state that must start clean and not be perturbed
// by unrelated tests sharing the same container/app.
public class AuthApiFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    public ApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        var connectionString = await _postgres.CreateDatabaseAsync();
        Factory = new ApiFactory(connectionString);
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
