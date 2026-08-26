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
}
