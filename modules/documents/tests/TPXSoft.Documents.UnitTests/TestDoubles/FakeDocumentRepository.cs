using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.TestDoubles;

/// <summary>In-memory stand-in for <see cref="IDocumentRepository"/>, mirroring
/// EfDocumentRepository's query shapes closely enough for DocumentService unit tests.</summary>
internal sealed class FakeDocumentRepository : IDocumentRepository
{
    private readonly List<Document> _documents = new();

    private readonly List<DocumentContent> _contents = new();

    /// <summary>Only documents added via <see cref="Add"/> during the test -- excludes seeded ones.</summary>
    public List<Document> Added { get; } = new();

    public List<DocumentContent> AddedContent { get; } = new();

    public List<Document> Removed { get; } = new();

    /// <summary>Pre-populates the repository as if this document already existed before the test ran.</summary>
    public void Seed(Document document) => _documents.Add(document);

    /// <summary>Pre-populates the document_contents "table" as if this row already existed before
    /// the test ran -- mirrors <see cref="Seed"/> but for content, needed by
    /// GetContentAsync/ReplaceContentAsync tests.</summary>
    public void SeedContent(DocumentContent content) => _contents.Add(content);

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_documents.SingleOrDefault(d => d.Id == id));

    public Task<Document?> GetByPublicLinkTokenAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult(_documents.SingleOrDefault(d => d.PublicLinkToken == token));

    public Task<IReadOnlyList<Document>> ListAsync(
        Guid callerUserId, Guid callerOrgId, Guid? folderId, bool mine, CancellationToken cancellationToken)
    {
        IEnumerable<Document> query = mine
            ? _documents.Where(d => d.OwnerUserId == callerUserId)
            : _documents.Where(d =>
                d.OwnerUserId == callerUserId ||
                (d.OrgId == callerOrgId && d.Visibility == Visibility.Organization));

        if (folderId is { } id)
        {
            query = query.Where(d => d.FolderId == id);
        }

        IReadOnlyList<Document> result = query
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Document>> ListByFolderAsync(Guid folderId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Document> result = _documents
            .Where(d => d.FolderId == folderId)
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .ToList();

        return Task.FromResult(result);
    }

    public void Add(Document document)
    {
        Added.Add(document);
        _documents.Add(document);
    }

    public void AddContent(DocumentContent content)
    {
        AddedContent.Add(content);
        _contents.Add(content);
    }

    public Task<DocumentContent?> GetContentAsync(Guid documentId, CancellationToken cancellationToken) =>
        Task.FromResult(_contents.SingleOrDefault(c => c.DocumentId == documentId));

    public Task<byte[]?> GetContentBytesAsync(Guid documentId, CancellationToken cancellationToken) =>
        Task.FromResult(_contents.SingleOrDefault(c => c.DocumentId == documentId)?.Bytes);

    public void Remove(Document document)
    {
        Removed.Add(document);
        _documents.Remove(document);
    }
}
