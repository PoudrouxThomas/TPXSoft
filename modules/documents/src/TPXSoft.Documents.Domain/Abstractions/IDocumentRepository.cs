using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Domain.Abstractions;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Looks a document up by its public-link token only, never by id -- the public
    /// download route has no id in it and must never learn one
    /// (documentation/05-preview-and-download.md's "Public route" rule 1). Matches regardless of
    /// the document's current Visibility; the caller (DocumentService) asserts
    /// Visibility == PublicLink explicitly rather than relying on this query alone, so a bug that
    /// leaves a stale token around does not become a leak (rule 2).</summary>
    Task<Document?> GetByPublicLinkTokenAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// The base set is `owner_user_id = callerUserId OR (org_id = callerOrgId AND visibility =
    /// Organization)` -- one query with an OR, never two round trips (documentation/
    /// 02-virtual-folders.md's "Query notes"). <paramref name="mine"/> drops the org-visible
    /// branch, leaving only documents owned by the caller. <paramref name="folderId"/>, when
    /// given, additionally restricts to that folder's direct children -- an unresolvable or
    /// foreign folder id is not special-cased here, it simply yields no rows (the service layer
    /// never distinguishes that from "empty folder"). Ordered created_at DESC, id DESC.
    /// </summary>
    Task<IReadOnlyList<Document>> ListAsync(
        Guid callerUserId, Guid callerOrgId, Guid? folderId, bool mine, CancellationToken cancellationToken);

    /// <summary>Direct children of a given folder, regardless of caller -- used by
    /// FolderService/GET /folders/{id}/children, which has already authorized the folder itself
    /// (folders are single-owner, so no separate access filter is needed here).</summary>
    Task<IReadOnlyList<Document>> ListByFolderAsync(Guid folderId, CancellationToken cancellationToken);

    void Add(Document document);

    /// <summary>Persists a document's bytes -- always called alongside <see cref="Add"/> for the
    /// same document id in the same unit of work (documentation/01-upload-document.md's
    /// "Persistence" section: one transaction for both rows).</summary>
    void AddContent(DocumentContent content);

    /// <summary>Loads the document_contents row as an EF-tracked instance so the caller can mutate
    /// its Bytes in place via <see cref="DocumentContent.ReplaceBytes"/> and have the same
    /// SaveChangesAsync call emit an UPDATE (documentation/06-update-document-content.md's
    /// "Implementation" section). Null only if the document row exists without a matching content
    /// row, which should not happen given they are always created together.</summary>
    Task<DocumentContent?> GetContentAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>No-tracking projection straight to the raw bytes -- used only by the two content
    /// download routes (documentation/05-preview-and-download.md's "Serving the bytes" section).
    /// The DocumentContent entity itself is never tracked or loaded on this path, unlike
    /// <see cref="GetContentAsync"/>, which is used by the replace-content write path and needs a
    /// tracked instance to mutate in place. Null only if the document row exists without a
    /// matching content row, which should not happen given they are always created together.
    /// </summary>
    Task<byte[]?> GetContentBytesAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>Hard-deletes the document row. Its document_contents row (and, once feature 04
    /// lands, its document_shares rows) cascade via the database's own ON DELETE CASCADE --
    /// nothing else needs to be removed explicitly (documentation/03-rename-move-delete-document.md's
    /// "Delete" section).</summary>
    void Remove(Document document);
}
