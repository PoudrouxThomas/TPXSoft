using System.ComponentModel.DataAnnotations;

namespace TPXSoft.Documents.Infrastructure.Options;

/// <summary>
/// Bound from the "Documents" section. Enforced both as an explicit check in
/// DocumentService/DocumentEndpoints and as the ASP.NET multipart body length limit
/// (documentation/01-upload-document.md's "Streaming vs buffering" section) so oversized bodies
/// are rejected before the handler allocates. Validated at startup (ValidateOnStart).
/// </summary>
public sealed class DocumentsOptions
{
    public const long DefaultMaxUploadBytes = 26_214_400; // 25 MiB

    [Range(1, long.MaxValue)]
    public long MaxUploadBytes { get; set; } = DefaultMaxUploadBytes;
}
