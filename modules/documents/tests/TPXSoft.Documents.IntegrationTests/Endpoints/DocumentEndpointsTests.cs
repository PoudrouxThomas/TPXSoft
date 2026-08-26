using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Domain.Entities;
using TPXSoft.Documents.IntegrationTests.Fixtures;

namespace TPXSoft.Documents.IntegrationTests.Endpoints;

/// <summary>Drives the real GET /documents and GET /documents/{id} endpoints over HTTP against a
/// real Postgres database. Test list per documentation/02-virtual-folders.md's "Tests ->
/// Integration" section. POST /documents (upload) is exercised separately in
/// <see cref="UploadDocumentEndpointTests"/> -- documents here are still seeded directly through a
/// fresh DocumentsDbContext via <see cref="SeedDocumentAsync"/> so these tests can construct
/// states upload alone cannot produce yet (e.g. Organization visibility, a set
/// PublicLinkToken).</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DocumentEndpointsTests : DocumentsIntegrationTestBase
{
    // Mirrors Program.cs's ConfigureHttpJsonOptions -- the server serializes Visibility as a
    // string via JsonStringEnumConverter, but HttpContent.ReadFromJsonAsync's default options
    // don't know that unless told explicitly.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public DocumentEndpointsTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    [Fact]
    public async Task ListDocuments_SameOrgCaller_SeesOnlyTheOrgVisibleDocument()
    {
        var alice = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var aliceClient = CreateAuthenticatedClient(alice, orgId);
        var folder = await CreateFolderAsync(aliceClient, "Q3 Reports");

        // Placed inside a folder Bob does not own, on purpose: folderId absent from the query
        // must not mean "root only" -- it means no folder filter at all (documentation
        // 02-virtual-folders.md's "folderId absent" rule), so the org-visible document must still
        // surface for Bob even though he cannot see the folder it lives in.
        var privateDoc = await SeedDocumentAsync(alice, orgId, folderId: null, Visibility.Private);
        var orgVisibleDoc = await SeedDocumentAsync(alice, orgId, folderId: folder.Id, Visibility.Organization);

        using var bobClient = CreateAuthenticatedClient(Guid.NewGuid(), orgId);

        var response = await bobClient.GetAsync("/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<DocumentResponse>>(ResponseJsonOptions);
        Assert.NotNull(body);
        Assert.Contains(body!, d => d.Id == orgVisibleDoc.Id);
        Assert.DoesNotContain(body, d => d.Id == privateDoc.Id);
    }

    [Fact]
    public async Task ListDocuments_MineTrue_ExcludesEveryoneElsesDocuments()
    {
        var alice = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        await SeedDocumentAsync(alice, orgId, folderId: null, Visibility.Organization);

        using var bobClient = CreateAuthenticatedClient(Guid.NewGuid(), orgId);

        var response = await bobClient.GetAsync("/documents?mine=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<DocumentResponse>>(ResponseJsonOptions);
        Assert.Empty(body!);
    }

    [Fact]
    public async Task ListDocuments_DifferentOrgCaller_SeesNeitherDocument()
    {
        var alice = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var privateDoc = await SeedDocumentAsync(alice, orgId, folderId: null, Visibility.Private);
        var orgVisibleDoc = await SeedDocumentAsync(alice, orgId, folderId: null, Visibility.Organization);

        using var carolClient = CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid());

        var response = await carolClient.GetAsync("/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<DocumentResponse>>(ResponseJsonOptions);
        Assert.DoesNotContain(body!, d => d.Id == privateDoc.Id);
        Assert.DoesNotContain(body!, d => d.Id == orgVisibleDoc.Id);
    }

    [Fact(Skip = "Requires an explicit share grant (DocumentShare / POST /documents/{id}/shares), " +
        "which does not exist yet -- that's documentation/04-sharing-and-visibility.md. " +
        "DocumentAccessEvaluator.Evaluate already supports hasShareGrant: true (see the Domain " +
        "unit tests), but DocumentService has no way to produce that input until feature 04 lands " +
        "a share-grant lookup. Un-skip once feature 04 is built.")]
    public Task ListDocuments_Grantee_DoesNotSeeGrantedDocument_ButGetDocumentReturns200()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ListDocuments_FolderIdOwnedByAnotherUser_Returns200EmptyArray()
    {
        using var bobClient = CreateAuthenticatedClient(Guid.NewGuid());
        var bobsFolder = await CreateFolderAsync(bobClient, "Bob's folder");

        using var aliceClient = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await aliceClient.GetAsync($"/documents?folderId={bobsFolder.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<DocumentResponse>>(ResponseJsonOptions);
        Assert.Empty(body!);
    }

    [Fact]
    public async Task ListDocuments_FolderIdOwnedByCaller_ReturnsOnlyDirectChildren_NotASubfolders()
    {
        var alice = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var aliceClient = CreateAuthenticatedClient(alice, orgId);
        var root = await CreateFolderAsync(aliceClient, "Root");
        var child = await CreateFolderAsync(aliceClient, "Child", root.Id);

        var inRoot = await SeedDocumentAsync(alice, orgId, root.Id, Visibility.Private);
        var inChild = await SeedDocumentAsync(alice, orgId, child.Id, Visibility.Private);

        var response = await aliceClient.GetAsync($"/documents?folderId={root.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<DocumentResponse>>(ResponseJsonOptions);
        Assert.NotNull(body);
        Assert.Single(body!);
        Assert.Equal(inRoot.Id, body![0].Id);
        Assert.DoesNotContain(body, d => d.Id == inChild.Id);
    }

    [Fact]
    public async Task GetDocument_PublicLinkDocument_SameOrgNonOwner_Returns403()
    {
        var alice = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = await SeedDocumentAsync(alice, orgId, folderId: null, Visibility.PublicLink);

        using var bobClient = CreateAuthenticatedClient(Guid.NewGuid(), orgId);

        var response = await bobClient.GetAsync($"/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDocument_PublicLinkToken_PresentForOwner_AbsentForOtherCallers()
    {
        // Visibility PUT (feature 04) does not exist yet, so there is no real path that leaves a
        // document with both a non-owner-visible visibility and a set publicLinkToken. This seeds
        // that state directly to exercise DocumentEndpoints.ToResponse's masking rule (only the
        // owner ever gets a non-null publicLinkToken back) in isolation from feature 04.
        var alice = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = await SeedDocumentAsync(alice, orgId, folderId: null, Visibility.Organization);
        await SetPublicLinkTokenAsync(document.Id, "test-public-link-token");

        using var aliceClient = CreateAuthenticatedClient(alice, orgId);
        using var bobClient = CreateAuthenticatedClient(Guid.NewGuid(), orgId);

        var ownerResponse = await aliceClient.GetAsync($"/documents/{document.Id}");
        var otherResponse = await bobClient.GetAsync($"/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherResponse.StatusCode);
        var ownerBody = await ownerResponse.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        var otherBody = await otherResponse.Content.ReadFromJsonAsync<DocumentResponse>(ResponseJsonOptions);
        Assert.Equal("test-public-link-token", ownerBody!.PublicLinkToken);
        Assert.Null(otherBody!.PublicLinkToken);
    }

    private async Task<Document> SeedDocumentAsync(Guid ownerUserId, Guid orgId, Guid? folderId, Visibility visibility)
    {
        await using var dbContext = CreateFreshDbContext();
        var document = Document.Create(
            ownerUserId, orgId, folderId, "file.txt", "text/plain", sizeBytes: 100, visibility, TimeProvider.System);
        dbContext.Documents.Add(document);
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
