using Microsoft.EntityFrameworkCore;
using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Infrastructure.Persistence.Repositories;

public sealed class EfDocumentShareRepository : IDocumentShareRepository
{
    private readonly DocumentsDbContext _dbContext;

    public EfDocumentShareRepository(DocumentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(DocumentShare share) => _dbContext.DocumentShares.Add(share);

    public async Task<IReadOnlyList<DocumentShare>> ListByDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        await _dbContext.DocumentShares
            .Where(s => s.DocumentId == documentId)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

    public Task<DocumentShare?> GetAsync(Guid documentId, Guid grantedToUserId, CancellationToken cancellationToken) =>
        _dbContext.DocumentShares
            .SingleOrDefaultAsync(s => s.DocumentId == documentId && s.GrantedToUserId == grantedToUserId, cancellationToken);

    public void Remove(DocumentShare share) => _dbContext.DocumentShares.Remove(share);
}
