using TPXSoft.Documents.Domain.Common;

namespace TPXSoft.Documents.Api.Contracts;

/// <summary>
/// Maps DocumentError to HTTP in exactly one place (documentation/README.md's "Result and error
/// mapping" section). Extended by later feature files as they add error cases; this module's
/// first slice (folders) covers only what documentation/07-manage-folders.md needs.
/// </summary>
internal static class DocumentErrorMapper
{
    public static (int StatusCode, string Message) ToHttp(this DocumentError error) => error switch
    {
        DocumentError.ValidationFailed => (StatusCodes.Status400BadRequest, "Validation failed."),
        DocumentError.CycleDetected => (StatusCodes.Status400BadRequest, "A folder cannot be moved into its own descendant."),
        DocumentError.FolderForbidden => (StatusCodes.Status403Forbidden, "Caller is not the owner."),
        DocumentError.FolderNotFound => (StatusCodes.Status404NotFound, "No folder with this id."),
        DocumentError.FolderNotEmpty => (StatusCodes.Status409Conflict, "Folder is not empty."),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Unmapped DocumentError.")
    };
}
