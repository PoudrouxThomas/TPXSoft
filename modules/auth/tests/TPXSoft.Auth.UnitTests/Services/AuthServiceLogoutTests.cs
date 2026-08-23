using TPXSoft.Auth.Domain.Common;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.UnitTests.Services;

public sealed class AuthServiceLogoutTests
{
    [Fact]
    public async Task LogoutAsync_ActiveToken_RevokesIt()
    {
        var builder = new AuthServiceTestBuilder();
        var user = User.Create("logout@example.com", "irrelevant-hash", Guid.NewGuid(), Role.Member, builder.TimeProvider);
        builder.UserRepository.Seed(user);
        var (token, plainToken) = builder.RefreshTokenFactory.Create(user.Id);
        builder.RefreshTokenRepository.Seed(token);
        var authService = builder.Build();

        await authService.LogoutAsync(plainToken, CancellationToken.None);

        Assert.NotNull(token.RevokedAt);
        Assert.Equal(1, builder.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task LogoutAsync_AlreadyRevokedToken_IsNoOp_DoesNotMoveRevokedAt()
    {
        var builder = new AuthServiceTestBuilder();
        var user = User.Create("logout-idempotent@example.com", "irrelevant-hash", Guid.NewGuid(), Role.Member, builder.TimeProvider);
        builder.UserRepository.Seed(user);
        var (token, plainToken) = builder.RefreshTokenFactory.Create(user.Id);
        token.Revoke(builder.TimeProvider.GetUtcNow());
        var originalRevokedAt = token.RevokedAt;
        builder.RefreshTokenRepository.Seed(token);
        var authService = builder.Build();

        builder.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await authService.LogoutAsync(plainToken, CancellationToken.None);

        Assert.Equal(originalRevokedAt, token.RevokedAt);
    }

    [Fact]
    public async Task LogoutAsync_UnknownToken_SucceedsSilently()
    {
        var builder = new AuthServiceTestBuilder();
        var authService = builder.Build();

        var exception = await Record.ExceptionAsync(() => authService.LogoutAsync("never-issued-token", CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(0, builder.UnitOfWork.SaveChangesCallCount);
    }
}
