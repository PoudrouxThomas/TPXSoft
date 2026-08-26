using System.Text;

namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Normalizes an uploaded file's name before it is persisted (documentation/01-upload-document.md's
/// "File name sanitization" section). The browser controls this string, so it is treated as
/// attacker input: only the last path segment survives, control characters are stripped, and the
/// result is capped at <see cref="MaxLength"/> characters -- truncated (extension preserved) rather
/// than rejected once something has survived that far.
/// </summary>
public static class FileNameSanitizer
{
    public const int MaxLength = 255;

    // Control character bounds called out explicitly by documentation 01: U+0000-U+001F plus the
    // DEL character U+007F -- these are what break header encoding, since this name is later
    // echoed into a Content-Disposition header by file 05.
    private const char MaxLowControlCharacter = (char)0x1F;
    private const char DeleteCharacter = (char)0x7F;

    /// <summary>
    /// Returns false (with <paramref name="normalized"/> set to empty) only when nothing survives
    /// sanitization -- an empty input, a path made entirely of separators, or a name made entirely
    /// of control characters/whitespace.
    /// </summary>
    public static bool TryNormalize(string rawFileName, out string normalized)
    {
        if (!TrySanitize(rawFileName, out var collapsed))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = collapsed.Length > MaxLength ? TruncatePreservingExtension(collapsed, MaxLength) : collapsed;
        return true;
    }

    /// <summary>
    /// Same sanitization as <see cref="TryNormalize"/> (path-segment stripping, control-character
    /// removal, whitespace collapsing) but rejects rather than truncates when the result exceeds
    /// <see cref="MaxLength"/> characters -- used by rename
    /// (documentation/03-rename-move-delete-document.md's validation table: "truncation is
    /// upload-only"), where silently shortening a name the caller explicitly chose would be a
    /// surprising side effect of a PATCH.
    /// </summary>
    public static bool TryNormalizeStrict(string rawFileName, out string normalized)
    {
        if (!TrySanitize(rawFileName, out var collapsed) || collapsed.Length > MaxLength)
        {
            normalized = string.Empty;
            return false;
        }

        normalized = collapsed;
        return true;
    }

    private static bool TrySanitize(string rawFileName, out string collapsed)
    {
        var lastSeparator = rawFileName.AsSpan().LastIndexOfAny('/', '\\');
        var segment = lastSeparator >= 0 ? rawFileName[(lastSeparator + 1)..] : rawFileName;

        var withoutControlCharacters = StripControlCharacters(segment);
        collapsed = CollapseWhitespace(withoutControlCharacters).Trim();

        return collapsed.Length != 0;
    }

    private static string StripControlCharacters(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c <= MaxLowControlCharacter || c == DeleteCharacter)
            {
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasWhitespace = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasWhitespace)
                {
                    builder.Append(' ');
                }

                lastWasWhitespace = true;
            }
            else
            {
                builder.Append(c);
                lastWasWhitespace = false;
            }
        }

        return builder.ToString();
    }

    private static string TruncatePreservingExtension(string name, int maxLength)
    {
        var dotIndex = name.LastIndexOf('.');

        // No extension, or the dot is the first character (e.g. ".gitignore" treated as a bare
        // name here) -- nothing to preserve, truncate outright.
        if (dotIndex <= 0)
        {
            return name[..maxLength];
        }

        var extension = name[dotIndex..];
        if (extension.Length >= maxLength)
        {
            // Pathological: the extension alone doesn't fit the cap. Fall back to plain
            // truncation rather than producing an empty base name plus a partial extension.
            return name[..maxLength];
        }

        var baseName = name[..dotIndex];
        var allowedBaseLength = maxLength - extension.Length;
        return baseName[..Math.Min(baseName.Length, allowedBaseLength)] + extension;
    }
}
