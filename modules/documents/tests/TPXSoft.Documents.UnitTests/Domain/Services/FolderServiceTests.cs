using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.Domain.Services;

/// <summary>Exercises FolderService's orchestration (validation, ownership, tri-state PATCH,
/// cycle detection, FK-race mapping) against in-memory fakes -- documentation/07-manage-folders.md
/// is the spec for every case here.</summary>
public sealed class FolderServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidNameAtRoot_Succeeds()
    {
        var builder = new FolderServiceTestBuilder();
        var service = builder.Build();
        var owner = Guid.NewGuid();

        var result = await service.CreateAsync(owner, "Q3 Reports", null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(owner, result.Value.OwnerUserId);
        Assert.Null(result.Value.ParentFolderId);
        Assert.Single(builder.FolderRepository.Added);
        Assert.Equal(1, builder.UnitOfWork.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_EmptyOrWhitespaceName_ReturnsValidationFailed(string name)
    {
        var service = new FolderServiceTestBuilder().Build();

        var result = await service.CreateAsync(Guid.NewGuid(), name, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ValidationFailed, result.Error);
    }

    [Fact]
    public async Task CreateAsync_NameOver255Characters_ReturnsValidationFailed()
    {
        var service = new FolderServiceTestBuilder().Build();
        var tooLong = new string('a', 256);

        var result = await service.CreateAsync(Guid.NewGuid(), tooLong, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ValidationFailed, result.Error);
    }

    [Fact]
    public async Task CreateAsync_UnderUnknownParent_ReturnsFolderNotFound()
    {
        var service = new FolderServiceTestBuilder().Build();

        var result = await service.CreateAsync(Guid.NewGuid(), "Reports", Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderNotFound, result.Error);
    }

    [Fact]
    public async Task CreateAsync_UnderAnotherUsersParent_ReturnsFolderNotFound_NotForbidden()
    {
        // createFolder defines no 403 -- a foreign parent is reported as 404, same as unknown,
        // leaking nothing (doc 07).
        var builder = new FolderServiceTestBuilder();
        var otherOwner = Guid.NewGuid();
        var foreignParent = Folder.Create(otherOwner, "Someone else's", null, builder.TimeProvider);
        builder.FolderRepository.Seed(foreignParent);
        var service = builder.Build();

        var result = await service.CreateAsync(Guid.NewGuid(), "Reports", foreignParent.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderNotFound, result.Error);
    }

    [Fact]
    public async Task CreateAsync_TwoFoldersSameNameSameParent_BothSucceed()
    {
        // Sibling names are not unique (doc 07) -- no 409 defined for createFolder.
        var builder = new FolderServiceTestBuilder();
        var service = builder.Build();
        var owner = Guid.NewGuid();

        var first = await service.CreateAsync(owner, "Reports", null, CancellationToken.None);
        var second = await service.CreateAsync(owner, "Reports", null, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.Id, second.Value.Id);
    }

    [Fact]
    public async Task ListAsync_WithoutParentFolderId_ReturnsAllOwnersFoldersFlat()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var root = Folder.Create(owner, "Root", null, builder.TimeProvider);
        var child = Folder.Create(owner, "Child", root.Id, builder.TimeProvider);
        var otherOwnersFolder = Folder.Create(Guid.NewGuid(), "Not mine", null, builder.TimeProvider);
        builder.FolderRepository.Seed(root);
        builder.FolderRepository.Seed(child);
        builder.FolderRepository.Seed(otherOwnersFolder);
        var service = builder.Build();

        var result = await service.ListAsync(owner, null, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.Id == root.Id);
        Assert.Contains(result, f => f.Id == child.Id);
    }

    [Fact]
    public async Task ListAsync_WithParentFolderId_ReturnsOnlyDirectChildren()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var root = Folder.Create(owner, "Root", null, builder.TimeProvider);
        var child = Folder.Create(owner, "Child", root.Id, builder.TimeProvider);
        var grandchild = Folder.Create(owner, "Grandchild", child.Id, builder.TimeProvider);
        builder.FolderRepository.Seed(root);
        builder.FolderRepository.Seed(child);
        builder.FolderRepository.Seed(grandchild);
        var service = builder.Build();

        var result = await service.ListAsync(owner, root.Id, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(child.Id, result[0].Id);
    }

    [Fact]
    public async Task ListAsync_ParentBelongingToAnotherUser_ReturnsEmpty()
    {
        // "GET /folders?parentFolderId=" another user's folder -- 200 [] (doc 07), no 403/404.
        var builder = new FolderServiceTestBuilder();
        var otherOwner = Guid.NewGuid();
        var foreignParent = Folder.Create(otherOwner, "Not mine", null, builder.TimeProvider);
        builder.FolderRepository.Seed(foreignParent);
        var service = builder.Build();

        var result = await service.ListAsync(Guid.NewGuid(), foreignParent.Id, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAsync_Owner_Succeeds()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();

        var result = await service.GetAsync(owner, folder.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(folder.Id, result.Value.Id);
    }

    [Fact]
    public async Task GetAsync_AnotherUsersFolder_ReturnsForbidden()
    {
        var builder = new FolderServiceTestBuilder();
        var folder = Folder.Create(Guid.NewGuid(), "Not mine", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();

        var result = await service.GetAsync(Guid.NewGuid(), folder.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderForbidden, result.Error);
    }

    [Fact]
    public async Task GetAsync_UnknownFolder_ReturnsNotFound()
    {
        var service = new FolderServiceTestBuilder().Build();

        var result = await service.GetAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderNotFound, result.Error);
    }

    [Fact]
    public async Task GetChildFoldersAsync_ReturnsDirectSubfoldersOnly_ExcludingGrandchildren()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var root = Folder.Create(owner, "Root", null, builder.TimeProvider);
        var child = Folder.Create(owner, "Child", root.Id, builder.TimeProvider);
        var grandchild = Folder.Create(owner, "Grandchild", child.Id, builder.TimeProvider);
        builder.FolderRepository.Seed(root);
        builder.FolderRepository.Seed(child);
        builder.FolderRepository.Seed(grandchild);
        var service = builder.Build();

        var result = await service.GetChildFoldersAsync(owner, root.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(child.Id, result.Value[0].Id);
    }

    [Fact]
    public async Task GetChildFoldersAsync_AnotherUsersFolder_ReturnsForbidden()
    {
        var builder = new FolderServiceTestBuilder();
        var folder = Folder.Create(Guid.NewGuid(), "Not mine", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();

        var result = await service.GetChildFoldersAsync(Guid.NewGuid(), folder.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderForbidden, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_NameOnly_LeavesParentFolderIdUnchanged()
    {
        // The tri-state regression test called out explicitly by documentation/README.md and
        // documentation/07-manage-folders.md: {"name": "x"} on a nested folder must NOT move it
        // to root. This is the most likely bug in this module.
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var parent = Folder.Create(owner, "Parent", null, builder.TimeProvider);
        var nested = Folder.Create(owner, "Nested", parent.Id, builder.TimeProvider);
        builder.FolderRepository.Seed(parent);
        builder.FolderRepository.Seed(nested);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, nested.Id, nameIsSet: true, "Archive", parentFolderIdIsSet: false, parentFolderId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Archive", result.Value.Name);
        Assert.Equal(parent.Id, result.Value.ParentFolderId);
    }

    [Fact]
    public async Task UpdateAsync_ParentFolderIdExplicitNull_MovesNestedFolderToRoot()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var parent = Folder.Create(owner, "Parent", null, builder.TimeProvider);
        var nested = Folder.Create(owner, "Nested", parent.Id, builder.TimeProvider);
        builder.FolderRepository.Seed(parent);
        builder.FolderRepository.Seed(nested);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, nested.Id, nameIsSet: false, name: null, parentFolderIdIsSet: true, parentFolderId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ParentFolderId);
        Assert.Equal("Nested", result.Value.Name);
    }

    [Fact]
    public async Task UpdateAsync_RefreshesUpdatedAt_OnSuccessfulChange()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        var createdAt = folder.UpdatedAt;
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();
        builder.TimeProvider.Advance(TimeSpan.FromMinutes(5));

        var result = await service.UpdateAsync(
            owner, folder.Id, nameIsSet: true, "Archive", parentFolderIdIsSet: false, parentFolderId: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.UpdatedAt > createdAt);
    }

    [Fact]
    public async Task UpdateAsync_EmptyName_ReturnsValidationFailed()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, folder.Id, nameIsSet: true, "   ", parentFolderIdIsSet: false, parentFolderId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ValidationFailed, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_UnknownFolder_ReturnsNotFound()
    {
        var service = new FolderServiceTestBuilder().Build();

        var result = await service.UpdateAsync(
            Guid.NewGuid(), Guid.NewGuid(), nameIsSet: true, "Archive", parentFolderIdIsSet: false, parentFolderId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderNotFound, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_AnotherUsersFolder_ReturnsForbidden()
    {
        var builder = new FolderServiceTestBuilder();
        var folder = Folder.Create(Guid.NewGuid(), "Not mine", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            Guid.NewGuid(), folder.Id, nameIsSet: true, "Archive", parentFolderIdIsSet: false, parentFolderId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderForbidden, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_NewParentBelongingToAnotherUser_ReturnsForbidden()
    {
        // Unlike createFolder, updateFolder DOES define 403 for a foreign parent (doc 07's
        // documented asymmetry).
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        var foreignParent = Folder.Create(Guid.NewGuid(), "Not mine", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        builder.FolderRepository.Seed(foreignParent);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, folder.Id, nameIsSet: false, name: null, parentFolderIdIsSet: true, foreignParent.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderForbidden, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_NewParentUnknown_ReturnsNotFound()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, folder.Id, nameIsSet: false, name: null, parentFolderIdIsSet: true, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderNotFound, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_MoveIntoItself_ReturnsCycleDetected()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, folder.Id, nameIsSet: false, name: null, parentFolderIdIsSet: true, folder.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.CycleDetected, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_MoveIntoOwnChild_ReturnsCycleDetected()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var parent = Folder.Create(owner, "Parent", null, builder.TimeProvider);
        var child = Folder.Create(owner, "Child", parent.Id, builder.TimeProvider);
        builder.FolderRepository.Seed(parent);
        builder.FolderRepository.Seed(child);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, parent.Id, nameIsSet: false, name: null, parentFolderIdIsSet: true, child.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.CycleDetected, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_MoveUnderUnrelatedFolder_Succeeds()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        var unrelated = Folder.Create(owner, "Unrelated", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        builder.FolderRepository.Seed(unrelated);
        var service = builder.Build();

        var result = await service.UpdateAsync(
            owner, folder.Id, nameIsSet: false, name: null, parentFolderIdIsSet: true, unrelated.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(unrelated.Id, result.Value.ParentFolderId);
    }

    [Fact]
    public async Task DeleteAsync_EmptyFolder_Succeeds()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();

        var result = await service.DeleteAsync(owner, folder.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(folder, builder.FolderRepository.Removed);
    }

    [Fact]
    public async Task DeleteAsync_FolderContainingSubfolder_ReturnsNotEmpty()
    {
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var parent = Folder.Create(owner, "Parent", null, builder.TimeProvider);
        var child = Folder.Create(owner, "Child", parent.Id, builder.TimeProvider);
        builder.FolderRepository.Seed(parent);
        builder.FolderRepository.Seed(child);
        var service = builder.Build();

        var result = await service.DeleteAsync(owner, parent.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderNotEmpty, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_SecondDeleteOnSameFolder_ReturnsNotFound()
    {
        // Not idempotent, unlike share revocation (doc 07 vs doc 04) -- a repeat returns 404.
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();

        var first = await service.DeleteAsync(owner, folder.Id, CancellationToken.None);
        var second = await service.DeleteAsync(owner, folder.Id, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal(DocumentError.FolderNotFound, second.Error);
    }

    [Fact]
    public async Task DeleteAsync_AnotherUsersFolder_ReturnsForbidden()
    {
        var builder = new FolderServiceTestBuilder();
        var folder = Folder.Create(Guid.NewGuid(), "Not mine", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        var service = builder.Build();

        var result = await service.DeleteAsync(Guid.NewGuid(), folder.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderForbidden, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_UnknownFolder_ReturnsNotFound()
    {
        var service = new FolderServiceTestBuilder().Build();

        var result = await service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderNotFound, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_ForeignKeyRaceOnSaveChanges_MapsToFolderNotEmpty()
    {
        // Something was created under this folder between the emptiness check and the delete --
        // the database's ON DELETE RESTRICT constraint is the source of truth regardless (doc 07).
        var builder = new FolderServiceTestBuilder();
        var owner = Guid.NewGuid();
        var folder = Folder.Create(owner, "Reports", null, builder.TimeProvider);
        builder.FolderRepository.Seed(folder);
        builder.UnitOfWork.ThrowOnSaveChanges =
            new ForeignKeyConstraintViolationException("fk violation", new InvalidOperationException());
        var service = builder.Build();

        var result = await service.DeleteAsync(owner, folder.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.FolderNotEmpty, result.Error);
    }
}
