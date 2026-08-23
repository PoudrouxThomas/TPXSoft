using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TPXSoft.Auth.Api.Contracts;
using TPXSoft.Auth.IntegrationTests.Fixtures;
using TPXSoft.Auth.IntegrationTests.Support;

namespace TPXSoft.Auth.IntegrationTests.Endpoints;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MeAuthorizationTests : AuthIntegrationTestBase
{
    public MeAuthorizationTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    [Fact]
    public async Task Me_WithNoAuthorizationHeader_Returns401WithAnErrorBody()
    {
        var response = await Client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(ApiJsonOptions.Instance);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Message));
    }

    [Fact]
    public async Task Me_WithAGarbageOrTamperedBearerToken_Returns401WithTheSameErrorBodyShape()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "this-is-not-a-real-jwt.tampered.garbage");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(ApiJsonOptions.Instance);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Message));
    }
}
