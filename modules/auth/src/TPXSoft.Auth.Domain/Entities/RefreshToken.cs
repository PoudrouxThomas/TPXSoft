namespace TPXSoft.Auth.Domain.Entities;

public sealed class RefreshToken
{
    // Private parameterless ctor for EF Core materialization only; use Create() elsewhere.
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>SHA-256 hex digest of the plain-text token; the plain token itself is never stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            RevokedAt = null
        };
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>Idempotent: revoking an already-revoked token keeps the original revocation time.</summary>
    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }
}
