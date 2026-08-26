using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Domain.Entities;
using TPXSoft.Documents.IntegrationTests.Fixtures;

namespace TPXSoft.Documents.IntegrationTests.Endpoints;

/// <summary>Drives the real GET /public/documents/{token}/content endpoint over HTTP against a
/// real Postgres database -- the one anonymous route in the module. Test list per
/// documentation/05-preview-and-download.md's "Tests -> Integration" section (the public-route
/// bullets).</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PublicDocumentContentEndpointTests : DocumentsIntegrationTestBase
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly byte[] Bytes = "hello world"u8.ToArray();

    public PublicDocumentContentEndpointTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    [Fact]
    public async Task Download_ValidToken_NoAuthorizationHeader_Returns200_WithBytes()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var ownerClient = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, Bytes, contentType: "application/pdf");
        var token = await SetPublicLinkAsync(ownerClient, document.Id);

        using var anonymousClient = Factory.CreateClient();
        using var response = await anonymousClient.GetAsync($"/public/documents/{token}/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("attachment", response.Content.Headers.ContentDisposition!.ToString());
    }

    [Fact]
    public async Task Download_AfterOwnerSwitchesToPrivate_Returns404()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var ownerClient = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, Bytes);
        var token = await SetPublicLinkAsync(ownerClient, document.Id);
        var privateResponse = await ownerClient.PutAsJsonAsync($"/documents/{document.Id}/visibility", new { visibility = "Private" });
        Assert.Equal(HttpStatusCode.OK, privateResponse.StatusCode);

        using var anonymousClient = Factory.CreateClient();
        using var response = await anonymousClient.GetAsync($"/public/documents/{token}/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("No document with this token.", body!.Message);
    }

    [Fact]
    public async Task Download_AfterTokenRotation_OldTokenReturns404()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var ownerClient = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, Bytes);
        var oldToken = await SetPublicLinkAsync(ownerClient, document.Id);
        var newToken = await SetPublicLinkAsync(ownerClient, document.Id);
        Assert.NotEqual(oldToken, newToken);

        using var anonymousClient = Factory.CreateClient();
        using var response = await anonymousClient.GetAsync($"/public/documents/{oldToken}/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_AfterDocumentDeleted_Returns404()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var ownerClient = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, Bytes);
        var token = await SetPublicLinkAsync(ownerClient, document.Id);
        var deleteResponse = await ownerClient.DeleteAsync($"/documents/{document.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var anonymousClient = Factory.CreateClient();
        using var response = await anonymousClient.GetAsync($"/public/documents/{token}/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_GarbageToken_Returns404_SameBodyAsAnyOtherFailure()
    {
        using var anonymousClient = Factory.CreateClient();

        using var response = await anonymousClient.GetAsync("/public/documents/not-a-real-token/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("No document with this token.", body!.Message);
    }

    [Fact]
    public async Task Download_GarbageToken_IsIndistinguishableFrom_ValidTokenOnPrivateDocument()
    {
        // documentation 05's public-route rule 2/3: the token column should never be non-null on a
        // non-PublicLink document through the normal API (ChangeVisibility clears it), but the
        // service asserts Visibility == PublicLink explicitly rather than trusting that -- so a row
        // that ends up with a stale token anyway (a bug, a hand-edited row, data predating that
        // invariant) still 404s, with a body indistinguishable from a garbage token. This seeds
        // exactly that "should not normally happen" row directly against the database to exercise
        // the explicit check rather than the token-nulling side effect.
        const string staleToken = "stale-token-on-a-private-document";
        await using (var dbContext = CreateFreshDbContext())
        {
            var document = Document.Create(
                Guid.NewGuid(), Guid.NewGuid(), null, "file.txt", "text/plain", Bytes.Length, Visibility.PublicLink, TimeProvider.System);
            document.ChangeVisibility(Visibility.Private, staleToken, TimeProvider.System);
            dbContext.Documents.Add(document);
            dbContext.DocumentContents.Add(DocumentContent.Create(document.Id, Bytes));
            await dbContext.SaveChangesAsync();
        }

        using var anonymousClient = Factory.CreateClient();
        using var staleTokenResponse = await anonymousClient.GetAsync($"/public/documents/{staleToken}/content");
        using var garbageTokenResponse = await anonymousClient.GetAsync("/public/documents/not-a-real-token-either/content");

        Assert.Equal(HttpStatusCode.NotFound, staleTokenResponse.StatusCode);
        Assert.Equal(garbageTokenResponse.StatusCode, staleTokenResponse.StatusCode);
        var staleTokenBody = await staleTokenResponse.Content.ReadAsStringAsync();
        var garbageTokenBody = await garbageTokenResponse.Content.ReadAsStringAsync();
        Assert.Equal(garbageTokenBody, staleTokenBody);
        Assert.Contains("No document with this token.", staleTokenBody);
    }

    private async Task<Document> SeedDocumentAsync(
        Guid ownerUserId, Guid orgId, Visibility visibility, byte[] bytes, string contentType = "text/plain", string fileName = "file.txt")
    {
        await using var dbContext = CreateFreshDbContext();
        var document = Document.Create(
            ownerUserId, orgId, null, fileName, contentType, sizeBytes: bytes.Length, visibility, TimeProvider.System);
        dbContext.Documents.Add(document);
        dbContext.DocumentContents.Add(DocumentContent.Create(document.Id, bytes));
        await dbContext.SaveChangesAsync();
        return document;
    }

    private static async Task<string> SetPublicLinkAsync(HttpClient ownerClient, Guid documentId)
    {
        var response = await ownerClient.PutAsJsonAsync($"/documents/{documentId}/visibility", new { visibility = "PublicLink" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        return body!.PublicLinkToken!;
    }
}
