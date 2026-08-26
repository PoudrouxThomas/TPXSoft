using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Domain.Services;

/// <summary>
/// Application-level orchestration for reading and uploading documents
/// (documentation/01-upload-document.md, documentation/02-virtual-folders.md).
/// Rename/move/delete/visibility/sharing land in later feature files.
/// </summary>
public sealed class DocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public DocumentService(
        IDocumentRepository documentRepository,
        IFolderRepository folderRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _documentRepository = documentRepository;
        _folderRepository = folderRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates a new, always-Private document plus its content row in one transaction
    /// (documentation/01-upload-document.md's "Behavior" section). <paramref name="rawFileName"/>
    /// and <paramref name="rawContentType"/> are attacker-controlled and are sanitized here;
    /// <paramref name="sizeBytes"/>/<paramref name="content"/> are expected to already agree (the
    /// caller has already read the file into memory to know its length). The contract defines no
    /// 403/404 for this route, so a missing/empty file, an unusable filename, and an unusable
    /// folderId all collapse into the same generic ValidationFailed -- a distinct "folder not
    /// found" would leak which folder ids exist in other accounts.
    /// </summary>
    public async Task<Result<Document>> UploadAsync(
        Guid ownerUserId,
        Guid orgId,
        Guid? folderId,
        string rawFileName,
        string? rawContentType,
        long sizeBytes,
        byte[] content,
        CancellationToken cancellationToken)
    {
        if (sizeBytes <= 0)
        {
            return Result<Document>.Failure(DocumentError.ValidationFailed);
        }

        if (!FileNameSanitizer.TryNormalize(rawFileName, out var fileName))
        {
            return Result<Document>.Failure(DocumentError.ValidationFailed);
        }

        if (folderId is { } id)
        {
            var folder = await _folderRepository.GetByIdAsync(id, cancellationToken);
            if (folder is null || folder.OwnerUserId != ownerUserId)
            {
                return Result<Document>.Failure(DocumentError.ValidationFailed);
            }
        }

        var contentType = ContentTypeSanitizer.Normalize(rawContentType);

        var document = Document.Create(
            ownerUserId, orgId, folderId, fileName, contentType, sizeBytes, Visibility.Private, _timeProvider);

        _documentRepository.Add(document);
        _documentRepository.AddContent(DocumentContent.Create(document.Id, content));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Document>.Success(document);
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

    /// <summary>
    /// Tri-state update: <paramref name="fileNameIsSet"/>/<paramref name="folderIdIsSet"/> tell
    /// apart "absent from the PATCH body" (leave alone) from "present" (apply, even when the
    /// present value is null -- move to root). Owner-only, and load-and-authorize happens before
    /// any body validation so a non-owner sending a malformed payload still gets 403, not 400
    /// (documentation/03-rename-move-delete-document.md's "Order of checks matters" rule). A move
    /// never touches Visibility, PublicLinkToken, SizeBytes, ContentType, or share grants.
    /// </summary>
    public async Task<Result<Document>> UpdateAsync(
        Guid callerUserId,
        Guid documentId,
        bool fileNameIsSet,
        string? fileName,
        bool folderIdIsSet,
        Guid? folderId,
        CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result<Document>.Failure(DocumentError.NotFound);
        }

        if (document.OwnerUserId != callerUserId)
        {
            return Result<Document>.Failure(DocumentError.NotOwner);
        }

        string? normalizedFileName = null;
        if (fileNameIsSet)
        {
            // No truncation here, unlike upload -- an overlong name after sanitization is
            // rejected outright rather than silently shortened (doc 03's validation table).
            if (fileName is null || !FileNameSanitizer.TryNormalizeStrict(fileName, out normalizedFileName))
            {
                return Result<Document>.Failure(DocumentError.ValidationFailed);
            }
        }

        if (folderIdIsSet && folderId is { } newFolderId)
        {
            // updateDocument defines both 403 and 404, unlike upload's single generic 400 -- a
            // missing folder is 404, a foreign folder is 403 (doc 03's "Unlike upload" note).
            var folder = await _folderRepository.GetByIdAsync(newFolderId, cancellationToken);
            if (folder is null)
            {
                return Result<Document>.Failure(DocumentError.FolderNotFound);
            }

            if (folder.OwnerUserId != callerUserId)
            {
                return Result<Document>.Failure(DocumentError.FolderForbidden);
            }
        }

        if (fileNameIsSet)
        {
            document.Rename(normalizedFileName!, _timeProvider);
        }

        if (folderIdIsSet)
        {
            document.MoveTo(folderId, _timeProvider);
        }

        if (fileNameIsSet || folderIdIsSet)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<Document>.Success(document);
    }

    /// <summary>
    /// Hard delete, owner-only. Not idempotent -- a repeat DELETE on an already-gone id is 404
    /// (doc 03), unlike share revocation. document_contents (and, once feature 04 lands,
    /// document_shares) cascade via the database's own ON DELETE CASCADE; nothing here removes
    /// them explicitly.
    /// </summary>
    public async Task<Result> DeleteAsync(Guid callerUserId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure(DocumentError.NotFound);
        }

        if (document.OwnerUserId != callerUserId)
        {
            return Result.Failure(DocumentError.NotOwner);
        }

        _documentRepository.Remove(document);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // A race lost to a simultaneous delete of the same document -- the row is gone
            // either way, which is what a 404 communicates to the loser (doc 03's "Concurrency"
            // section).
            return Result.Failure(DocumentError.NotFound);
        }

        return Result.Success();
    }
}
