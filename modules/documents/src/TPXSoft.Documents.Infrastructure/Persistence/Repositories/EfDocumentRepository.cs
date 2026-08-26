using Microsoft.EntityFrameworkCore;
using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Infrastructure.Persistence.Repositories;

public sealed class EfDocumentRepository : IDocumentRepository
{
    private readonly DocumentsDbContext _dbContext;

    public EfDocumentRepository(DocumentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Documents.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Document>> ListAsync(
        Guid callerUserId, Guid callerOrgId, Guid? folderId, bool mine, CancellationToken cancellationToken)
    {
        // One query with an OR, not two round trips -- documentation/02-virtual-folders.md's
        // "Query notes". mine=true drops the org-visible branch entirely.
        var query = mine
            ? _dbContext.Documents.Where(d => d.OwnerUserId == callerUserId)
            : _dbContext.Documents.Where(d =>
                d.OwnerUserId == callerUserId ||
                (d.OrgId == callerOrgId && d.Visibility == Visibility.Organization));

        if (folderId is { } id)
        {
            query = query.Where(d => d.FolderId == id);
        }

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> ListByFolderAsync(Guid folderId, CancellationToken cancellationToken) =>
        await _dbContext.Documents
            .Where(d => d.FolderId == folderId)
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .ToListAsync(cancellationToken);

    public void Add(Document document) => _dbContext.Documents.Add(document);

    public void AddContent(DocumentContent content) => _dbContext.DocumentContents.Add(content);

    public Task<DocumentContent?> GetContentAsync(Guid documentId, CancellationToken cancellationToken) =>
        _dbContext.DocumentContents.SingleOrDefaultAsync(c => c.DocumentId == documentId, cancellationToken);

    public void Remove(Document document) => _dbContext.Documents.Remove(document);
}
