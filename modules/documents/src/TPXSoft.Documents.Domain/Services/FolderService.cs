using TPXSoft.Documents.Domain.Abstractions;
using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Domain.Services;

/// <summary>
/// Application-level orchestration for the folder tree (documentation/07-manage-folders.md).
/// Holds the business rules; everything it talks to (the repository, the clock) is a port
/// implemented in Infrastructure.
/// </summary>
public sealed class FolderService
{
    private readonly IFolderRepository _folderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public FolderService(IFolderRepository folderRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _folderRepository = folderRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<Folder>> CreateAsync(
        Guid ownerUserId, string name, Guid? parentFolderId, CancellationToken cancellationToken)
    {
        if (!FolderName.TryNormalize(name, out var normalizedName))
        {
            return Result<Folder>.Failure(DocumentError.ValidationFailed);
        }

        if (parentFolderId is { } parentId)
        {
            // No 403 defined on createFolder -- a parent owned by someone else is reported as
            // 404, same as an unknown parent, leaking nothing (see doc 07).
            var parent = await _folderRepository.GetByIdAsync(parentId, cancellationToken);
            if (parent is null || parent.OwnerUserId != ownerUserId)
            {
                return Result<Folder>.Failure(DocumentError.FolderNotFound);
            }
        }

        var folder = Folder.Create(ownerUserId, normalizedName, parentFolderId, _timeProvider);
        _folderRepository.Add(folder);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Folder>.Success(folder);
    }

    /// <summary>Always succeeds -- listFolders defines no 403/404, an unknown or foreign
    /// parentFolderId simply yields an empty list (see doc 07).</summary>
    public Task<IReadOnlyList<Folder>> ListAsync(Guid ownerUserId, Guid? parentFolderId, CancellationToken cancellationToken) =>
        _folderRepository.ListAsync(ownerUserId, parentFolderId, cancellationToken);

    public async Task<Result<Folder>> GetAsync(Guid ownerUserId, Guid folderId, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(folderId, cancellationToken);
        if (folder is null)
        {
            return Result<Folder>.Failure(DocumentError.FolderNotFound);
        }

        if (folder.OwnerUserId != ownerUserId)
        {
            return Result<Folder>.Failure(DocumentError.FolderForbidden);
        }

        return Result<Folder>.Success(folder);
    }

    /// <summary>Direct subfolders only, one level -- listFolderChildren's own emptiness/ownership
    /// rules are identical to getFolder's.</summary>
    public async Task<Result<IReadOnlyList<Folder>>> GetChildFoldersAsync(
        Guid ownerUserId, Guid folderId, CancellationToken cancellationToken)
    {
        var folderResult = await GetAsync(ownerUserId, folderId, cancellationToken);
        if (folderResult.IsFailure)
        {
            return Result<IReadOnlyList<Folder>>.Failure(folderResult.Error);
        }

        var children = await _folderRepository.ListAsync(ownerUserId, folderId, cancellationToken);
        return Result<IReadOnlyList<Folder>>.Success(children);
    }

    /// <summary>
    /// Tri-state update: <paramref name="nameIsSet"/>/<paramref name="parentFolderIdIsSet"/> tell
    /// apart "absent from the PATCH body" (leave alone) from "present" (apply, even if the
    /// present value is null -- move to root). Deliberately plain bool+value pairs rather than a
    /// shared wrapper type: the JSON-binding Patch{T} struct that produces these lives in
    /// Api/Contracts and Domain must not reference the Api project.
    /// </summary>
    public async Task<Result<Folder>> UpdateAsync(
        Guid ownerUserId,
        Guid folderId,
        bool nameIsSet,
        string? name,
        bool parentFolderIdIsSet,
        Guid? parentFolderId,
        CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(folderId, cancellationToken);
        if (folder is null)
        {
            return Result<Folder>.Failure(DocumentError.FolderNotFound);
        }

        if (folder.OwnerUserId != ownerUserId)
        {
            return Result<Folder>.Failure(DocumentError.FolderForbidden);
        }

        string? normalizedName = null;
        if (nameIsSet)
        {
            if (name is null || !FolderName.TryNormalize(name, out normalizedName))
            {
                return Result<Folder>.Failure(DocumentError.ValidationFailed);
            }
        }

        if (parentFolderIdIsSet && parentFolderId is { } newParentId)
        {
            // Unlike createFolder, updateFolder does define 403 -- so a foreign or unknown
            // parent is distinguished here (see doc 07).
            var newParent = await _folderRepository.GetByIdAsync(newParentId, cancellationToken);
            if (newParent is null)
            {
                return Result<Folder>.Failure(DocumentError.FolderNotFound);
            }

            if (newParent.OwnerUserId != ownerUserId)
            {
                return Result<Folder>.Failure(DocumentError.FolderForbidden);
            }

            var wouldCycle = await FolderCycleCheck.WouldCreateCycleAsync(
                folderId, newParentId, _folderRepository.GetParentIdAsync, cancellationToken);
            if (wouldCycle)
            {
                return Result<Folder>.Failure(DocumentError.CycleDetected);
            }
        }

        if (nameIsSet)
        {
            folder.Rename(normalizedName!, _timeProvider);
        }

        if (parentFolderIdIsSet)
        {
            folder.MoveTo(parentFolderId, _timeProvider);
        }

        if (nameIsSet || parentFolderIdIsSet)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<Folder>.Success(folder);
    }

    public async Task<Result> DeleteAsync(Guid ownerUserId, Guid folderId, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(folderId, cancellationToken);
        if (folder is null)
        {
            return Result.Failure(DocumentError.FolderNotFound);
        }

        if (folder.OwnerUserId != ownerUserId)
        {
            return Result.Failure(DocumentError.FolderForbidden);
        }

        // No documents.folder_id check here: the Document entity does not exist yet in this
        // module (folders is being built first, per documentation/README.md's suggested build
        // order) -- only child folders can make a folder non-empty today. Once Document lands,
        // this needs "OR EXISTS documents WHERE folder_id = @id" too (documentation 07's own
        // emptiness check). The FK-violation catch below is the safety net either way.
        if (await _folderRepository.HasChildFoldersAsync(folderId, cancellationToken))
        {
            return Result.Failure(DocumentError.FolderNotEmpty);
        }

        _folderRepository.Remove(folder);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ForeignKeyConstraintViolationException)
        {
            // A race lost to the emptiness check above (something was created under this folder
            // between the check and the delete) -- the database's own ON DELETE RESTRICT
            // constraint is the source of truth regardless of the service-level check.
            return Result.Failure(DocumentError.FolderNotEmpty);
        }

        return Result.Success();
    }
}
