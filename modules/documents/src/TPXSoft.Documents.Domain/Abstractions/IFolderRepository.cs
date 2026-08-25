using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Domain.Abstractions;

public interface IFolderRepository
{
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <param name="parentFolderId">Null returns every one of the owner's folders, flat, every
    /// level. Non-null returns only direct children of that parent. Always ordered by
    /// name ASC, id ASC.</param>
    Task<IReadOnlyList<Folder>> ListAsync(Guid ownerUserId, Guid? parentFolderId, CancellationToken cancellationToken);

    /// <summary>Single-step ancestor lookup used by FolderCycleCheck -- the immediate parent id
    /// of <paramref name="folderId"/>, or null if it is a root folder (or does not exist).</summary>
    Task<Guid?> GetParentIdAsync(Guid folderId, CancellationToken cancellationToken);

    /// <summary>True if any folder has <paramref name="folderId"/> as its direct parent.</summary>
    Task<bool> HasChildFoldersAsync(Guid folderId, CancellationToken cancellationToken);

    void Add(Folder folder);

    void Remove(Folder folder);
}
