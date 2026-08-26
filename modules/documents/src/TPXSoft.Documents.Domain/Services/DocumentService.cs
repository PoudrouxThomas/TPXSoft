using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Domain.Services;

/// <summary>
/// Application-level orchestration for reading documents (documentation/02-virtual-folders.md).
/// Upload/rename/move/delete/visibility/sharing land in later feature files.
/// </summary>
public sealed class DocumentService
{
    private readonly IDocumentRepository _documentRepository;

    public DocumentService(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    /// <summary>Always succeeds -- listDocuments defines no 403/404, an unresolvable or foreign
    /// folderId simply yields an empty list (documentation 02's "Unresolvable folderId"). The
    /// base visible set (owned UNION org-visible) and the mine/folderId filters are applied
    /// entirely inside the repository query.</summary>
    public Task<IReadOnlyList<Document>> ListAsync(
        Guid callerUserId, Guid callerOrgId, Guid? folderId, bool mine, CancellationToken cancellationToken) =>
        _documentRepository.ListAsync(callerUserId, callerOrgId, folderId, mine, cancellationToken);

    /// <summary>404 if the document does not exist at all, 403 if it exists but
    /// DocumentAccess.Evaluate returns None for the caller, 200 otherwise. hasShareGrant is
    /// always false for now -- DocumentShare does not exist yet (files 03-06 introduce it).
    /// </summary>
    /// <summary>Direct child documents of a folder, one level, never recursive -- used by
    /// GET /folders/{id}/children after FolderService has already authorized the folder itself.
    /// Folder trees are single-owner, so no separate document-level access filter applies here.
    /// </summary>
    public Task<IReadOnlyList<Document>> ListByFolderAsync(Guid folderId, CancellationToken cancellationToken) =>
        _documentRepository.ListByFolderAsync(folderId, cancellationToken);

    public async Task<Result<Document>> GetAsync(Guid callerUserId, Guid callerOrgId, Guid id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result<Document>.Failure(DocumentError.NotFound);
        }

        var access = DocumentAccessEvaluator.Evaluate(document, callerUserId, callerOrgId, hasShareGrant: false);
        if (access == DocumentAccess.None)
        {
            return Result<Document>.Failure(DocumentError.Forbidden);
        }

        return Result<Document>.Success(document);
    }
}
