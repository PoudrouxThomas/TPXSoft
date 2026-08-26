using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Domain.Entities;
using TPXSoft.Documents.IntegrationTests.Fixtures;

namespace TPXSoft.Documents.IntegrationTests.Endpoints;

/// <summary>Drives the real PUT /documents/{id}/content endpoint over HTTP against a real Postgres
/// database. Test list per documentation/06-update-document-content.md's "Tests -> Integration"
/// section.
///
/// GET /documents/{id}/content (documentation/05-preview-and-download.md) does not exist yet --
/// same gap already noted in UploadDocumentEndpointTests. Every bullet below that the spec phrases
/// as "... then GET .../content returns X" is instead verified by reading the document_contents
/// row directly through a fresh DocumentsDbContext, mirroring that existing precedent. The two
/// bullets phrased as "the grantee/public-link caller downloads the new bytes" cannot be driven
/// end-to-end over HTTP until feature 05 lands a download route; they are verified here as the
/// closest available proxy -- the underlying bytes changed and the grantee's share grant /
/// the document's public link token were left untouched by the replace -- and flagged inline.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ReplaceDocumentContentEndpointTests : DocumentsIntegrationTestBase
{
    // Mirrors Program.cs's ConfigureHttpJsonOptions -- the server serializes Visibility as a
    // string via JsonStringEnumConverter, but HttpContent.ReadFromJsonAsync's default options
    // don't know that unless told explicitly.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly byte[] OriginalBytes = "hello"u8.ToArray();

    private static readonly byte[] NewBytes = "goodbye world"u8.ToArray();

    public ReplaceDocumentContentEndpointTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    [Fact]
    public async Task ReplaceContent_Owner_Returns200_AndStoredBytesAndSizeMatchTheNewFile()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, OriginalBytes);

        using var response = await client.PutAsync(
            $"/documents/{document.Id}/content", BuildForm("new.bin", "application/octet-stream", NewBytes));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        Assert.Equal(NewBytes.Length, body!.SizeBytes);

        await using var dbContext = CreateFreshDbContext();
        var content = await dbContext.DocumentContents.SingleAsync(c => c.DocumentId == document.Id);
        Assert.Equal(NewBytes, content.Bytes);
    }

    [Fact]
    public async Task ReplaceContent_UploadedPartHasDifferentName_DocumentKeepsItsOriginalFileName()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, OriginalBytes, fileName: "report.pdf");

        using var response = await client.PutAsync(
            $"/documents/{document.Id}/content", BuildForm("report-final-v3.pdf", "application/pdf", NewBytes));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        Assert.Equal("report.pdf", body!.FileName);
    }

    [Fact]
    public async Task ReplaceContent_OnASharedDocument_GranteesShareGrantSurvives_AndBytesChange()
    {
        // Proxy for "grantee downloads the new bytes with no re-grant" -- see class doc comment.
        // No call to POST /documents/{id}/shares is made after the replace, so the grantee's
        // existing grant surviving (and still being the only grant row) demonstrates "no re-grant
        // needed"; GET .../content not existing yet means the actual download cannot be driven.
        // The grantee's read access itself is verified via GET /documents/{id} below.
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var ownerClient = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, OriginalBytes);
        var grantee = Guid.NewGuid();
        var shareResponse = await ownerClient.PostAsJsonAsync($"/documents/{document.Id}/shares", new { userId = grantee });
        Assert.Equal(HttpStatusCode.Created, shareResponse.StatusCode);

        using var replaceResponse = await ownerClient.PutAsync(
            $"/documents/{document.Id}/content", BuildForm("new.bin", "application/octet-stream", NewBytes));

        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        await using var dbContext = CreateFreshDbContext();
        var content = await dbContext.DocumentContents.SingleAsync(c => c.DocumentId == document.Id);
        Assert.Equal(NewBytes, content.Bytes);
        var shares = await dbContext.DocumentShares
            .Where(s => s.DocumentId == document.Id && s.GrantedToUserId == grantee)
            .ToListAsync();
        Assert.Single(shares);

        using var granteeClient = CreateAuthenticatedClient(grantee, Guid.NewGuid());
        var getResponse = await granteeClient.GetAsync($"/documents/{document.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task ReplaceContent_OnAPublicLinkDocument_TokenIsNotRotated_AndBytesChange()
    {
        // Proxy for "the same token now serves the new bytes" -- see class doc comment. The token
        // not rotating is verified directly; serving the bytes through the public route cannot be
        // driven until feature 05 lands GET /public/documents/{token}/content.
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, OriginalBytes);
        var visibilityResponse = await client.PutAsJsonAsync(
            $"/documents/{document.Id}/visibility", new { visibility = "PublicLink" });
        Assert.Equal(HttpStatusCode.OK, visibilityResponse.StatusCode);
        var visibilityBody = await visibilityResponse.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        var originalToken = visibilityBody!.PublicLinkToken;
        Assert.NotNull(originalToken);

        using var replaceResponse = await client.PutAsync(
            $"/documents/{document.Id}/content", BuildForm("new.bin", "application/octet-stream", NewBytes));

        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        var replaceBody = await replaceResponse.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        Assert.Equal(originalToken, replaceBody!.PublicLinkToken);
        await using var dbContext = CreateFreshDbContext();
        var content = await dbContext.DocumentContents.SingleAsync(c => c.DocumentId == document.Id);
        Assert.Equal(NewBytes, content.Bytes);
    }

    [Fact]
    public async Task ReplaceContent_EmptyFile_Returns400_AndOldBytesIntact()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, OriginalBytes);

        using var response = await client.PutAsync(
            $"/documents/{document.Id}/content", BuildForm("empty.bin", "application/octet-stream", []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var dbContext = CreateFreshDbContext();
        var content = await dbContext.DocumentContents.SingleAsync(c => c.DocumentId == document.Id);
        Assert.Equal(OriginalBytes, content.Bytes);
    }

    [Fact]
    public async Task ReplaceContent_OneByteOverMaxUploadBytes_Returns400_AndOldBytesIntact()
    {
        // Mirrors UploadDocumentEndpointTests' equivalent test -- a dedicated factory with a tiny
        // Documents:MaxUploadBytes so the test does not need to upload tens of megabytes.
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        await using var factory = new DocumentsWebApplicationFactory(ConnectionString, maxUploadBytes: 16);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.IssueFor(owner, orgId));
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, OriginalBytes);

        using var response = await client.PutAsync(
            $"/documents/{document.Id}/content", BuildForm("new.bin", "application/octet-stream", new byte[17]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var dbContext = CreateFreshDbContext();
        var content = await dbContext.DocumentContents.SingleAsync(c => c.DocumentId == document.Id);
        Assert.Equal(OriginalBytes, content.Bytes);
    }

    [Fact]
    public async Task ReplaceContent_ByGrantee_Returns403_AndBytesUnchanged()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var ownerClient = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, OriginalBytes);
        var grantee = Guid.NewGuid();
        var shareResponse = await ownerClient.PostAsJsonAsync($"/documents/{document.Id}/shares", new { userId = grantee });
        Assert.Equal(HttpStatusCode.Created, shareResponse.StatusCode);

        using var granteeClient = CreateAuthenticatedClient(grantee, Guid.NewGuid());
        using var response = await granteeClient.PutAsync(
            $"/documents/{document.Id}/content", BuildForm("new.bin", "application/octet-stream", NewBytes));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var dbContext = CreateFreshDbContext();
        var content = await dbContext.DocumentContents.SingleAsync(c => c.DocumentId == document.Id);
        Assert.Equal(OriginalBytes, content.Bytes);
    }

    [Fact]
    public async Task ReplaceContent_SameOrgColleagueOnOrganizationDocument_Returns403()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Organization, OriginalBytes);

        using var colleagueClient = CreateAuthenticatedClient(Guid.NewGuid(), orgId);
        using var response = await colleagueClient.PutAsync(
            $"/documents/{document.Id}/content", BuildForm("new.bin", "application/octet-stream", NewBytes));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var dbContext = CreateFreshDbContext();
        var content = await dbContext.DocumentContents.SingleAsync(c => c.DocumentId == document.Id);
        Assert.Equal(OriginalBytes, content.Bytes);
    }

    [Fact]
    public async Task ReplaceContent_UnknownDocument_Returns404()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());

        using var response = await client.PutAsync(
            $"/documents/{Guid.NewGuid()}/content", BuildForm("new.bin", "application/octet-stream", NewBytes));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceContent_NoAuthorizationHeader_Returns401()
    {
        using var client = Factory.CreateClient();

        using var response = await client.PutAsync(
            $"/documents/{Guid.NewGuid()}/content", BuildForm("new.bin", "application/octet-stream", NewBytes));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceContent_TwiceInARow_SecondSetOfBytesWins_AndCreatedAtNeverMoves()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private, OriginalBytes);
        var secondBytes = "third version of the bytes"u8.ToArray();

        using var first = await client.PutAsync(
            $"/documents/{document.Id}/content", BuildForm("v2.bin", "application/octet-stream", NewBytes));
        using var second = await client.PutAsync(
            $"/documents/{document.Id}/content", BuildForm("v3.bin", "application/octet-stream", secondBytes));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        var secondBody = await second.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        // Compared against the first replace's response CreatedAt, not the original in-memory seed
        // value -- both responses round-tripped through the same Postgres timestamptz (microsecond
        // precision) and JSON serialization, so they compare equal without a sub-tick precision
        // mismatch that comparing against the pre-persistence in-memory value would introduce.
        Assert.Equal(firstBody!.CreatedAt, secondBody!.CreatedAt);

        await using var dbContext = CreateFreshDbContext();
        var content = await dbContext.DocumentContents.SingleAsync(c => c.DocumentId == document.Id);
        Assert.Equal(secondBytes, content.Bytes);
    }

    private async Task<Document> SeedDocumentAsync(
        Guid ownerUserId, Guid orgId, Visibility visibility, byte[] bytes, string fileName = "file.txt")
    {
        await using var dbContext = CreateFreshDbContext();
        var document = Document.Create(
            ownerUserId, orgId, null, fileName, "text/plain", sizeBytes: bytes.Length, visibility, TimeProvider.System);
        dbContext.Documents.Add(document);
        dbContext.DocumentContents.Add(DocumentContent.Create(document.Id, bytes));
        await dbContext.SaveChangesAsync();
        return document;
    }

    private static MultipartFormDataContent BuildForm(string fileName, string contentType, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }
}
