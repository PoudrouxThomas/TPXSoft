using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Domain.Entities;
using TPXSoft.Documents.IntegrationTests.Fixtures;

namespace TPXSoft.Documents.IntegrationTests.Endpoints;

/// <summary>Drives the real PATCH /documents/{id} and DELETE /documents/{id} endpoints over HTTP
/// against a real Postgres database. Test list per
/// documentation/03-rename-move-delete-document.md's "Tests -> Integration" section.</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class UpdateDeleteDocumentEndpointTests : DocumentsIntegrationTestBase
{
    // Mirrors Program.cs's ConfigureHttpJsonOptions -- the server serializes Visibility as a
    // string via JsonStringEnumConverter, but HttpContent.ReadFromJsonAsync's default options
    // don't know that unless told explicitly.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public UpdateDeleteDocumentEndpointTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    [Fact]
    public async Task UpdateDocument_FileNameOnly_LeavesFolderIdUnchanged()
    {
        // The tri-state PATCH regression test: {"fileName": "x"} must not move a filed document
        // to root (documentation/README.md + documentation 03).
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var folder = await CreateFolderAsync(client, "Reports");
        var document = await SeedDocumentAsync(owner, orgId, folder.Id, Visibility.Private);

        var response = await client.PatchAsJsonAsync($"/documents/{document.Id}", new { fileName = "new.txt" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        Assert.Equal("new.txt", body!.FileName);
        Assert.Equal(folder.Id, body.FolderId);
    }

    [Fact]
    public async Task UpdateDocument_FolderIdExplicitNull_MovesFiledDocumentToRoot()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var folder = await CreateFolderAsync(client, "Reports");
        var document = await SeedDocumentAsync(owner, orgId, folder.Id, Visibility.Private);

        var response = await client.PatchAsJsonAsync($"/documents/{document.Id}", new { folderId = (Guid?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        Assert.Null(body!.FolderId);
    }

    [Fact]
    public async Task UpdateDocument_EmptyBody_Returns200WithUnchangedDocument()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.Private);

        var response = await client.PatchAsJsonAsync($"/documents/{document.Id}", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        Assert.Equal(document.FileName, body!.FileName);
        Assert.Null(body.FolderId);
    }

    [Fact]
    public async Task UpdateDocument_MoveIntoAnotherUsersFolder_Returns403()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var ownerClient = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.Private);

        using var bobClient = CreateAuthenticatedClient(Guid.NewGuid());
        var bobsFolder = await CreateFolderAsync(bobClient, "Bob's folder");

        var response = await ownerClient.PatchAsJsonAsync($"/documents/{document.Id}", new { folderId = bobsFolder.Id });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDocument_MoveIntoUnknownFolder_Returns404()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.Private);

        var response = await client.PatchAsJsonAsync($"/documents/{document.Id}", new { folderId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDocument_NonOwnerSameOrgOrganizationDocument_Returns403()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.Organization);

        using var bobClient = CreateAuthenticatedClient(Guid.NewGuid(), orgId);

        var response = await bobClient.PatchAsJsonAsync($"/documents/{document.Id}", new { fileName = "hijacked.txt" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDocument_UnknownDocument_Returns404()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.PatchAsJsonAsync($"/documents/{Guid.NewGuid()}", new { fileName = "new.txt" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDocument_NonOwnerWithMalformedBody_Returns403_NotBadRequest()
    {
        // Order of checks matters end-to-end, not just inside DocumentService: load-and-authorize
        // the document before validating the body, so a non-owner sending a malformed payload
        // (whitespace-only fileName) still gets 403, not 400 -- otherwise the response would leak
        // that the payload reached a real document (doc 03's "Order of checks matters" rule).
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.Private);

        using var otherClient = CreateAuthenticatedClient(Guid.NewGuid());
        var response = await otherClient.PatchAsJsonAsync($"/documents/{document.Id}", new { fileName = "   " });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDocument_EmptyFileName_Returns400()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.Private);

        var response = await client.PatchAsJsonAsync($"/documents/{document.Id}", new { fileName = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDocument_RenameToNameAlreadyUsedBySibling_Returns200()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.Private, "Q3 report.pdf");
        var document = await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.Private, "old.pdf");

        var response = await client.PatchAsJsonAsync($"/documents/{document.Id}", new { fileName = "Q3 report.pdf" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDocument_Owner_Returns204_AndSecondDeleteReturns404()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.Private);

        var first = await client.DeleteAsync($"/documents/{document.Id}");
        var second = await client.DeleteAsync($"/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);

        await using var dbContext = CreateFreshDbContext();
        Assert.False(await dbContext.Documents.AnyAsync(d => d.Id == document.Id));
        Assert.False(await dbContext.DocumentContents.AnyAsync(c => c.DocumentId == document.Id));
    }

    [Fact]
    public async Task DeleteDocument_ByGrantee_Returns403_AndDocumentSurvives()
    {
        // No DocumentShare yet (feature 04) -- this exercises the plain non-owner path, which is
        // the same 403 a grantee would hit (grants never widen modify rights, doc 03/README).
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.Private);

        using var otherClient = CreateAuthenticatedClient(Guid.NewGuid());
        var response = await otherClient.DeleteAsync($"/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var dbContext = CreateFreshDbContext();
        Assert.True(await dbContext.Documents.AnyAsync(d => d.Id == document.Id));
    }

    [Fact]
    public async Task DeleteDocument_UnknownDocument_Returns404()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.DeleteAsync($"/documents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDocument_PublicLinkDocument_ThenPublicTokenRouteReturns404()
    {
        // GET /public/documents/{token}/content does not exist yet (feature 05) -- this asserts
        // the persistence-level consequence the route will rely on: the row (and its token) are
        // gone after delete, so any future lookup by that token has nothing to find.
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, folderId: null, Visibility.PublicLink);
        await SetPublicLinkTokenAsync(document.Id, "test-public-link-token");

        var response = await client.DeleteAsync($"/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var dbContext = CreateFreshDbContext();
        Assert.False(await dbContext.Documents.AnyAsync(d => d.PublicLinkToken == "test-public-link-token"));
    }

    [Fact]
    public async Task Endpoints_WithoutBearerToken_Return401()
    {
        using var client = Factory.CreateClient();

        var response = await client.PatchAsJsonAsync($"/documents/{Guid.NewGuid()}", new { fileName = "new.txt" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Document> SeedDocumentAsync(
        Guid ownerUserId, Guid orgId, Guid? folderId, Visibility visibility, string fileName = "file.txt")
    {
        await using var dbContext = CreateFreshDbContext();
        var document = Document.Create(
            ownerUserId, orgId, folderId, fileName, "text/plain", sizeBytes: 100, visibility, TimeProvider.System);
        dbContext.Documents.Add(document);
        dbContext.DocumentContents.Add(DocumentContent.Create(document.Id, "hello"u8.ToArray()));
        await dbContext.SaveChangesAsync();
        return document;
    }

    private async Task SetPublicLinkTokenAsync(Guid documentId, string token)
    {
        // Column names are PascalCase (matching the C# property names, see the AddDocument
        // migration) -- only the table name itself was made snake_case via ToTable(...).
        await using var dbContext = CreateFreshDbContext();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE documents SET \"PublicLinkToken\" = {token} WHERE \"Id\" = {documentId}");
    }

    private static async Task<FolderResponse> CreateFolderAsync(HttpClient client, string name, Guid? parentFolderId = null)
    {
        var response = await client.PostAsJsonAsync("/folders", new { name, parentFolderId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FolderResponse>())!;
    }
}
