namespace TPXSoft.Documents.Domain.Entities;

public sealed class Document
{
    // Private parameterless ctor for EF Core materialization only; use Create() elsewhere.
    private Document()
    {
    }

    public Guid Id { get; private set; }

    public Guid OwnerUserId { get; private set; }

    /// <summary>Copied off the "orgId" claim at upload time -- this module has no users table to
    /// join back to the owner, see documentation/README.md's "Why OrgId is denormalized"
    /// section.</summary>
    public Guid OrgId { get; private set; }

    /// <summary>Null means the owner's root, same convention as Folder.ParentFolderId. Not
    /// validated for ownership here -- Document does not know about Folder; the service layer
    /// resolves that.</summary>
    public Guid? FolderId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public Visibility Visibility { get; private set; }

    /// <summary>Set only while Visibility is PublicLink; null otherwise. Serialized to the wire
    /// only for the owner (documentation/02-virtual-folders.md).</summary>
    public string? PublicLinkToken { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Minimal factory sufficient for feature 02's read paths and test seeding --
    /// upload-specific validation (file name sanitization, size caps) belongs to feature 01.
    /// </summary>
    public static Document Create(
        Guid ownerUserId,
        Guid orgId,
        Guid? folderId,
        string fileName,
        string contentType,
        long sizeBytes,
        Visibility visibility,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();

        return new Document
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            OrgId = orgId,
            FolderId = folderId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Visibility = visibility,
            PublicLinkToken = null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <param name="fileName">Must already be validated/normalized by the caller (see
    /// Domain.Common.FileNameSanitizer.TryNormalizeStrict -- rename does not truncate, unlike
    /// upload).</param>
    public void Rename(string fileName, TimeProvider timeProvider)
    {
        FileName = fileName;
        UpdatedAt = timeProvider.GetUtcNow();
    }

    /// <param name="folderId">Null moves the document to the owner's root. Caller is responsible
    /// for ownership checks before calling this -- Document does not know about Folder, same as
    /// Create.</param>
    public void MoveTo(Guid? folderId, TimeProvider timeProvider)
    {
        FolderId = folderId;
        UpdatedAt = timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Visibility and DocumentShare grants are independent axes -- this never touches grants
    /// (documentation/04-sharing-and-visibility.md's "Two independent axes" section).
    /// </summary>
    /// <param name="publicLinkToken">Must already be a freshly generated token (see
    /// Domain.Common.PublicLinkTokenGenerator) when <paramref name="visibility"/> is PublicLink,
    /// and null otherwise -- this method performs the state transition only, it does not generate
    /// the token itself, same as Rename/MoveTo taking already-normalized input.</param>
    public void ChangeVisibility(Visibility visibility, string? publicLinkToken, TimeProvider timeProvider)
    {
        Visibility = visibility;
        PublicLinkToken = publicLinkToken;
        UpdatedAt = timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Wholesale replace of the document's bytes (documentation/06-update-document-content.md).
    /// Only ContentType, SizeBytes, and UpdatedAt change here; FileName, FolderId, Visibility,
    /// PublicLinkToken, and CreatedAt are deliberately untouched -- see the feature file's "What
    /// changes and what does not" table. The actual bytes live in DocumentContent and are updated
    /// separately by the caller in the same unit of work.
    /// </summary>
    /// <param name="contentType">Must already be sanitized by the caller (see
    /// Domain.Common.ContentTypeSanitizer.Normalize), same convention as Rename's fileName.</param>
    /// <param name="sizeBytes">The actual byte count written, not a client-supplied header.</param>
    public void ReplaceContent(string contentType, long sizeBytes, TimeProvider timeProvider)
    {
        ContentType = contentType;
        SizeBytes = sizeBytes;
        UpdatedAt = timeProvider.GetUtcNow();
    }
}
