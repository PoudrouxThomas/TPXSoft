using Microsoft.Extensions.Time.Testing;
using TPXSoft.Auth.Domain.Services;
using TPXSoft.Auth.UnitTests.TestDoubles;

namespace TPXSoft.Auth.UnitTests;

/// <summary>
/// Builds an <see cref="AuthService"/> wired to fully in-memory fakes for every port it depends
/// on, so each test only names what it varies (a seeded user, an advanced clock, a shorter token
/// lifetime, ...) instead of repeating the constructor wiring.
/// </summary>
internal sealed class AuthServiceTestBuilder
{
    public FakeOrgRepository OrgRepository { get; } = new();

    public FakeUserRepository UserRepository { get; } = new();

    public FakeRefreshTokenRepository RefreshTokenRepository { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public FakePasswordHasher PasswordHasher { get; } = new();

    public FakeAccessTokenIssuer AccessTokenIssuer { get; } = new();

    public FakeTimeProvider TimeProvider { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public FakeRefreshTokenFactory RefreshTokenFactory { get; }

    public AuthServiceTestBuilder()
    {
        RefreshTokenFactory = new FakeRefreshTokenFactory(TimeProvider);
    }

    public AuthService Build() => new(
        OrgRepository,
        UserRepository,
        RefreshTokenRepository,
        UnitOfWork,
        PasswordHasher,
        RefreshTokenFactory,
        AccessTokenIssuer,
        TimeProvider);
}
