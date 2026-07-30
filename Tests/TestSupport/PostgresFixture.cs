using Npgsql;
using Testcontainers.PostgreSql;

namespace Netrom_Eco_Meal.Tests.TestSupport;

// One real Postgres server (via Testcontainers) shared across a test class. Individual tests get
// isolation by creating their own logical database on it (CreateDatabaseAsync) rather than paying
// for a fresh container per test — migrations + seeding are the thing under test, a fresh
// container per test would mostly just measure Docker startup time.
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("ecomeal_fixture")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<string> CreateDatabaseAsync()
    {
        var databaseName = $"test_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = databaseName };
        return builder.ConnectionString;
    }
}
