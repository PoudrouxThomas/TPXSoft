using TPXSoft.Auth.Domain.Common;
using TPXSoft.Auth.Domain.Entities;

namespace TPXSoft.Auth.UnitTests.Services;

public sealed class AuthServiceLoginTests
{
    [Fact]
    public async Task LoginAsync_CorrectPassword_Succeeds()
    {
        var builder = new AuthServiceTestBuilder();
        var passwordHash = builder.PasswordHasher.Hash("correct-horse");
        builder.UserRepository.Seed(
            User.Create("login@example.com", passwordHash, Guid.NewGuid(), Role.Member, builder.TimeProvider));
        var authService = builder.Build();

        var result = await authService.LoginAsync("login@example.com", "correct-horse", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        var builder = new AuthServiceTestBuilder();
        var passwordHash = builder.PasswordHasher.Hash("correct-horse");
        builder.UserRepository.Seed(
            User.Create("wrong-pw@example.com", passwordHash, Guid.NewGuid(), Role.Member, builder.TimeProvider));
        var authService = builder.Build();

        var result = await authService.LoginAsync("wrong-pw@example.com", "totally-wrong", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthError.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsInvalidCredentials()
    {
        var builder = new AuthServiceTestBuilder();
        var authService = builder.Build();

        var result = await authService.LoginAsync("nobody@example.com", "whatever-password", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthError.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task LoginAsync_WrongPasswordAndUnknownEmail_ReturnTheIdenticalError_NoUserEnumeration()
    {
        var builder = new AuthServiceTestBuilder();
        var passwordHash = builder.PasswordHasher.Hash("correct-horse");
        builder.UserRepository.Seed(
            User.Create("known@example.com", passwordHash, Guid.NewGuid(), Role.Member, builder.TimeProvider));
        var authService = builder.Build();

        var wrongPasswordResult = await authService.LoginAsync("known@example.com", "not-the-password", CancellationToken.None);
        var unknownEmailResult = await authService.LoginAsync("nobody@example.com", "not-the-password", CancellationToken.None);

        Assert.True(wrongPasswordResult.IsFailure);
        Assert.True(unknownEmailResult.IsFailure);
        Assert.Equal(AuthError.InvalidCredentials, wrongPasswordResult.Error);
        Assert.Equal(wrongPasswordResult.Error, unknownEmailResult.Error);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_StillRunsADummyPasswordVerify_ForTimingParity()
    {
        var builder = new AuthServiceTestBuilder();
        var authService = builder.Build();

        await authService.LoginAsync("nobody@example.com", "whatever-password", CancellationToken.None);

        var call = Assert.Single(builder.PasswordHasher.VerifyCalls);
        Assert.Equal("whatever-password", call.Password);
        Assert.Equal(builder.PasswordHasher.Hash("dummy-password-for-timing-parity"), call.Hash);
    }
}
