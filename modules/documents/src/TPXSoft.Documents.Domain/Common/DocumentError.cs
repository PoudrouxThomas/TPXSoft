namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Failure reasons Documents domain services can produce. Starting set per
/// modules/documents/documentation/README.md, extended by later feature files as they land.
/// Kept in Documents.Domain for now rather than shared/TPXSoft.Shared.Kernel -- that project gets
/// created when a second module actually needs to share this kind of type, not speculatively
/// (same reasoning already applied to AuthError).
/// </summary>
public enum DocumentError
{
    ValidationFailed,

    /// <summary>No folder with the given id.</summary>
    FolderNotFound,

    /// <summary>Folder exists but the caller is not its owner.</summary>
    FolderForbidden,

    /// <summary>Folder has at least one direct child folder or document.</summary>
    FolderNotEmpty,

    /// <summary>Moving a folder into itself or one of its own descendants.</summary>
    CycleDetected
}
