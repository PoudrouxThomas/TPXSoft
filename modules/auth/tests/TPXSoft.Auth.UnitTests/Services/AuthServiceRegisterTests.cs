using TPXSoft.Auth.Domain.Common;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.UnitTests.Services;

public sealed class AuthServiceRegisterTests
{
    [Fact]
    public async Task RegisterAsync_CreatesOrgAndAdminUser_ReturnsNonEmptyTokenPair()
    {
        var builder = new AuthServiceTestBuilder();
        var authService = builder.Build();

        var result = await authService.RegisterAsync("new-user@example.com", "correct-horse", "Acme Inc", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));

        var org = Assert.Single(builder.OrgRepository.Added);
        Assert.Equal("Acme Inc", org.Name);

        var user = Assert.Single(builder.UserRepository.Added);
        Assert.Equal("new-user@example.com", user.Email);
        Assert.Equal(org.Id, user.OrgId);
        Assert.Equal(Role.Admin, user.Role);

        Assert.Equal(1, builder.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task RegisterAsync_PersistsRefreshTokenHashed_NeverPlaintext()
    {
        var builder = new AuthServiceTestBuilder();
        var authService = builder.Build();

        var result = await authService.RegisterAsync("hash-check@example.com", "correct-horse", "Acme Inc", CancellationToken.None);

        var plainRefreshToken = result.Value.RefreshToken;
        var storedToken = Assert.Single(builder.RefreshTokenRepository.Added);

        Assert.NotEqual(plainRefreshToken, storedToken.TokenHash);
        Assert.Equal(builder.RefreshTokenFactory.HashToken(plainRefreshToken), storedToken.TokenHash);
    }

    [Theory]
    [InlineData("bob@x.com", " Bob@X.com ")]
    [InlineData("bob@x.com", "BOB@X.COM")]
    public async Task RegisterAsync_DuplicateEmail_CaseOrWhitespaceVariant_RejectedWithoutCreatingAnything(
        string existingNormalizedEmail, string attemptedEmail)
    {
        var builder = new AuthServiceTestBuilder();
        builder.UserRepository.Seed(
            User.Create(existingNormalizedEmail, "irrelevant-hash", Guid.NewGuid(), Role.Admin, builder.TimeProvider));
        var authService = builder.Build();

        var result = await authService.RegisterAsync(attemptedEmail, "correct-horse", "Acme Inc", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthError.EmailAlreadyRegistered, result.Error);
        Assert.Empty(builder.OrgRepository.Added);
        Assert.Empty(builder.UserRepository.Added);
        Assert.Empty(builder.RefreshTokenRepository.Added);
        Assert.Equal(0, builder.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task RegisterAsync_PasswordTooShort_ValidationFailed_NothingPersisted()
    {
        var builder = new AuthServiceTestBuilder();
        var authService = builder.Build();

        var result = await authService.RegisterAsync("short-pw@example.com", "short1", "Acme Inc", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthError.ValidationFailed, result.Error);
        Assert.Empty(builder.OrgRepository.Added);
        Assert.Empty(builder.UserRepository.Added);
        Assert.Empty(builder.RefreshTokenRepository.Added);
        Assert.Equal(0, builder.UnitOfWork.SaveChangesCallCount);
    }
}
