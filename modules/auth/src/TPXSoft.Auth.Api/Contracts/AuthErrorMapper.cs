using TPXSoft.Auth.Domain.Common;

namespace TPXSoft.Auth.Api.Contracts;

internal static class AuthErrorMapper
{
    public static (int StatusCode, string Message) ToHttp(this AuthError error) => error switch
    {
        AuthError.EmailAlreadyRegistered => (StatusCodes.Status409Conflict, "Email already registered."),
        AuthError.InvalidCredentials => (StatusCodes.Status401Unauthorized, "Invalid email or password."),
        AuthError.InvalidRefreshToken => (StatusCodes.Status401Unauthorized, "Refresh token invalid, expired, or already revoked."),
        AuthError.ValidationFailed => (StatusCodes.Status400BadRequest, "Validation failed."),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Unmapped AuthError.")
    };
}
