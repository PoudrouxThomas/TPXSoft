namespace TPXSoft.Documents.Api.Contracts;

// Request/response shapes mirror contracts/documents.v1.yaml exactly (property names, casing
// handled by System.Text.Json's default camelCase policy).

public sealed record CreateFolderRequest(string Name, Guid? ParentFolderId);

/// <summary>Both properties tri-state: absent means "leave alone", explicit null on
/// ParentFolderId means "move to root" (documentation/README.md's PATCH rule).</summary>
public sealed record UpdateFolderRequest(Patch<string> Name, Patch<Guid?> ParentFolderId);

public sealed record FolderResponse(
    Guid Id, Guid OwnerUserId, Guid? ParentFolderId, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>
/// Mirrors the contract's FolderChildren schema. Documents is always an empty array in this
/// slice -- the Document entity does not exist yet in this module (folders is built first, per
/// documentation/README.md's suggested build order); feature 01 introduces it. The JSON shape
/// for the "documents" property is already correct so nothing here needs to change on the wire
/// once that lands, only the handler that populates it.
/// </summary>
public sealed record FolderChildrenResponse(IReadOnlyList<FolderResponse> Folders, IReadOnlyList<object> Documents);

public sealed record ErrorResponse(string Message);
