using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Api.Contracts;

// Mirrors the contract's Document schema exactly (property names, casing handled by
// System.Text.Json's default camelCase policy; Visibility serializes as a string via the
// JsonStringEnumConverter registered in Program.cs).

/// <param name="PublicLinkToken">Must be null for every caller except the document's owner, even
/// though every caller gets the same schema -- documentation/02-virtual-folders.md. Callers build
/// this record with PublicLinkToken already nulled out rather than relying on a serializer-level
/// trick, so the rule is visible at the call site.</param>
/// <summary>Both properties tri-state: absent means "leave alone", explicit null on FolderId
/// means "move to root" (documentation/README.md's PATCH rule, documentation/03's tri-state
/// table). Content and visibility are not here -- they are updated via their own endpoints
/// (documentation 04/06) and any such fields in the payload are simply ignored.</summary>
public sealed record UpdateDocumentRequest(Patch<string> FileName, Patch<Guid?> FolderId);

public sealed record DocumentResponse(
    Guid Id,
    Guid OwnerUserId,
    Guid? FolderId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Visibility Visibility,
    string? PublicLinkToken,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
