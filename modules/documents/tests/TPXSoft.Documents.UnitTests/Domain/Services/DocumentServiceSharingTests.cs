using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.Domain.Services;

/// <summary>Exercises DocumentService.ListSharesAsync/ShareAsync/RevokeShareAsync against in-memory
/// fakes -- documentation/04-sharing-and-visibility.md's "Per-user grants" section and "Tests"
/// list are the spec for every case here.</summary>
public sealed class DocumentServiceSharingTests
{
    [Fact]
    public async Task ShareAsync_SelfShare_ReturnsValidationFailed()
    {
        // "Sharing with yourself is 400" (doc 04's shareDocumentWithUser section).
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.ShareAsync(owner, document.Id, owner, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ValidationFailed, result.Error);
        Assert.Empty(builder.DocumentShareRepository.Added);
    }

    [Fact]
    public async Task ShareAsync_DuplicateGrant_ReturnsShareAlreadyExists()
    {
        // "A second grant for the same user is 409... back it with a unique index... and translate
        // the resulting DbUpdateException into ShareAlreadyExists" (doc 04's shareDocumentWithUser
        // section) -- simulated here via UnitOfWork throwing the same
        // UniqueConstraintViolationException Infrastructure translates the Postgres 23505 into.
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.UnitOfWork.ThrowOnSaveChanges =
            new UniqueConstraintViolationException("duplicate grant", new InvalidOperationException());
        var service = builder.Build();

        var result = await service.ShareAsync(owner, document.Id, targetUserId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ShareAlreadyExists, result.Error);
    }

    [Fact]
    public async Task ListSharesAsync_NonOwner_ReturnsNotOwner()
    {
        // Owner only -- 403 for everyone else, including the grantees themselves (doc 04's
        // listDocumentShares section).
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        var grant = DocumentShare.Create(document.Id, grantee, owner, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentShareRepository.Seed(grant);
        var service = builder.Build();

        var result = await service.ListSharesAsync(grantee, document.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotOwner, result.Error);
    }

    [Fact]
    public async Task RevokeShareAsync_NonExistentGrant_StillSucceeds()
    {
        // "Idempotent by contract: 204 whether or not a grant existed" (doc 04's
        // revokeDocumentShare section).
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.RevokeShareAsync(owner, document.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RevokeShareAsync_ExistingGrant_RemovesIt_AndRevokingAgainStillSucceeds()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        var grant = DocumentShare.Create(document.Id, grantee, owner, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentShareRepository.Seed(grant);
        var service = builder.Build();

        var first = await service.RevokeShareAsync(owner, document.Id, grantee, CancellationToken.None);
        var second = await service.RevokeShareAsync(owner, document.Id, grantee, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Contains(grant, builder.DocumentShareRepository.Removed);
    }

    [Fact]
    public async Task RevokeShareAsync_NonOwner_ReturnsNotOwner()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        var grant = DocumentShare.Create(document.Id, grantee, owner, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentShareRepository.Seed(grant);
        var service = builder.Build();

        var result = await service.RevokeShareAsync(Guid.NewGuid(), document.Id, grantee, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotOwner, result.Error);
        Assert.DoesNotContain(grant, builder.DocumentShareRepository.Removed);
    }
}
