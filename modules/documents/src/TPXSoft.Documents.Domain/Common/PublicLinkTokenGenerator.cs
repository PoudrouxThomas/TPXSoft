using System.Security.Cryptography;

namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Generates the raw token behind Visibility.PublicLink
/// (documentation/04-sharing-and-visibility.md's "Token generation and storage" section). 32 bytes
/// of CSPRNG output, base64url-encoded without padding -- 43 characters, 256 bits of entropy. Same
/// generator family as Auth's refresh tokens (TPXSoft.Auth.Infrastructure.Security
/// .RefreshTokenFactory), but stored raw here rather than hashed: Document.PublicLinkToken is
/// returned to the owner on every subsequent read so the UI can display the link, and a hash could
/// not be shown again. Safe for the same reason Auth's refresh token hash is safe to look up
/// deterministically -- the value is CSPRNG output, not a guessable secret.
/// </summary>
public static class PublicLinkTokenGenerator
{
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
