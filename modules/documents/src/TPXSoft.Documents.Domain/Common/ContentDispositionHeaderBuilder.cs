using System.Text;

namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Builds the Content-Disposition header value for both content-download routes
/// (documentation/05-preview-and-download.md's "Filename encoding" section). v1 always emits
/// `attachment` -- there is no inline preview yet, see that file's Open questions -- with both a
/// legacy ASCII-folded `filename=` fallback (quotes and backslashes escaped) and a percent-encoded
/// `filename*=UTF-8''…` form (RFC 5987) so non-ASCII names survive for clients that honor it.
/// FileNameSanitizer already strips control characters at upload time, but this re-checks rather
/// than trusts it, because rows written before that sanitization existed would still be sitting in
/// the database -- a raw CR or LF surviving into this header would be a response-splitting bug.
/// </summary>
public static class ContentDispositionHeaderBuilder
{
    private const string FallbackFileName = "download";

    public static string BuildAttachment(string fileName)
    {
        var safeName = StripLineBreakingCharacters(fileName);
        if (safeName.Length == 0)
        {
            safeName = FallbackFileName;
        }

        var quotedAsciiFallback = EscapeQuotedString(ToAsciiFallback(safeName));
        var percentEncoded = Uri.EscapeDataString(safeName);

        return $"attachment; filename=\"{quotedAsciiFallback}\"; filename*=UTF-8''{percentEncoded}";
    }

    /// <summary>Removes CR, LF, and every other C0/DEL control character -- the same bound
    /// FileNameSanitizer enforces on upload, re-applied here rather than trusted, since this value
    /// is about to be written directly into a response header.</summary>
    private static string StripLineBreakingCharacters(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c <= 0x1F || c == 0x7F)
            {
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>Folds every character outside printable ASCII down to '_' for the legacy
    /// `filename=` parameter -- clients that ignore `filename*=` still get a stable, safe name
    /// instead of mangled or truncated bytes.</summary>
    private static string ToAsciiFallback(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(c is >= (char)0x20 and <= (char)0x7E ? c : '_');
        }

        return builder.ToString();
    }

    private static string EscapeQuotedString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
