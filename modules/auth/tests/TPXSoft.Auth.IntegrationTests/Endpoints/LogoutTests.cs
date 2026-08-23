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
public sealed class LogoutTests : AuthIntegrationTestBase
{
    public LogoutTests(PostgresFixture postgresFixture) : base(postgresFixture)
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
    public async Task Logout_RevokesTheToken_PersistsIt_AndBlocksASubsequentRefresh()
    {
        var tokens = await RegisterAsync("logout-persist@example.com");

        var logoutResponse = await Client.PostAsJsonAsync("/auth/logout", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var refreshTokenFactory = scope.ServiceProvider.GetRequiredService<IRefreshTokenFactory>();
        var tokenHash = refreshTokenFactory.HashToken(tokens.RefreshToken);

        await using var freshDbContext = CreateFreshDbContext();
        var storedToken = await freshDbContext.RefreshTokens.AsNoTracking().SingleAsync(rt => rt.TokenHash == tokenHash);
        Assert.NotNull(storedToken.RevokedAt);

        var refreshAttempt = await Client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAttempt.StatusCode);
    }

    [Fact]
    public async Task Logout_WithACompletelyUnknownToken_Returns204_BecauseTheContractMandatesIdempotence()
    {
        var response = await Client.PostAsJsonAsync("/auth/logout", new { refreshToken = "a-token-that-was-never-issued" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
