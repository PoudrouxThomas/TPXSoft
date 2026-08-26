using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Domain.Abstractions;

public interface IDocumentShareRepository
{
    void Add(DocumentShare share);

    /// <summary>Ordered by created_at (documentation/04-sharing-and-visibility.md's
    /// listDocumentShares section).</summary>
    Task<IReadOnlyList<DocumentShare>> ListByDocumentAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>Looks up a single grant by its unique (documentId, grantedToUserId) pair -- used
    /// by revokeDocumentShare, which is idempotent and needs to know whether there is anything to
    /// remove without treating "nothing to remove" as an error.</summary>
    Task<DocumentShare?> GetAsync(Guid documentId, Guid grantedToUserId, CancellationToken cancellationToken);

    void Remove(DocumentShare share);
}
