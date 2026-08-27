namespace TPXSoft.Auth.Api.Options;

/// <summary>Bound from Auth:Cors. CORS is a browser/HTTP concept, so it lives in Api rather than
/// Infrastructure (unlike JwtOptions, which Infrastructure also needs for token issuance).</summary>
public sealed class AuthCorsOptions
{
    public string[] AllowedOrigins { get; set; } = [];
}
