namespace TPXSoft.Auth.Domain.Common;

/// <summary>
/// Failure reasons AuthService can produce. Kept in Auth.Domain for now rather than
/// shared/TPXSoft.Shared.Kernel -- that project gets created when a second module actually
/// needs to share this kind of type, not speculatively.
/// </summary>
public enum AuthError
{
    EmailAlreadyRegistered,
    InvalidCredentials,
    InvalidRefreshToken,
    ValidationFailed
}
