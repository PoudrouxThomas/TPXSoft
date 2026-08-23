namespace TPXSoft.Auth.Domain.Common;

/// <summary>
/// Password acceptance rules enforced at registration. Deliberately minimal for Phase 1 --
/// no complexity/character-class rules, just a length floor.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static bool IsValid(string password) => !string.IsNullOrEmpty(password) && password.Length >= MinimumLength;
}
