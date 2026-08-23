namespace TPXSoft.Auth.Domain.Common;

/// <summary>
/// Email is kept as a plain string on <see cref="Entities.User"/> rather than a full value-object
/// wrapper, to avoid an EF value-converter-backed unique index. This helper centralizes the
/// normalization/validation rules so callers don't reimplement them ad hoc.
/// </summary>
public static class Email
{
    /// <summary>
    /// Normalizes an email to its canonical, comparable form: trimmed and lower-invariant.
    /// Always normalize before persisting, querying, or comparing emails.
    /// </summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// A deliberately simple structural check (single '@', a '.' in the domain part, no
    /// whitespace) -- not a full RFC 5322 validator. Good enough to reject obviously malformed
    /// input; real deliverability is a signup/verification-email concern, not this module's.
    /// </summary>
    public static bool IsValid(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        if (email.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        var atIndex = email.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0 || atIndex != email.LastIndexOf('@'))
        {
            return false;
        }

        var domain = email[(atIndex + 1)..];
        if (domain.Length == 0)
        {
            return false;
        }

        var dotIndex = domain.IndexOf('.', StringComparison.Ordinal);
        return dotIndex > 0 && dotIndex < domain.Length - 1;
    }
}
