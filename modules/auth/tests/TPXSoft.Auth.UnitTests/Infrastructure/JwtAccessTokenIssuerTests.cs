using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TPXSoft.Auth.Domain.Common;
using TPXSoft.Auth.Domain.Entities;
using TPXSoft.Auth.Infrastructure.Options;
using TPXSoft.Auth.Infrastructure.Security;

namespace TPXSoft.Auth.UnitTests.Infrastructure;

/// <summary>Exercises the real JwtAccessTokenIssuer (not a fake) -- these tests are what make
/// AuthService's fake-hasher/fake-issuer unit tests trustworthy.</summary>
public sealed class JwtAccessTokenIssuerTests
{
    private static JwtOptions CreateOptions(int lifetimeMinutes = 15, string? signingKey = null) => new()
    {
        Issuer = "tpxsoft-auth-tests",
        Audience = "tpxsoft-auth-tests-aud",
        SigningKey = signingKey ?? new string('s', 32),
        AccessTokenLifetimeMinutes = lifetimeMinutes
    };

    private static User CreateUser(Role role = Role.Admin) =>
        User.Create("issuer-test@example.com", "irrelevant-hash", Guid.NewGuid(), role, TimeProvider.System);

    private static TokenValidationParameters CreateValidationParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        ClockSkew = TimeSpan.Zero
    };

    [Fact]
    public void Issue_EncodesUserIdentityClaims()
    {
        var options = CreateOptions();
        var issuer = new JwtAccessTokenIssuer(TimeProvider.System, Options.Create(options));
        var user = CreateUser(Role.Member);

        var token = issuer.Issue(user);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(user.Email, jwt.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal(user.OrgId.ToString(), jwt.Claims.Single(c => c.Type == "orgId").Value);
        Assert.Equal("Member", jwt.Claims.Single(c => c.Type == "role").Value);
    }

    [Fact]
    public void Issue_ExpiryEqualsFakeNowPlusConfiguredLifetime()
    {
        var fakeNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fakeNow);
        var options = CreateOptions(lifetimeMinutes: 15);
        var issuer = new JwtAccessTokenIssuer(timeProvider, Options.Create(options));

        var token = issuer.Issue(CreateUser());
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Equal(fakeNow.UtcDateTime.AddMinutes(15), jwt.ValidTo);
    }

    [Fact]
    public async Task Issue_TokenValidatesAgainstMatchingTokenValidationParameters()
    {
        var options = CreateOptions();
        var issuer = new JwtAccessTokenIssuer(TimeProvider.System, Options.Create(options));

        var token = issuer.Issue(CreateUser());
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, CreateValidationParameters(options));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Issue_TokenFailsValidation_WhenSignedWithADifferentKey()
    {
        var options = CreateOptions();
        var issuer = new JwtAccessTokenIssuer(TimeProvider.System, Options.Create(options));
        var token = issuer.Issue(CreateUser());

        var wrongKeyOptions = CreateOptions(signingKey: new string('x', 32));
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, CreateValidationParameters(wrongKeyOptions));

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenSignatureKeyNotFoundException>(result.Exception);
    }

    [Fact]
    public async Task Issue_ExpiredToken_FailsValidation_UnderZeroClockSkew()
    {
        // Issued as if it were January 2020 with a 1-minute lifetime -- guaranteed to be long
        // expired relative to the real wall clock the validator checks against, regardless of
        // when this test actually runs.
        var farPast = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(farPast);
        var options = CreateOptions(lifetimeMinutes: 1);
        var issuer = new JwtAccessTokenIssuer(timeProvider, Options.Create(options));

        var token = issuer.Issue(CreateUser());
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, CreateValidationParameters(options));

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(result.Exception);
    }
}
