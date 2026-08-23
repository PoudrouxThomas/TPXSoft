using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.UnitTests.TestDoubles;

internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _tokens = new();

    /// <summary>Only the tokens added via <see cref="Add"/> during the test -- excludes seeded ones.</summary>
    public List<RefreshToken> Added { get; } = new();

    /// <summary>Pre-populates the repository as if this token already existed before the test ran.</summary>
    public void Seed(RefreshToken token) => _tokens.Add(token);

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_tokens.SingleOrDefault(t => t.TokenHash == tokenHash));

    public void Add(RefreshToken refreshToken)
    {
        Added.Add(refreshToken);
        _tokens.Add(refreshToken);
    }
}
