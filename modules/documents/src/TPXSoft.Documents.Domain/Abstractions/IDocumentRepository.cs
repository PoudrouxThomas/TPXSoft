using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Domain.Abstractions;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

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
}
