using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TPXSoft.Auth.Api.Contracts;
using TPXSoft.Auth.Domain.Common;
using TPXSoft.Auth.IntegrationTests.Fixtures;
using TPXSoft.Auth.IntegrationTests.Support;

namespace TPXSoft.Auth.IntegrationTests.Endpoints;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AuthEndpointsFlowTests : AuthIntegrationTestBase
{
    public AuthEndpointsFlowTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    [Fact]
    public async Task Register_Login_Me_RoundTrip_Succeeds()
    {
        const string email = "roundtrip@example.com";

        var registerResponse = await Client.PostAsJsonAsync("/auth/register",
            new { email, password = "correct-horse-battery", orgName = "Acme Inc" });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await Client.PostAsJsonAsync("/auth/login", new { email, password = "correct-horse-battery" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenPair>(ApiJsonOptions.Instance);
        Assert.NotNull(tokens);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await Client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var user = await meResponse.Content.ReadFromJsonAsync<UserResponse>(ApiJsonOptions.Instance);
        Assert.NotNull(user);
        Assert.Equal(email, user!.Email);
        Assert.Equal(Role.Admin, user.Role);
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.NotEqual(Guid.Empty, user.OrgId);
        Assert.Equal("Acme Inc", user.OrgName);
    }

    [Fact]
    public async Task Register_ResponseBody_DeserializesToNonEmptyAccessAndRefreshTokens()
    {
        var response = await Client.PostAsJsonAsync("/auth/register",
            new { email = "shape-check@example.com", password = "correct-horse-battery", orgName = "Acme Inc" });

        var tokens = await response.Content.ReadFromJsonAsync<TokenPair>(ApiJsonOptions.Instance);

        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrEmpty(tokens!.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
    }

    [Fact]
    public async Task Register_AlreadyRegisteredEmail_Returns409WithAnErrorBody()
    {
        const string email = "duplicate@example.com";
        var first = await Client.PostAsJsonAsync("/auth/register",
            new { email, password = "correct-horse-battery", orgName = "Acme Inc" });
        first.EnsureSuccessStatusCode();

        var second = await Client.PostAsJsonAsync("/auth/register",
            new { email, password = "another-password", orgName = "Other Inc" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ErrorResponse>(ApiJsonOptions.Instance);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Message));
    }
}
