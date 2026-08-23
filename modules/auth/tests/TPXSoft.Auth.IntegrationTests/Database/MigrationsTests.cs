using Microsoft.EntityFrameworkCore;
using TPXSoft.Auth.Infrastructure.Persistence;
using TPXSoft.Auth.IntegrationTests.Fixtures;

namespace TPXSoft.Auth.IntegrationTests.Database;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MigrationsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgresFixture;
    private string _connectionString = string.Empty;

    public MigrationsTests(PostgresFixture postgresFixture)
    {
        _postgresFixture = postgresFixture;
    }

    public async Task InitializeAsync() =>
        _connectionString = await _postgresFixture.CreateDatabaseAsync($"tpxsoft_auth_migrations_{Guid.NewGuid():N}");

    public Task DisposeAsync() => Task.CompletedTask;

    private AuthDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(_connectionString).Options);

    [Fact]
    public async Task MigrateAsync_OnAnEmptyDatabase_CreatesExpectedTablesAndTheHistoryTable()
    {
        await using var dbContext = CreateDbContext();

        await dbContext.Database.MigrateAsync();

        var tableNames = await GetTableNamesAsync(dbContext);
        Assert.Contains("orgs", tableNames);
        Assert.Contains("users", tableNames);
        Assert.Contains("refresh_tokens", tableNames);
        Assert.Contains("__EFMigrationsHistory", tableNames);
    }

    [Fact]
    public async Task MigrateAsync_RunASecondTime_IsANoOp()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        var historyRowCountAfterFirstRun = await GetMigrationsHistoryRowCountAsync(dbContext);

        await dbContext.Database.MigrateAsync();

        var historyRowCountAfterSecondRun = await GetMigrationsHistoryRowCountAsync(dbContext);
        Assert.Equal(1, historyRowCountAfterFirstRun);
        Assert.Equal(historyRowCountAfterFirstRun, historyRowCountAfterSecondRun);
    }

    private static async Task<List<string>> GetTableNamesAsync(AuthDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'";
            await using var reader = await command.ExecuteReaderAsync();

            var names = new List<string>();
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }
            return names;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<int> GetMigrationsHistoryRowCountAsync(AuthDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\"";
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
