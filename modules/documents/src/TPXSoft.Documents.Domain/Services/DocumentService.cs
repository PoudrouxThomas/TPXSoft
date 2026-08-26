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
}
