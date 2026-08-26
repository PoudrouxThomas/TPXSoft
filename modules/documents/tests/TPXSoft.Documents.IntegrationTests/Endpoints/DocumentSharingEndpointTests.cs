using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TPXSoft.Documents.Api.Contracts;
using TPXSoft.Documents.Domain.Entities;
using TPXSoft.Documents.IntegrationTests.Fixtures;

namespace TPXSoft.Documents.IntegrationTests.Endpoints;

/// <summary>
/// Drives the real sharing/visibility endpoints over HTTP against a real Postgres database, for
/// the behaviors that genuinely need the database rather than a fake -- the unique-index-backed
/// 409 on a duplicate grant, the ON DELETE CASCADE of document_shares, and the partial unique
/// index on public_link_token. Everything else in documentation/04-sharing-and-visibility.md's
/// "Tests -> Unit" list plus the owner/self-share/idempotency rules are covered against fakes in
/// TPXSoft.Documents.UnitTests.Domain.Services.DocumentServiceVisibilityTests/
/// DocumentServiceSharingTests -- this file only covers what a fake cannot: real constraints.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DocumentSharingEndpointTests : DocumentsIntegrationTestBase
{
    public DocumentSharingEndpointTests(PostgresFixture postgresFixture) : base(postgresFixture)
    {
    }

    [Fact]
    public async Task ShareDocument_SecondGrantForSameUser_Returns409_BackedByTheRealUniqueIndex()
    {
        // ShareAsync does not check-then-insert -- it relies entirely on the database's unique
        // (document_id, granted_to_user_id) index and translates the resulting Postgres 23505 into
        // ShareAlreadyExists (doc 04's shareDocumentWithUser section: "Do not rely on a
        // check-then-insert alone"). This exercises that translation against a real constraint,
        // not a faked exception.
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private);
        var bob = Guid.NewGuid();

        var first = await client.PostAsJsonAsync($"/documents/{document.Id}/shares", new { userId = bob });
        var second = await client.PostAsJsonAsync($"/documents/{document.Id}/shares", new { userId = bob });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        await using var dbContext = CreateFreshDbContext();
        var count = await dbContext.DocumentShares.CountAsync(s => s.DocumentId == document.Id && s.GrantedToUserId == bob);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DeleteDocument_WithExistingShares_CascadesToDocumentShares()
    {
        // "document_shares ... FK -> documents(id) ON DELETE CASCADE" (doc 04's Persistence
        // section) -- deleting the document must take its grants with it at the database level,
        // not just via application code.
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(owner, orgId);
        var document = await SeedDocumentAsync(owner, orgId, Visibility.Private);
        var bob = Guid.NewGuid();
        var shareResponse = await client.PostAsJsonAsync($"/documents/{document.Id}/shares", new { userId = bob });
        Assert.Equal(HttpStatusCode.Created, shareResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/documents/{document.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        await using var dbContext = CreateFreshDbContext();
        Assert.False(await dbContext.DocumentShares.AnyAsync(s => s.DocumentId == document.Id));
    }

    [Fact]
    public async Task PublicLinkToken_UniqueIndex_RejectsASecondDocumentWithTheSameToken()
    {
        // "Unique partial index on public_link_token ... the index is there so a bug cannot
        // produce two documents behind one link" (doc 04's Token generation and storage section).
        // Real token generation makes a natural collision astronomically unlikely, so this drives
        // the constraint directly with raw SQL to confirm the migration actually created it.
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var first = await SeedDocumentAsync(owner, orgId, Visibility.PublicLink);
        var second = await SeedDocumentAsync(owner, orgId, Visibility.PublicLink);

        await using var dbContext = CreateFreshDbContext();
        const string sharedToken = "collision-token-for-partial-unique-index-test";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE documents SET \"PublicLinkToken\" = {sharedToken} WHERE \"Id\" = {first.Id}");

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE documents SET \"PublicLinkToken\" = {sharedToken} WHERE \"Id\" = {second.Id}"));

        Assert.Equal("23505", exception.SqlState);
    }

    [Fact]
    public async Task PublicLinkToken_PartialUniqueIndex_AllowsMultipleDocumentsWithNullToken()
    {
        // The index is filtered to "PublicLinkToken IS NOT NULL" -- Private/Organization documents
        // (token always null) must not collide with each other under the same index.
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        await SeedDocumentAsync(owner, orgId, Visibility.Private);
        await SeedDocumentAsync(owner, orgId, Visibility.Private);

        await using var dbContext = CreateFreshDbContext();
        var nullTokenCount = await dbContext.Documents.CountAsync(d => d.OwnerUserId == owner && d.PublicLinkToken == null);

        Assert.Equal(2, nullTokenCount);
    }

    private async Task<Document> SeedDocumentAsync(Guid ownerUserId, Guid orgId, Visibility visibility)
    {
        await using var dbContext = CreateFreshDbContext();
        var document = Document.Create(
            ownerUserId, orgId, null, "file.txt", "text/plain", sizeBytes: 100, visibility, TimeProvider.System);
        dbContext.Documents.Add(document);
        dbContext.DocumentContents.Add(DocumentContent.Create(document.Id, "hello"u8.ToArray()));
        await dbContext.SaveChangesAsync();
        return document;
    }
}
