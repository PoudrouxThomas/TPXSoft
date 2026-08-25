using Microsoft.EntityFrameworkCore;
using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Infrastructure.Persistence.Repositories;

public sealed class EfFolderRepository : IFolderRepository
{
    private readonly DocumentsDbContext _dbContext;

    public EfFolderRepository(DocumentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Folders.SingleOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Folder>> ListAsync(Guid ownerUserId, Guid? parentFolderId, CancellationToken cancellationToken)
    {
        var query = _dbContext.Folders.Where(f => f.OwnerUserId == ownerUserId);

        if (parentFolderId is not null)
        {
            query = query.Where(f => f.ParentFolderId == parentFolderId);
        }

        return await query
            .OrderBy(f => f.Name)
            .ThenBy(f => f.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Guid?> GetParentIdAsync(Guid folderId, CancellationToken cancellationToken) =>
        _dbContext.Folders
            .Where(f => f.Id == folderId)
            .Select(f => f.ParentFolderId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasChildFoldersAsync(Guid folderId, CancellationToken cancellationToken) =>
        _dbContext.Folders.AnyAsync(f => f.ParentFolderId == folderId, cancellationToken);

    public void Add(Folder folder) => _dbContext.Folders.Add(folder);

    public void Remove(Folder folder) => _dbContext.Folders.Remove(folder);
}
