using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TPXSoft.Auth.Domain.Abstractions;
using TPXSoft.Auth.Domain.Entities;
using TPXSoft.Auth.Infrastructure.Options;

namespace TPXSoft.Auth.Infrastructure.Security;

/// <summary>
/// Issues HMAC-SHA256-signed access tokens via JsonWebTokenHandler (the modern handler, not the
/// legacy JwtSecurityTokenHandler). Claim set is part of the contract (see auth.v1.yaml's
/// bearerAuth description): sub, email, orgId, role, jti/iat/exp/iss/aud.
/// </summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly TimeProvider _timeProvider;
    private readonly JwtOptions _options;

    public JwtAccessTokenIssuer(TimeProvider timeProvider, IOptions<JwtOptions> options)
    {
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public string Issue(User user)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Email] = user.Email,
            ["orgId"] = user.OrgId.ToString(),
            ["role"] = user.Role.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(_options.AccessTokenLifetimeMinutes),
            Claims = claims,
            SigningCredentials = credentials
        };

        return Handler.CreateToken(descriptor);
    }
}
