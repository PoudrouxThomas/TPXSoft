namespace TPXSoft.Documents.Domain.Entities;

/// <summary>
/// An explicit per-user read grant on a Document, independent of Visibility -- it neither widens
/// listing nor is revoked by narrowing Visibility (documentation/04-sharing-and-visibility.md's
/// "Two independent axes" section).
/// </summary>
public sealed class DocumentShare
{
    // Private parameterless ctor for EF Core materialization only; use Create() elsewhere.
    private DocumentShare()
    {
    }

    public Guid Id { get; private set; }

    public Guid DocumentId { get; private set; }

    public Guid GrantedToUserId { get; private set; }

    /// <summary>The caller who created the grant -- always the document's owner at the time of
    /// grant, never a body field. Exists so a future audit view can answer "who shared this"
    /// (documentation 04's POST section).</summary>
    public Guid GrantedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static DocumentShare Create(
        Guid documentId, Guid grantedToUserId, Guid grantedByUserId, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            GrantedToUserId = grantedToUserId,
            GrantedByUserId = grantedByUserId,
            CreatedAt = timeProvider.GetUtcNow()
        };
}
