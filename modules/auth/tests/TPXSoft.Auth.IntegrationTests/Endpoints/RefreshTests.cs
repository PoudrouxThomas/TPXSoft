using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TPXSoft.Auth.Api.Contracts;
using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.IntegrationTests.Fixtures;
using TPXSoft.Auth.IntegrationTests.Support;

namespace TPXSoft.Auth.IntegrationTests.Endpoints;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RefreshTests : AuthIntegrationTestBase
{
    public RefreshTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    private async Task<TokenPair> RegisterAsync(string email)
    {
        var response = await Client.PostAsJsonAsync("/auth/register",
            new { email, password = "correct-horse-battery", orgName = "Acme Inc" });
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<TokenPair>(ApiJsonOptions.Instance);
        return tokens!;
    }

    [Fact]
    public async Task Refresh_RotatesTokens_AndPersistsRevocationOfTheOldOne()
    {
        var originalTokens = await RegisterAsync("refresh-rotate@example.com");

        var refreshResponse = await Client.PostAsJsonAsync("/auth/refresh", new { refreshToken = originalTokens.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<TokenPair>(ApiJsonOptions.Instance);
        Assert.NotNull(newTokens);
        Assert.NotEqual(originalTokens.RefreshToken, newTokens!.RefreshToken);

        // Re-read from a brand-new DbContext/connection -- catches "revocation looked right
        // in-memory but was never actually persisted" bugs that a same-context assertion can't.
        using var scope = Factory.Services.CreateScope();
        var refreshTokenFactory = scope.ServiceProvider.GetRequiredService<IRefreshTokenFactory>();
        var oldTokenHash = refreshTokenFactory.HashToken(originalTokens.RefreshToken);

        await using var freshDbContext = CreateFreshDbContext();
        var storedOldToken = await freshDbContext.RefreshTokens.AsNoTracking()
            .SingleAsync(rt => rt.TokenHash == oldTokenHash);

        Assert.NotNull(storedOldToken.RevokedAt);
    }

    [Fact]
    public async Task Refresh_ReusingARotatedAwayToken_Returns401()
    {
        var originalTokens = await RegisterAsync("refresh-reuse@example.com");
        var firstRefresh = await Client.PostAsJsonAsync("/auth/refresh", new { refreshToken = originalTokens.RefreshToken });
        firstRefresh.EnsureSuccessStatusCode();

        var secondRefresh = await Client.PostAsJsonAsync("/auth/refresh", new { refreshToken = originalTokens.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);
    }
}
