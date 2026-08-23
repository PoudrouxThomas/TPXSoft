using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.Domain.Abstractions;

public interface IRefreshTokenFactory
{
    /// <summary>
    /// Creates a new refresh token for <paramref name="userId"/>. The returned entity holds only
    /// the hash (for persistence); <c>PlainToken</c> is the one-time value to hand back to the
    /// caller and is never stored.
    /// </summary>
    (RefreshToken Token, string PlainToken) Create(Guid userId);

    /// <summary>Hashes a plain-text token the same way <see cref="Create"/> does, for lookup by hash.</summary>
    string HashToken(string plainToken);
}
