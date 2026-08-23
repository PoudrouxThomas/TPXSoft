using Microsoft.Extensions.Options;
using TPXSoft.Auth.Infrastructure.Options;
using TPXSoft.Auth.Infrastructure.Security;

namespace TPXSoft.Auth.UnitTests.Infrastructure;

/// <summary>Exercises the real RefreshTokenFactory (not a fake) -- deterministic hashing is what
/// lets AuthService look a presented refresh token back up by its hash.</summary>
public sealed class RefreshTokenFactoryTests
{
    private static RefreshTokenFactory CreateFactory() =>
        new(TimeProvider.System, Options.Create(new AuthTokenOptions { RefreshTokenLifetimeDays = 7 }));

    [Fact]
    public void HashToken_SamePlainToken_AlwaysHashesToTheSameValue()
    {
        var factory = CreateFactory();

        var first = factory.HashToken("a-fixed-plain-token");
        var second = factory.HashToken("a-fixed-plain-token");

        Assert.Equal(first, second);
    }

    [Fact]
    public void HashToken_TwoDistinctPlainTokens_HashToDistinctValues()
    {
        var factory = CreateFactory();

        var (_, plainA) = factory.Create(Guid.NewGuid());
        var (_, plainB) = factory.Create(Guid.NewGuid());

        Assert.NotEqual(factory.HashToken(plainA), factory.HashToken(plainB));
    }

    [Fact]
    public void Create_TheStoredHash_IsNeverEqualToThePlaintextToken()
    {
        var factory = CreateFactory();

        var (token, plainToken) = factory.Create(Guid.NewGuid());

        Assert.NotEqual(plainToken, token.TokenHash);
    }
}
