using System.Net;
using System.Net.Http.Json;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.IntegrationTests.Fixtures;

namespace TPXSoft.Documents.IntegrationTests.Endpoints;

/// <summary>Drives the real folder endpoints over HTTP against a real Postgres database. Test
/// list per documentation/07-manage-folders.md's "Tests -> Integration" section.</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class FolderEndpointsTests : DocumentsIntegrationTestBase
{
    public FolderEndpointsTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    [Fact]
    public async Task CreateFolder_AtRoot_Returns201WithNullParentFolderId()
    {
        var owner = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner);

        var response = await client.PostAsJsonAsync("/folders", new { name = "Q3 Reports" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FolderResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.ParentFolderId);
        Assert.Equal(owner, body.OwnerUserId);
    }

    [Fact]
    public async Task CreateFolder_UnderOwnedFolder_Returns201()
    {
        var owner = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner);
        var parent = await CreateFolderAsync(client, "Parent");

        var response = await client.PostAsJsonAsync("/folders", new { name = "Child", parentFolderId = parent.Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FolderResponse>();
        Assert.Equal(parent.Id, body!.ParentFolderId);
    }

    [Fact]
    public async Task CreateFolder_UnderAnotherUsersFolder_Returns404()
    {
        using var ownerClient = CreateAuthenticatedClient(Guid.NewGuid());
        var foreignParent = await CreateFolderAsync(ownerClient, "Not yours");
        using var otherClient = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await otherClient.PostAsJsonAsync("/folders", new { name = "Child", parentFolderId = foreignParent.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateFolder_EmptyOrWhitespaceName_Returns400(string name)
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/folders", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFolder_TwoFoldersSameNameSameParent_BothReturn201()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());

        var first = await client.PostAsJsonAsync("/folders", new { name = "Reports" });
        var second = await client.PostAsJsonAsync("/folders", new { name = "Reports" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task ListFolders_WithoutParentFolderId_ReturnsEveryLevelFlat()
    {
        var owner = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner);
        var root = await CreateFolderAsync(client, "Root");
        var child = await CreateFolderAsync(client, "Child", root.Id);

        var response = await client.GetAsync("/folders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<FolderResponse>>();
        Assert.NotNull(body);
        Assert.Contains(body!, f => f.Id == root.Id);
        Assert.Contains(body, f => f.Id == child.Id);
    }

    [Fact]
    public async Task ListFolders_WithParentFolderId_ReturnsOneLevel()
    {
        var owner = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner);
        var root = await CreateFolderAsync(client, "Root");
        var child = await CreateFolderAsync(client, "Child", root.Id);
        await CreateFolderAsync(client, "Grandchild", child.Id);

        var response = await client.GetAsync($"/folders?parentFolderId={root.Id}");

        var body = await response.Content.ReadFromJsonAsync<List<FolderResponse>>();
        Assert.NotNull(body);
        Assert.Single(body!);
        Assert.Equal(child.Id, body![0].Id);
    }

    [Fact]
    public async Task ListFolders_ParentBelongingToAnotherUser_ReturnsEmptyArray()
    {
        using var ownerClient = CreateAuthenticatedClient(Guid.NewGuid());
        var foreignFolder = await CreateFolderAsync(ownerClient, "Not yours");
        using var otherClient = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await otherClient.GetAsync($"/folders?parentFolderId={foreignFolder.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<FolderResponse>>();
        Assert.Empty(body!);
    }

    [Fact]
    public async Task GetFolder_AnotherUsersFolder_Returns403()
    {
        using var ownerClient = CreateAuthenticatedClient(Guid.NewGuid());
        var folder = await CreateFolderAsync(ownerClient, "Not yours");
        using var otherClient = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await otherClient.GetAsync($"/folders/{folder.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetFolder_UnknownFolder_Returns404()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.GetAsync($"/folders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetFolderChildren_ReturnsDirectSubfoldersAndDocuments_ExcludingGrandchildren()
    {
        var owner = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner);
        var root = await CreateFolderAsync(client, "Root");
        var child = await CreateFolderAsync(client, "Child", root.Id);
        await CreateFolderAsync(client, "Grandchild", child.Id);

        var response = await client.GetAsync($"/folders/{root.Id}/children");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FolderChildrenResponse>();
        Assert.NotNull(body);
        Assert.Single(body!.Folders);
        Assert.Equal(child.Id, body.Folders[0].Id);
        Assert.Empty(body.Documents);
    }

    [Fact]
    public async Task UpdateFolder_NameOnly_LeavesParentFolderIdUnchanged()
    {
        // The tri-state PATCH regression test: {"name": "x"} must not move a nested folder to
        // root (documentation/README.md + documentation/07-manage-folders.md).
        var owner = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner);
        var parent = await CreateFolderAsync(client, "Parent");
        var nested = await CreateFolderAsync(client, "Nested", parent.Id);

        var response = await client.PatchAsJsonAsync($"/folders/{nested.Id}", new { name = "Archive" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FolderResponse>();
        Assert.Equal("Archive", body!.Name);
        Assert.Equal(parent.Id, body.ParentFolderId);
    }

    [Fact]
    public async Task UpdateFolder_ParentFolderIdExplicitNull_MovesToRoot()
    {
        var owner = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner);
        var parent = await CreateFolderAsync(client, "Parent");
        var nested = await CreateFolderAsync(client, "Nested", parent.Id);

        var response = await client.PatchAsJsonAsync($"/folders/{nested.Id}", new { parentFolderId = (Guid?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FolderResponse>();
        Assert.Null(body!.ParentFolderId);
    }

    [Fact]
    public async Task UpdateFolder_MoveIntoOwnChild_Returns400()
    {
        var owner = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner);
        var parent = await CreateFolderAsync(client, "Parent");
        var child = await CreateFolderAsync(client, "Child", parent.Id);

        var response = await client.PatchAsJsonAsync($"/folders/{parent.Id}", new { parentFolderId = child.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFolder_MoveIntoItself_Returns400()
    {
        var owner = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner);
        var folder = await CreateFolderAsync(client, "Reports");

        var response = await client.PatchAsJsonAsync($"/folders/{folder.Id}", new { parentFolderId = folder.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFolder_EmptyFolder_Returns204()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());
        var folder = await CreateFolderAsync(client, "Reports");

        var response = await client.DeleteAsync($"/folders/{folder.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFolder_ContainingSubfolder_Returns409()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());
        var parent = await CreateFolderAsync(client, "Parent");
        await CreateFolderAsync(client, "Child", parent.Id);

        var response = await client.DeleteAsync($"/folders/{parent.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFolder_SecondDelete_Returns404()
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());
        var folder = await CreateFolderAsync(client, "Reports");
        await client.DeleteAsync($"/folders/{folder.Id}");

        var response = await client.DeleteAsync($"/folders/{folder.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFolder_AnotherUsersFolder_Returns403()
    {
        using var ownerClient = CreateAuthenticatedClient(Guid.NewGuid());
        var folder = await CreateFolderAsync(ownerClient, "Not yours");
        using var otherClient = CreateAuthenticatedClient(Guid.NewGuid());

        var response = await otherClient.DeleteAsync($"/folders/{folder.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(Skip = "documents.folder_id half of the emptiness check does not exist yet -- the " +
        "Document entity is not built until feature 01 (upload). FolderService.DeleteAsync's " +
        "own doc comment flags exactly this gap; the ON DELETE RESTRICT FK-violation catch is " +
        "already in place as the safety net either way. Un-skip once feature 01 lands.")]
    public async Task DeleteFolder_ContainingDocument_Returns409()
    {
    }

    [Fact]
    public async Task Endpoints_WithoutBearerToken_Return401()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/folders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<FolderResponse> CreateFolderAsync(HttpClient client, string name, Guid? parentFolderId = null)
    {
        var response = await client.PostAsJsonAsync("/folders", new { name, parentFolderId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FolderResponse>())!;
    }
}
