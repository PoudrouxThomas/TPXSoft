using System.Text.RegularExpressions;

namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Normalizes an uploaded file's declared content type before it is persisted (documentation/
/// 01-upload-document.md's "Content type" section). This is metadata the uploader chose and is
/// never trusted for anything but round-tripping -- no sniffing happens here, and the defense
/// against a malicious declared type happens at download time (file 05), not on upload.
/// </summary>
public static partial class ContentTypeSanitizer
{
    public const string Fallback = "application/octet-stream";

    public const int MaxLength = 128;

    /// <summary>
    /// Returns <paramref name="rawContentType"/> unchanged when it is a syntactically valid media
    /// type (RFC 9110 token rules, "type/subtype" with optional ";param=value" parameters) that
    /// fits within <see cref="MaxLength"/>; otherwise falls back to
    /// <see cref="Fallback"/> rather than storing something that cannot round-trip cleanly.
    /// </summary>
    public static string Normalize(string? rawContentType)
    {
        if (string.IsNullOrWhiteSpace(rawContentType))
        {
            return Fallback;
        }

        var trimmed = rawContentType.Trim();
        if (trimmed.Length > MaxLength || !MediaTypeRegex().IsMatch(trimmed))
        {
            return Fallback;
        }

        return trimmed;
    }

    // RFC 9110 token characters: alphanumerics plus !#$%&'*+-.^_`|~. A media type is
    // token "/" token, followed by zero or more ";" token "=" (token / quoted-string) parameters.
    [GeneratedRegex(
        """^[!#$%&'*+\-.^_`|~0-9A-Za-z]+/[!#$%&'*+\-.^_`|~0-9A-Za-z]+(\s*;\s*[!#$%&'*+\-.^_`|~0-9A-Za-z]+=([!#$%&'*+\-.^_`|~0-9A-Za-z]+|"[^"]*"))*$""")]
    private static partial Regex MediaTypeRegex();
}
