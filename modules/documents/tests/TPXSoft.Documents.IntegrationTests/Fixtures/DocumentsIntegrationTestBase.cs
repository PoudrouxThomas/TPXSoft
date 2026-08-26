using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using TPXSoft.Documents.Infrastructure.Persistence;

namespace TPXSoft.Documents.IntegrationTests.Fixtures;

/// <summary>
/// Shared setup for tests that drive the Api over real HTTP: a fresh, isolated database (created
/// from the shared <see cref="PostgresFixture"/> container) plus a <see cref="DocumentsWebApplicationFactory"/>
/// and <see cref="HttpClient"/> bound to it. Every concrete test class must still carry its own
/// [Collection] and [Trait("Category", "Integration")] -- attribute inheritance isn't relied on
/// for xUnit discovery/filtering. Mirrors TPXSoft.Auth.IntegrationTests.Fixtures.AuthIntegrationTestBase.
/// </summary>
public abstract class DocumentsIntegrationTestBase : IAsyncLifetime
{
    private readonly PostgresFixture _postgresFixture;

    protected DocumentsIntegrationTestBase(PostgresFixture postgresFixture)
    {
        _postgresFixture = postgresFixture;
    }

    protected DocumentsWebApplicationFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    protected string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = await _postgresFixture.CreateDatabaseAsync($"tpxsoft_documents_{Guid.NewGuid():N}");
        Factory = new DocumentsWebApplicationFactory(ConnectionString);
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>A fresh HttpClient authenticated as the given user -- callers that need more than
    /// one caller identity in the same test (owner vs. another user) create one per identity.</summary>
    protected HttpClient CreateAuthenticatedClient(Guid userId, Guid? orgId = null)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.IssueFor(userId, orgId));
        return client;
    }

    /// <summary>A brand-new DocumentsDbContext over a brand-new connection -- not the same
    /// instance or tracked graph as anything the HTTP call under test used. Use this to confirm
    /// state was actually persisted, not just held in-memory by the request that changed it.</summary>
    protected DocumentsDbContext CreateFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new DocumentsDbContext(options);
    }
}
