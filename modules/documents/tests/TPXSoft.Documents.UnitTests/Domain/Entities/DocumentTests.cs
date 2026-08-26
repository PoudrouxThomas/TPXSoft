using Microsoft.Extensions.Time.Testing;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.Domain.Entities;

/// <summary>documentation/01-upload-document.md's "Tests -> Unit" section: Document.Create's
/// upload-relevant guarantees.</summary>
public sealed class DocumentTests
{
    [Fact]
    public void Create_WithPrivateVisibility_SetsPublicLinkTokenNull()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 1024, Visibility.Private, timeProvider);

        Assert.Equal(Visibility.Private, document.Visibility);
        Assert.Null(document.PublicLinkToken);
    }

    [Fact]
    public void Create_SetsCreatedAtEqualToUpdatedAt_FromFrozenTimeProvider()
    {
        var frozen = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(frozen);

        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 1024, Visibility.Private, timeProvider);

        Assert.Equal(frozen, document.CreatedAt);
        Assert.Equal(document.CreatedAt, document.UpdatedAt);
    }

    // Document.Rename itself, like Folder.Rename, trusts an already-validated/normalized name --
    // FileNameSanitizer.TryNormalizeStrict is what rejects empty/whitespace input (see
    // FileNameSanitizerTests), and DocumentServiceUpdateDeleteTests exercises the resulting
    // ValidationFailed through DocumentService.UpdateAsync (documentation
    // 03-rename-move-delete-document.md's "Tests -> Unit" section).

    [Fact]
    public void Rename_RefreshesUpdatedAt_ButLeavesCreatedAtUnchanged()
    {
        var createdAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(createdAt);
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 1024, Visibility.Private, timeProvider);

        var renamedAt = createdAt.AddMinutes(5);
        timeProvider.SetUtcNow(renamedAt);
        document.Rename("Q3 report.pdf", timeProvider);

        Assert.Equal("Q3 report.pdf", document.FileName);
        Assert.Equal(renamedAt, document.UpdatedAt);
        Assert.Equal(createdAt, document.CreatedAt);
    }

    [Fact]
    public void MoveTo_SetsFolderId_AndRefreshesUpdatedAt()
    {
        var createdAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(createdAt);
        var newFolderId = Guid.NewGuid();
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 1024, Visibility.Private, timeProvider);

        var movedAt = createdAt.AddMinutes(10);
        timeProvider.SetUtcNow(movedAt);
        document.MoveTo(newFolderId, timeProvider);

        Assert.Equal(newFolderId, document.FolderId);
        Assert.Equal(movedAt, document.UpdatedAt);
    }

    [Fact]
    public void MoveTo_Null_MovesDocumentToRoot()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "report.pdf", "application/pdf", 1024, Visibility.Private, timeProvider);

        document.MoveTo(null, timeProvider);

        Assert.Null(document.FolderId);
    }

    [Fact]
    public void Rename_DoesNotMutateVisibilityPublicLinkTokenSizeBytesOrCreatedAt()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 1024, Visibility.Organization, timeProvider);
        var createdAt = document.CreatedAt;

        document.Rename("Archive.pdf", timeProvider);

        Assert.Equal(Visibility.Organization, document.Visibility);
        Assert.Null(document.PublicLinkToken);
        Assert.Equal(1024, document.SizeBytes);
        Assert.Equal(createdAt, document.CreatedAt);
    }

    [Fact]
    public void MoveTo_DoesNotMutateVisibilityPublicLinkTokenSizeBytesOrCreatedAt()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 1024, Visibility.Organization, timeProvider);
        var createdAt = document.CreatedAt;

        document.MoveTo(Guid.NewGuid(), timeProvider);

        Assert.Equal(Visibility.Organization, document.Visibility);
        Assert.Null(document.PublicLinkToken);
        Assert.Equal(1024, document.SizeBytes);
        Assert.Equal(createdAt, document.CreatedAt);
    }

    // ReplaceContent itself, like Rename/MoveTo, trusts an already-sanitized contentType --
    // ContentTypeSanitizer.Normalize is what falls back to application/octet-stream for blank or
    // malformed input (see ContentTypeSanitizerTests), and
    // DocumentServiceReplaceContentTests exercises that fallback through
    // DocumentService.ReplaceContentAsync (documentation/06-update-document-content.md's
    // "Tests -> Unit" section, third bullet).

    [Fact]
    public void ReplaceContent_UpdatesContentTypeSizeBytesAndUpdatedAt()
    {
        // documentation/06-update-document-content.md's "Tests -> Unit" section, first bullet.
        var createdAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(createdAt);
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 1024, Visibility.Private, timeProvider);

        var replacedAt = createdAt.AddMinutes(5);
        timeProvider.SetUtcNow(replacedAt);
        document.ReplaceContent("image/png", 2048, timeProvider);

        Assert.Equal("image/png", document.ContentType);
        Assert.Equal(2048, document.SizeBytes);
        Assert.Equal(replacedAt, document.UpdatedAt);
    }

    [Fact]
    public void ReplaceContent_LeavesFileNameFolderIdVisibilityPublicLinkTokenAndCreatedAtUnchanged()
    {
        // documentation/06-update-document-content.md's "Tests -> Unit" section, second bullet, and
        // the "What changes and what does not" table.
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var folderId = Guid.NewGuid();
        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), folderId, "report.pdf", "application/pdf", 1024, Visibility.PublicLink, timeProvider);
        document.ChangeVisibility(Visibility.PublicLink, "some-token", timeProvider);
        var createdAt = document.CreatedAt;

        document.ReplaceContent("image/png", 2048, timeProvider);

        Assert.Equal("report.pdf", document.FileName);
        Assert.Equal(folderId, document.FolderId);
        Assert.Equal(Visibility.PublicLink, document.Visibility);
        Assert.Equal("some-token", document.PublicLinkToken);
        Assert.Equal(createdAt, document.CreatedAt);
    }
}
