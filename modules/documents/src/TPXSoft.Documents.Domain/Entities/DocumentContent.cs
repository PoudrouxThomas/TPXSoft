namespace TPXSoft.Documents.Domain.Entities;

/// <summary>
/// The raw bytes of a Document, split into its own 1:1 table so the expensive read stays out of
/// every listing/metadata query (documentation/README.md's "Why content is a separate table").
/// Never crosses the wire directly -- only GET/PUT /documents/{id}/content (file 05/06) touch it.
/// </summary>
public sealed class DocumentContent
{
    // Private parameterless ctor for EF Core materialization only; use Create() elsewhere.
    private DocumentContent()
    {
    }

    /// <summary>PK and FK to Document, 1:1 -- ON DELETE CASCADE, unlike Document.FolderId's
    /// RESTRICT: deleting a document must take its bytes with it.</summary>
    public Guid DocumentId { get; private set; }

    public byte[] Bytes { get; private set; } = [];

    public static DocumentContent Create(Guid documentId, byte[] bytes) => new()
    {
        DocumentId = documentId,
        Bytes = bytes
    };

    /// <summary>
    /// Updates the row in place -- called by DocumentService.ReplaceContentAsync on an
    /// already-loaded, EF-tracked instance so SaveChangesAsync emits an UPDATE, not a
    /// delete-then-insert (documentation/06-update-document-content.md's "Implementation"
    /// section). Does not touch DocumentId.
    /// </summary>
    public void ReplaceBytes(byte[] bytes) => Bytes = bytes;
}
