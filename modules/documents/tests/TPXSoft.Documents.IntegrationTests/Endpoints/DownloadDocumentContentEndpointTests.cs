using System.Net;
using System.Net.Http.Json;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Domain.Entities;
using TPXSoft.Documents.IntegrationTests.Fixtures;

namespace TPXSoft.Documents.IntegrationTests.Endpoints;

/// <summary>Drives the real GET /documents/{id}/content endpoint over HTTP against a real Postgres
/// database. Test list per documentation/05-preview-and-download.md's "Tests -> Integration"
/// section (the authenticated-route bullets).</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DownloadDocumentContentEndpointTests : DocumentsIntegrationTestBase
{
    private static readonly byte[] Bytes = "hello world"u8.ToArray();

    public DownloadDocumentContentEndpointTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    [Fact]
    public async Task Download_Owner_Returns200_WithBytesContentTypeAndHeaders()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, Bytes, contentType: "application/pdf");

        using var response = await client.GetAsync($"/documents/{document.Id}/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("attachment", response.Content.Headers.ContentDisposition!.ToString());
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Contains("nosniff", nosniff!);
    }

    [Fact]
    public async Task Download_ExplicitGrantee_OnPrivateDocument_Returns200()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var ownerClient = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, Bytes);
        var grantee = Guid.NewGuid();
        var shareResponse = await ownerClient.PostAsJsonAsync($"/documents/{document.Id}/shares", new { userId = grantee });
        Assert.Equal(HttpStatusCode.Created, shareResponse.StatusCode);

        using var granteeClient = CreateAuthenticatedClient(grantee, Guid.NewGuid());
        using var response = await granteeClient.GetAsync($"/documents/{document.Id}/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Download_SameOrgColleague_OnOrganizationDocument_Returns200()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Organization, Bytes);

        using var colleagueClient = CreateAuthenticatedClient(Guid.NewGuid(), orgId);
        using var response = await colleagueClient.GetAsync($"/documents/{document.Id}/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Download_SameOrgColleague_OnPrivateDocument_Returns403()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, Bytes);

        using var colleagueClient = CreateAuthenticatedClient(Guid.NewGuid(), orgId);
        using var response = await colleagueClient.GetAsync($"/documents/{document.Id}/content");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Download_SameOrgColleague_OnPublicLinkDocument_Returns403()
    {
        // A PublicLink document is not readable here by a non-owner -- public access goes through
        // the token route and nowhere else (documentation 05's authenticated-route section).
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = await SeedDocumentAsync(owner, orgId, Visibility.PublicLink, Bytes);

        using var colleagueClient = CreateAuthenticatedClient(Guid.NewGuid(), orgId);
        using var response = await colleagueClient.GetAsync($"/documents/{document.Id}/content");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Download_DifferentOrgUser_Returns403()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Organization, Bytes);

        using var otherOrgClient = CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid());
        using var response = await otherOrgClient.GetAsync($"/documents/{document.Id}/content");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Download_NoAuthorizationHeader_Returns401()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync($"/documents/{Guid.NewGuid()}/content");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Download_UnknownId_Returns404()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());

        using var response = await client.GetAsync($"/documents/{Guid.NewGuid()}/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_HtmlUpload_ResponseIsAttachmentWithNosniff_StoredXssRegression()
    {
        // Document.ContentType is uploader-chosen and untrusted (documentation 05's "Why this
        // matters" section) -- serving it back as `inline` would let an uploaded HTML file execute
        // in the app's own origin. attachment + nosniff closes that even when ContentType is
        // text/html.
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var htmlBytes = "<script>alert(document.cookie)</script>"u8.ToArray();
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, htmlBytes, contentType: "text/html", fileName: "payload.html");

        using var response = await client.GetAsync($"/documents/{document.Id}/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("attachment", response.Content.Headers.ContentDisposition!.ToString());
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Contains("nosniff", nosniff!);
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
}
