using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.UnitTests.TestDoubles;

/// <summary>
/// Deterministic stand-in for <see cref="IRefreshTokenFactory"/>: plain tokens are just
/// incrementing strings, and <see cref="HashToken"/> is a pure, reversible-in-spirit mapping so
/// AuthService's "look the token back up by hash" flow works the same way the real SHA-256
/// implementation does, without needing real hashing in a unit test.
/// </summary>
internal sealed class FakeRefreshTokenFactory : IRefreshTokenFactory
{
    private readonly TimeProvider _timeProvider;
    private int _counter;

    public FakeRefreshTokenFactory(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>How far past "now" a newly created token's ExpiresAt is set. Defaults to a
    /// generous window so most tests don't need to think about expiry at all.</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(7);

    public List<(Guid UserId, string PlainToken)> Created { get; } = new();

    public (RefreshToken Token, string PlainToken) Create(Guid userId)
    {
        var plainToken = $"plain-token-{++_counter}";
        Created.Add((userId, plainToken));

        var expiresAt = _timeProvider.GetUtcNow() + Lifetime;
        return (RefreshToken.Create(userId, HashToken(plainToken), expiresAt), plainToken);
    }

    public string HashToken(string plainToken) => $"hash::{plainToken}";
}
