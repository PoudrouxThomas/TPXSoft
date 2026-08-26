using Microsoft.Extensions.Time.Testing;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.Domain.Entities;

public sealed class FolderTests
{
    [Fact]
    public void Create_SetsCreatedAtEqualToUpdatedAt_FromFrozenTimeProvider()
    {
        var frozen = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(frozen);

        var folder = Folder.Create(Guid.NewGuid(), "Q3 Reports", parentFolderId: null, timeProvider);

        Assert.Equal(frozen, folder.CreatedAt);
        Assert.Equal(folder.CreatedAt, folder.UpdatedAt);
    }

    [Fact]
    public void Create_AtRoot_LeavesParentFolderIdNull()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var folder = Folder.Create(Guid.NewGuid(), "Q3 Reports", parentFolderId: null, timeProvider);

        Assert.Null(folder.ParentFolderId);
    }

    [Fact]
    public void Rename_RefreshesUpdatedAt_ButLeavesCreatedAtUnchanged()
    {
        var createdAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(createdAt);
        var folder = Folder.Create(Guid.NewGuid(), "Reports", null, timeProvider);

        var renamedAt = createdAt.AddMinutes(5);
        timeProvider.SetUtcNow(renamedAt);
        folder.Rename("Archive", timeProvider);

        Assert.Equal("Archive", folder.Name);
        Assert.Equal(renamedAt, folder.UpdatedAt);
        Assert.Equal(createdAt, folder.CreatedAt);
    }

    [Fact]
    public void MoveTo_SetsParentFolderId_AndRefreshesUpdatedAt()
    {
        var createdAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(createdAt);
        var newParentId = Guid.NewGuid();
        var folder = Folder.Create(Guid.NewGuid(), "Reports", parentFolderId: null, timeProvider);

        var movedAt = createdAt.AddMinutes(10);
        timeProvider.SetUtcNow(movedAt);
        folder.MoveTo(newParentId, timeProvider);

        Assert.Equal(newParentId, folder.ParentFolderId);
        Assert.Equal(movedAt, folder.UpdatedAt);
    }

    [Fact]
    public void MoveTo_Null_MovesFolderToRoot()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var folder = Folder.Create(Guid.NewGuid(), "Reports", parentFolderId: Guid.NewGuid(), timeProvider);

        folder.MoveTo(null, timeProvider);

        Assert.Null(folder.ParentFolderId);
    }
}
