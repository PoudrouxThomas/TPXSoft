using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using TPXSoft.Auth.Infrastructure.Persistence;

namespace TPXSoft.Auth.IntegrationTests.Fixtures;

/// <summary>
/// Shared setup for tests that drive the Api over real HTTP: a fresh, isolated database (created
/// from the shared <see cref="PostgresFixture"/> container) plus an <see cref="AuthWebApplicationFactory"/>
/// and <see cref="HttpClient"/> bound to it. Every concrete test class must still carry its own
/// [Collection] and [Trait("Category", "Integration")] -- attribute inheritance from an abstract
/// base isn't something to rely on for xUnit discovery/filtering.
/// </summary>
public abstract class AuthIntegrationTestBase : IAsyncLifetime
{
    private readonly PostgresFixture _postgresFixture;

    protected AuthIntegrationTestBase(PostgresFixture postgresFixture)
    {
        _postgresFixture = postgresFixture;
    }

    protected AuthWebApplicationFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    protected string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = await _postgresFixture.CreateDatabaseAsync($"tpxsoft_auth_{Guid.NewGuid():N}");
        Factory = new AuthWebApplicationFactory(ConnectionString);
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>A brand-new AuthDbContext over a brand-new connection -- not the same instance or
    /// tracked graph as anything the HTTP call under test used. Use this to confirm state was
    /// actually persisted, not just held in-memory by the request that changed it.</summary>
    protected AuthDbContext CreateFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AuthDbContext(options);
    }
}
