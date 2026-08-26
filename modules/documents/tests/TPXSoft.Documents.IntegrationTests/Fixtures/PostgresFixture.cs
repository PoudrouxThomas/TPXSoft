using Npgsql;
using Testcontainers.PostgreSql;

namespace TPXSoft.Documents.IntegrationTests.Fixtures;

/// <summary>
/// Starts a single postgres:16 container for the whole assembly (see <see cref="PostgresCollection"/>).
/// Individual test classes don't share a database -- <see cref="CreateDatabaseAsync"/> provisions
/// an isolated, empty logical database per test class inside this one server. Mirrors
/// TPXSoft.Auth.IntegrationTests.Fixtures.PostgresFixture exactly.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("tpxsoft_documents_root")
        .WithUsername("tpxsoft")
        .WithPassword("tpxsoft")
        .Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Creates a fresh, empty database on the shared server and returns a connection
    /// string pointing at it.</summary>
    public async Task<string> CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = databaseName
        };
        return builder.ConnectionString;
    }
}
