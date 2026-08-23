using TPXSoft.Auth.Domain.Common;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.UnitTests.Services;

public sealed class AuthServiceRefreshTests
{
    [Fact]
    public async Task RefreshAsync_ValidToken_IssuesNewDistinctPair_AndRevokesThePresentedToken()
    {
        var builder = new AuthServiceTestBuilder();
        var user = User.Create("refresh@example.com", "irrelevant-hash", Guid.NewGuid(), Role.Member, builder.TimeProvider);
        builder.UserRepository.Seed(user);
        var (existingToken, plainToken) = builder.RefreshTokenFactory.Create(user.Id);
        builder.RefreshTokenRepository.Seed(existingToken);
        var authService = builder.Build();

        var result = await authService.RefreshAsync(plainToken, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(plainToken, result.Value.RefreshToken);
        Assert.NotNull(existingToken.RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_Rejected()
    {
        var builder = new AuthServiceTestBuilder();
        var user = User.Create("expired@example.com", "irrelevant-hash", Guid.NewGuid(), Role.Member, builder.TimeProvider);
        builder.UserRepository.Seed(user);
        builder.RefreshTokenFactory.Lifetime = TimeSpan.FromMinutes(5);
        var (existingToken, plainToken) = builder.RefreshTokenFactory.Create(user.Id);
        builder.RefreshTokenRepository.Seed(existingToken);
        builder.TimeProvider.Advance(TimeSpan.FromMinutes(10)); // past ExpiresAt
        var authService = builder.Build();

        var result = await authService.RefreshAsync(plainToken, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthError.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task RefreshAsync_AlreadyRevokedToken_Rejected()
    {
        var builder = new AuthServiceTestBuilder();
        var user = User.Create("revoked@example.com", "irrelevant-hash", Guid.NewGuid(), Role.Member, builder.TimeProvider);
        builder.UserRepository.Seed(user);
        var (existingToken, plainToken) = builder.RefreshTokenFactory.Create(user.Id);
        existingToken.Revoke(builder.TimeProvider.GetUtcNow());
        builder.RefreshTokenRepository.Seed(existingToken);
        var authService = builder.Build();

        var result = await authService.RefreshAsync(plainToken, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthError.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task RefreshAsync_UnknownToken_Rejected()
    {
        var builder = new AuthServiceTestBuilder();
        var authService = builder.Build();

        var result = await authService.RefreshAsync("garbage-token-that-was-never-issued", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthError.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task RefreshAsync_RotatedAwayToken_CannotBeUsedASecondTime()
    {
        var builder = new AuthServiceTestBuilder();
        var user = User.Create("rotate@example.com", "irrelevant-hash", Guid.NewGuid(), Role.Member, builder.TimeProvider);
        builder.UserRepository.Seed(user);
        var (existingToken, plainToken) = builder.RefreshTokenFactory.Create(user.Id);
        builder.RefreshTokenRepository.Seed(existingToken);
        var authService = builder.Build();

        var firstResult = await authService.RefreshAsync(plainToken, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        var secondResult = await authService.RefreshAsync(plainToken, CancellationToken.None);

        Assert.True(secondResult.IsFailure);
        Assert.Equal(AuthError.InvalidRefreshToken, secondResult.Error);
    }
}
