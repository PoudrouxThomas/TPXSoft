using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.Domain.Services;

/// <summary>Exercises DocumentService.UpdateAsync/DeleteAsync's orchestration (ownership, the
/// tri-state PATCH rule, target-folder validation, hard delete, the concurrency race) against
/// in-memory fakes -- documentation/03-rename-move-delete-document.md is the spec for every case
/// here.</summary>
public sealed class DocumentServiceUpdateDeleteTests
{
    [Fact]
    public async Task UpdateAsync_FileNameOnly_LeavesFolderIdUnchanged()
    {
        // The tri-state regression test called out explicitly by documentation/README.md and
        // documentation/03-rename-move-delete-document.md: {"fileName": "x"} on a document inside
        // a folder must NOT move it to root. This is the most likely bug in this module.
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        var document = Document.Create(
            owner, Guid.NewGuid(), folder.Id, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, document.Id, fileNameIsSet: true, "new.txt", folderIdIsSet: false, folderId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new.txt", result.Value.FileName);
        Assert.Equal(folder.Id, result.Value.FolderId);
    }

    [Fact]
    public async Task UpdateAsync_FolderIdExplicitNull_MovesFiledDocumentToRoot()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        var document = Document.Create(
            owner, Guid.NewGuid(), folder.Id, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, document.Id, fileNameIsSet: false, fileName: null, folderIdIsSet: true, folderId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.FolderId);
        Assert.Equal("report.pdf", result.Value.FileName);
    }

    [Fact]
    public async Task UpdateAsync_EmptyBody_IsANoOp_AndStillSucceeds()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, document.Id, fileNameIsSet: false, fileName: null, folderIdIsSet: false, folderId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("report.pdf", result.Value.FileName);
        Assert.Null(result.Value.FolderId);
        Assert.Equal(0, builder.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateAsync_RefreshesUpdatedAt_OnSuccessfulChange()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        var createdAt = document.UpdatedAt;
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();
        builder.TimeProvider.Advance(TimeSpan.FromMinutes(5));

        var result = await service.UpdateAsync(
            owner, document.Id, fileNameIsSet: true, "new.txt", folderIdIsSet: false, folderId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.UpdatedAt > createdAt);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotMutateVisibilityPublicLinkTokenOrSizeBytes()
    {
        // "A move never changes who can see a document" (doc 03's "On success" section).
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Organization, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, document.Id, fileNameIsSet: true, "new.txt", folderIdIsSet: true, folderId: folder.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Visibility.Organization, result.Value.Visibility);
        Assert.Null(result.Value.PublicLinkToken);
        Assert.Equal(100, result.Value.SizeBytes);
    }

    [Fact]
    public async Task UpdateAsync_UnknownDocument_ReturnsNotFound()
    {
        var service = new DocumentServiceTestBuilder().Build();

        var result = await service.UpdateAsync(
            Guid.NewGuid(), Guid.NewGuid(), fileNameIsSet: true, "new.txt", folderIdIsSet: false, folderId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_NonOwner_ReturnsNotOwner_BeforeValidatingBody()
    {
        // Order of checks matters: load-and-authorize before validating the body, so a non-owner
        // sending a malformed payload gets 403, not 400 (doc 03's "Order of checks matters" rule).
        var builder = new DocumentServiceTestBuilder();
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            Guid.NewGuid(), document.Id, fileNameIsSet: true, "   ", folderIdIsSet: false, folderId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotOwner, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_NonOwnerWithShareGrant_ReturnsNotOwner()
    {
        // Grants are read-only -- a grantee still cannot PATCH (doc 03's Integration test list;
        // DocumentShare does not exist yet, so this exercises plain non-ownership).
        var builder = new DocumentServiceTestBuilder();
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            Guid.NewGuid(), document.Id, fileNameIsSet: true, "new.txt", folderIdIsSet: false, folderId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotOwner, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_EmptyOrWhitespaceFileName_ReturnsValidationFailed(string fileName)
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, document.Id, fileNameIsSet: true, fileName, folderIdIsSet: false, folderId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ValidationFailed, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_FileNameOver255CharactersAfterSanitization_ReturnsValidationFailed_NotTruncated()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();
        var tooLong = new string('a', 300) + ".pdf";

        var result = await service.UpdateAsync(
            owner, document.Id, fileNameIsSet: true, tooLong, folderIdIsSet: false, folderId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ValidationFailed, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_MoveIntoUnknownFolder_ReturnsFolderNotFound()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, document.Id, fileNameIsSet: false, fileName: null, folderIdIsSet: true, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderNotFound, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_MoveIntoAnotherUsersFolder_ReturnsFolderForbidden()
    {
        // Unlike upload, updateDocument defines both 403 and 404 -- a foreign folder is 403, an
        // unknown one is 404 (doc 03's "Unlike upload" note).
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        var foreignFolder = Folder.Create(Guid.NewGuid(), "Not mine", null, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.FolderRepository.Seed(foreignFolder);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, document.Id, fileNameIsSet: false, fileName: null, folderIdIsSet: true, foreignFolder.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderForbidden, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_RenameToNameAlreadyUsedBySibling_Succeeds()
    {
        // Duplicate names remain legal -- renaming to match a sibling is not a conflict.
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var sibling = Document.Create(owner, orgId, null, "Q3 report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        var document = Document.Create(owner, orgId, null, "old.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(sibling);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, document.Id, fileNameIsSet: true, "Q3 report.pdf", folderIdIsSet: false, folderId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Q3 report.pdf", result.Value.FileName);
    }

    [Fact]
    public async Task DeleteAsync_Owner_RemovesTheDocument()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.DeleteAsync(owner, document.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(document, builder.DocumentRepository.Removed);
    }

    [Fact]
    public async Task DeleteAsync_SecondDeleteOnSameDocument_ReturnsNotFound()
    {
        // Not idempotent, unlike share revocation (doc 03 vs doc 04) -- a repeat returns 404.
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var first = await service.DeleteAsync(owner, document.Id, CancellationToken.None);
        var second = await service.DeleteAsync(owner, document.Id, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal(DocumentError.NotFound, second.Error);
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_ReturnsNotOwner_AndDocumentSurvives()
    {
        var builder = new DocumentServiceTestBuilder();
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.DeleteAsync(Guid.NewGuid(), document.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotOwner, result.Error);
        Assert.DoesNotContain(document, builder.DocumentRepository.Removed);
    }

    [Fact]
    public async Task DeleteAsync_UnknownDocument_ReturnsNotFound()
    {
        var service = new DocumentServiceTestBuilder().Build();

        var result = await service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotFound, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_ConcurrencyRaceOnSaveChanges_MapsToNotFound()
    {
        // Two simultaneous deletes race; the loser's SaveChangesAsync throws
        // DbUpdateConcurrencyException, which Infrastructure rethrows as
        // ConcurrencyConflictException -- the row is gone either way, which is what a 404
        // communicates to the loser (doc 03's "Concurrency" section).
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.UnitOfWork.ThrowOnSaveChanges =
            new ConcurrencyConflictException("concurrency conflict", new InvalidOperationException());
        var service = builder.Build();

        var result = await service.DeleteAsync(owner, document.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotFound, result.Error);
    }
}
