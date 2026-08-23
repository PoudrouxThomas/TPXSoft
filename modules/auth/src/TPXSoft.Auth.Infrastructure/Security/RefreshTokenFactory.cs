using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Entities;
using TPXSoft.Auth.Infrastructure.Options;

namespace TPXSoft.Auth.Infrastructure.Security;

public sealed class RefreshTokenFactory : IRefreshTokenFactory
{
    private readonly TimeProvider _timeProvider;
    private readonly AuthTokenOptions _options;

    public RefreshTokenFactory(TimeProvider timeProvider, IOptions<AuthTokenOptions> options)
    {
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public (RefreshToken Token, string PlainToken) Create(Guid userId)
    {
        // 256 bits of CSPRNG output -- this is the bearer credential handed to the caller.
        var plainToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(plainToken);
        var expiresAt = _timeProvider.GetUtcNow().AddDays(_options.RefreshTokenLifetimeDays);

        return (RefreshToken.Create(userId, tokenHash, expiresAt), plainToken);
    }

    // Deliberately unsalted/deterministic SHA-256. The plain token is already 256 bits of CSPRNG
    // output, so it cannot be brute-forced or dictionary-attacked from its hash, and refresh/
    // logout need to look a token up by hash -- a salted hash can't be looked up without an
    // unindexed table scan. Do not "fix" this into a salted hash later.
    public string HashToken(string plainToken)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexStringLower(digest);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
