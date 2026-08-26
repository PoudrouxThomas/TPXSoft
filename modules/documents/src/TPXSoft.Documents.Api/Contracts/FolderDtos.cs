namespace TPXSoft.Documents.Api.Contracts;

// Request/response shapes mirror contracts/documents.v1.yaml exactly (property names, casing
// handled by System.Text.Json's default camelCase policy).

public sealed record CreateFolderRequest(string Name, Guid? ParentFolderId);

/// <summary>Both properties tri-state: absent means "leave alone", explicit null on
/// ParentFolderId means "move to root" (documentation/README.md's PATCH rule).</summary>
public sealed record UpdateFolderRequest(Patch<string> Name, Patch<Guid?> ParentFolderId);

public sealed record FolderResponse(
    Guid Id, Guid OwnerUserId, Guid? ParentFolderId, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>Mirrors the contract's FolderChildren schema: the folder's direct child folders and
/// direct child documents, one level each, never recursive.</summary>
public sealed record FolderChildrenResponse(IReadOnlyList<FolderResponse> Folders, IReadOnlyList<DocumentResponse> Documents);

public sealed record ErrorResponse(string Message);
