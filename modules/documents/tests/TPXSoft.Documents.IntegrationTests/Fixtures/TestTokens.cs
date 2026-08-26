using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace TPXSoft.Documents.IntegrationTests.Fixtures;

/// <summary>
/// Documents issues no access tokens of its own -- it only validates the same tokens
/// TPXSoft.Auth issues (see modules/documents/CLAUDE.md "Auth" section and
/// JwtAccessTokenIssuer in Auth.Infrastructure, which this mirrors). Integration tests here mint
/// tokens with the exact same claim shape (sub/orgId/jti, HMAC-SHA256) against the signing
/// key/issuer/audience configured on <see cref="DocumentsWebApplicationFactory"/>, standing in
/// for a real Auth-issued token.
/// </summary>
public static class TestTokens
{
    public const string Issuer = "tpxsoft-auth-integration-tests";
    public const string Audience = "tpxsoft-auth-integration-tests";
    public const string SigningKey = "integration-test-signing-key-0123456789ab";

    private static readonly JsonWebTokenHandler Handler = new();

    public static string IssueFor(Guid userId, Guid? orgId = null)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = userId.ToString(),
            ["orgId"] = (orgId ?? Guid.NewGuid()).ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(15),
            Claims = claims,
            SigningCredentials = credentials
        };

        return Handler.CreateToken(descriptor);
    }
}
