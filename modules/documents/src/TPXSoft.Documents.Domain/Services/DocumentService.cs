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
    private readonly IDocumentShareRepository _documentShareRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public DocumentService(
        IDocumentRepository documentRepository,
        IFolderRepository folderRepository,
        IDocumentShareRepository documentShareRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _documentRepository = documentRepository;
        _folderRepository = folderRepository;
        _documentShareRepository = documentShareRepository;
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

    /// <summary>Direct child documents of a folder, one level, never recursive -- used by
    /// GET /folders/{id}/children after FolderService has already authorized the folder itself.
    /// Folder trees are single-owner, so no separate document-level access filter applies here.
    /// </summary>
    public Task<IReadOnlyList<Document>> ListByFolderAsync(Guid folderId, CancellationToken cancellationToken) =>
        _documentRepository.ListByFolderAsync(folderId, cancellationToken);

    /// <summary>404 if the document does not exist at all, 403 if it exists but
    /// DocumentAccess.Evaluate returns None for the caller, 200 otherwise.</summary>
    public async Task<Result<Document>> GetAsync(Guid callerUserId, Guid callerOrgId, Guid id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result<Document>.Failure(DocumentError.NotFound);
        }

        var share = await _documentShareRepository.GetAsync(document.Id, callerUserId, cancellationToken);
        var access = DocumentAccessEvaluator.Evaluate(document, callerUserId, callerOrgId, hasShareGrant: share is not null);
        if (access == DocumentAccess.None)
        {
            return Result<Document>.Failure(DocumentError.Forbidden);
        }

        return Result<Document>.Success(document);
    }

    /// <summary>
    /// 404 if the document does not exist at all, 403 if it exists but DocumentAccess.Evaluate
    /// returns None for the caller, 200 + bytes otherwise -- the same access rule as
    /// <see cref="GetAsync"/> (Read or Owner), but downloadDocumentContent has its own 403 message
    /// per documentation/05-preview-and-download.md's Errors table, hence
    /// <see cref="DocumentError.ContentForbidden"/> rather than <see cref="DocumentError.Forbidden"/>.
    /// A PublicLink document is not readable here by a non-owner/non-grantee/non-org caller --
    /// public access goes through <see cref="DownloadByPublicLinkAsync"/> and nowhere else.
    /// </summary>
    public async Task<Result<DocumentDownload>> DownloadContentAsync(
        Guid callerUserId, Guid callerOrgId, Guid id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return Result<DocumentDownload>.Failure(DocumentError.NotFound);
        }

        var share = await _documentShareRepository.GetAsync(document.Id, callerUserId, cancellationToken);
        var access = DocumentAccessEvaluator.Evaluate(document, callerUserId, callerOrgId, hasShareGrant: share is not null);
        if (access == DocumentAccess.None)
        {
            return Result<DocumentDownload>.Failure(DocumentError.ContentForbidden);
        }

        var content = await _documentRepository.GetContentBytesAsync(document.Id, cancellationToken);
        if (content is null)
        {
            // Should not happen -- document and content rows share the same lifetime
            // (documentation/README.md's "Why content is a separate table"); guarded rather than
            // surfacing a null-ref as an unhandled 500, same as ReplaceContentAsync below.
            return Result<DocumentDownload>.Failure(DocumentError.NotFound);
        }

        return Result<DocumentDownload>.Success(new DocumentDownload(document, content));
    }

    /// <summary>
    /// The one anonymous read path in this module (documentation/05-preview-and-download.md's
    /// "Public route" section). Looks the document up by token only, never by id (rule 1), and
    /// asserts Visibility == PublicLink explicitly rather than relying on token nullability alone
    /// (rule 2). Every failure mode -- unknown token, revoked link, deleted document, or a token
    /// whose document has since moved off PublicLink -- collapses into the same
    /// <see cref="DocumentError.PublicLinkNotFound"/> so probing tokens gets no distinguishing
    /// signal (rule 3).
    /// </summary>
    public async Task<Result<DocumentDownload>> DownloadByPublicLinkAsync(string token, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByPublicLinkTokenAsync(token, cancellationToken);
        if (document is null || document.Visibility != Visibility.PublicLink)
        {
            return Result<DocumentDownload>.Failure(DocumentError.PublicLinkNotFound);
        }

        var content = await _documentRepository.GetContentBytesAsync(document.Id, cancellationToken);
        if (content is null)
        {
            return Result<DocumentDownload>.Failure(DocumentError.PublicLinkNotFound);
        }

        return Result<DocumentDownload>.Success(new DocumentDownload(document, content));
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

    /// <summary>
    /// Owner-only. Going PublicLink always (re)generates a fresh token, even if the document is
    /// already PublicLink -- doubles as the only way to rotate a leaked link, since there is no
    /// dedicated rotate endpoint. Going Private or Organization clears the token. Never touches
    /// share grants (documentation/04-sharing-and-visibility.md's setDocumentVisibility section).
    /// </summary>
    public async Task<Result<Document>> SetVisibilityAsync(
        Guid callerUserId, Guid documentId, Visibility visibility, CancellationToken cancellationToken)
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

        var publicLinkToken = visibility == Visibility.PublicLink ? PublicLinkTokenGenerator.Generate() : null;
        document.ChangeVisibility(visibility, publicLinkToken, _timeProvider);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Document>.Success(document);
    }

    /// <summary>
    /// Owner-only wholesale replace of a document's bytes (documentation/06-update-document-content.md).
    /// Only ContentType, SizeBytes, UpdatedAt, and the document_contents row change -- FileName,
    /// FolderId, Visibility, PublicLinkToken, CreatedAt, and share grants are left untouched (the
    /// feature file's "What changes and what does not" table). Load-and-authorize happens before
    /// any body validation, same ordering rule as UpdateAsync (doc 03's "Order of checks matters")
    /// -- a non-owner sending a missing/empty/oversized file still gets 403, not 400.
    /// <paramref name="fileLength"/> covers both "file part present" and "file length > 0" from
    /// the validation table: the endpoint passes 0 when no file part was sent at all, which is
    /// indistinguishable in outcome from an empty part -- both map to the same ValidationFailed.
    /// Both writes (Document metadata and DocumentContent bytes) go through the same unit of work.
    /// </summary>
    public async Task<Result<Document>> ReplaceContentAsync(
        Guid callerUserId,
        Guid documentId,
        string? rawContentType,
        long fileLength,
        byte[] content,
        long maxUploadBytes,
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

        if (fileLength <= 0 || fileLength > maxUploadBytes)
        {
            return Result<Document>.Failure(DocumentError.ValidationFailed);
        }

        var existingContent = await _documentRepository.GetContentAsync(documentId, cancellationToken);
        if (existingContent is null)
        {
            // Should not happen -- the document and its content row are created together and
            // share the same lifetime (documentation/README.md's "Why content is a separate
            // table") -- guarded rather than surfacing a null-ref as an unhandled 500.
            return Result<Document>.Failure(DocumentError.NotFound);
        }

        var contentType = ContentTypeSanitizer.Normalize(rawContentType);

        document.ReplaceContent(contentType, fileLength, _timeProvider);
        existingContent.ReplaceBytes(content);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Document>.Success(document);
    }

    /// <summary>Owner-only -- 403 for everyone else, including the grantees themselves
    /// (documentation/04-sharing-and-visibility.md's listDocumentShares section).</summary>
    public async Task<Result<IReadOnlyList<DocumentShare>>> ListSharesAsync(
        Guid callerUserId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result<IReadOnlyList<DocumentShare>>.Failure(DocumentError.NotFound);
        }

        if (document.OwnerUserId != callerUserId)
        {
            return Result<IReadOnlyList<DocumentShare>>.Failure(DocumentError.NotOwner);
        }

        var shares = await _documentShareRepository.ListByDocumentAsync(documentId, cancellationToken);
        return Result<IReadOnlyList<DocumentShare>>.Success(shares);
    }

    /// <summary>
    /// Owner-only. Self-share is ValidationFailed (400) -- the owner already has access. A second
    /// grant for the same user is ShareAlreadyExists (409), backed by the unique
    /// (document_id, granted_to_user_id) index and the UniqueConstraintViolationException catch
    /// below, not just this method's own check-then-insert -- two concurrent requests would both
    /// pass a check alone. targetUserId is never verified against a users table; this module has
    /// none (documentation/04-sharing-and-visibility.md's shareDocumentWithUser section and Open
    /// questions).
    /// </summary>
    public async Task<Result<DocumentShare>> ShareAsync(
        Guid callerUserId, Guid documentId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result<DocumentShare>.Failure(DocumentError.NotFound);
        }

        if (document.OwnerUserId != callerUserId)
        {
            return Result<DocumentShare>.Failure(DocumentError.NotOwner);
        }

        if (targetUserId == callerUserId)
        {
            return Result<DocumentShare>.Failure(DocumentError.ValidationFailed);
        }

        var share = DocumentShare.Create(documentId, targetUserId, callerUserId, _timeProvider);
        _documentShareRepository.Add(share);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            return Result<DocumentShare>.Failure(DocumentError.ShareAlreadyExists);
        }

        return Result<DocumentShare>.Success(share);
    }

    /// <summary>
    /// Owner-only, idempotent by contract: 204 whether or not the grant existed, unlike
    /// DeleteAsync's 404-on-repeat (documentation/04-sharing-and-visibility.md's
    /// revokeDocumentShare section -- the asymmetry with delete-document is deliberate). The
    /// document itself must still exist and belong to the caller, or this returns NotFound/NotOwner
    /// same as every other owner-only route.
    /// </summary>
    public async Task<Result> RevokeShareAsync(
        Guid callerUserId, Guid documentId, Guid targetUserId, CancellationToken cancellationToken)
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

        var share = await _documentShareRepository.GetAsync(documentId, targetUserId, cancellationToken);
        if (share is not null)
        {
            _documentShareRepository.Remove(share);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
