using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Result of a successful content download (documentation/05-preview-and-download.md) -- the
/// document's metadata (for ContentType/FileName) alongside the raw bytes loaded via a no-tracking
/// projection straight from document_contents. Never serialized to JSON directly; the endpoint
/// reads Document.ContentType/FileName for headers and writes Content as the raw response body.
/// </summary>
public sealed record DocumentDownload(Document Document, byte[] Content);
