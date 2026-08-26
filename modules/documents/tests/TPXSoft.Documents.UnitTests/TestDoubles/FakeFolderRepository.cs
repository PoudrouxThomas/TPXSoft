using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.TestDoubles;

/// <summary>In-memory stand-in for <see cref="IFolderRepository"/>, mirroring the query shapes
/// EfFolderRepository implements against Postgres (same ordering, same "direct children only"
/// semantics) so FolderService unit tests exercise the same contract the real repository
/// honors.</summary>
internal sealed class FakeFolderRepository : IFolderRepository
{
    private readonly List<Folder> _folders = new();

    /// <summary>Only folders added via <see cref="Add"/> during the test -- excludes seeded ones.</summary>
    public List<Folder> Added { get; } = new();

    public List<Folder> Removed { get; } = new();

    /// <summary>Pre-populates the repository as if this folder already existed before the test ran.</summary>
    public void Seed(Folder folder) => _folders.Add(folder);

    public Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_folders.SingleOrDefault(f => f.Id == id));

    public Task<IReadOnlyList<Folder>> ListAsync(Guid ownerUserId, Guid? parentFolderId, CancellationToken cancellationToken)
    {
        IEnumerable<Folder> query = _folders.Where(f => f.OwnerUserId == ownerUserId);

        if (parentFolderId is not null)
        {
            query = query.Where(f => f.ParentFolderId == parentFolderId);
        }

        IReadOnlyList<Folder> result = query
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ThenBy(f => f.Id)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<Guid?> GetParentIdAsync(Guid folderId, CancellationToken cancellationToken) =>
        Task.FromResult(_folders.SingleOrDefault(f => f.Id == folderId)?.ParentFolderId);

    public Task<bool> HasChildFoldersAsync(Guid folderId, CancellationToken cancellationToken) =>
        Task.FromResult(_folders.Any(f => f.ParentFolderId == folderId));

    public void Add(Folder folder)
    {
        Added.Add(folder);
        _folders.Add(folder);
    }

    public void Remove(Folder folder)
    {
        Removed.Add(folder);
        _folders.Remove(folder);
    }
}
