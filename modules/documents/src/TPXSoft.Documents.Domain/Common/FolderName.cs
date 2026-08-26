namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Validation/normalization rules for a folder's display name, shared by create and rename
/// (documentation/07-manage-folders.md). Folder names are display strings, not path segments --
/// ".." and "/" are legal and need no stripping. Control characters are still rejected.
/// </summary>
public static class FolderName
{
    public const int MaxLength = 255;

    /// <summary>
    /// Trims the raw input and validates it: non-empty after trimming, at most
    /// <see cref="MaxLength"/> characters, no control characters. Returns false (with
    /// <paramref name="normalized"/> set to empty) if any rule fails.
    /// </summary>
    public static bool TryNormalize(string rawName, out string normalized)
    {
        var trimmed = rawName.Trim();

        if (trimmed.Length == 0 || trimmed.Length > MaxLength || ContainsControlCharacter(trimmed))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = trimmed;
        return true;
    }

    private static bool ContainsControlCharacter(string value)
    {
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }
}
