using TPXSoft.Auth.Infrastructure.Security;

namespace TPXSoft.Auth.UnitTests.Infrastructure;

/// <summary>
/// Exercises the real PBKDF2-based hasher (not a fake). Deliberately kept to a couple of tests --
/// slow-ish by design, so AuthService's own unit tests use FakePasswordHasher instead.
/// </summary>
public sealed class IdentityPasswordHasherTests
{
    [Fact]
    public void Hash_ProducesAValueDifferentFromThePlaintextPassword()
    {
        var hasher = new IdentityPasswordHasher();

        var hash = hasher.Hash("correct-horse-battery-staple");

        Assert.NotEqual("correct-horse-battery-staple", hash);
        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void Verify_IsTrueForTheCorrectPassword_AndFalseForAWrongOne()
    {
        var hasher = new IdentityPasswordHasher();
        var hash = hasher.Hash("correct-horse-battery-staple");

        Assert.True(hasher.Verify(hash, "correct-horse-battery-staple"));
        Assert.False(hasher.Verify(hash, "wrong-password"));
    }
}
