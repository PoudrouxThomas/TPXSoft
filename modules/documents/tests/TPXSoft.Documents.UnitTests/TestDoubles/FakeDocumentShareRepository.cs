using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.TestDoubles;

/// <summary>In-memory stand-in for <see cref="IDocumentShareRepository"/>, mirroring
/// EfDocumentShareRepository's query shapes closely enough for DocumentService unit tests.</summary>
internal sealed class FakeDocumentShareRepository : IDocumentShareRepository
{
    private readonly List<DocumentShare> _shares = new();

    /// <summary>Only shares added via <see cref="Add"/> during the test -- excludes seeded ones.</summary>
    public List<DocumentShare> Added { get; } = new();

    public List<DocumentShare> Removed { get; } = new();

    /// <summary>Pre-populates the repository as if this share already existed before the test ran.</summary>
    public void Seed(DocumentShare share) => _shares.Add(share);

    public void Add(DocumentShare share)
    {
        Added.Add(share);
        _shares.Add(share);
    }

    public Task<IReadOnlyList<DocumentShare>> ListByDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentShare> result = _shares
            .Where(s => s.DocumentId == documentId)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<DocumentShare?> GetAsync(Guid documentId, Guid grantedToUserId, CancellationToken cancellationToken) =>
        Task.FromResult(_shares.SingleOrDefault(s => s.DocumentId == documentId && s.GrantedToUserId == grantedToUserId));

    public void Remove(DocumentShare share)
    {
        Removed.Add(share);
        _shares.Remove(share);
    }
}
