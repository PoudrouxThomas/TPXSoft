namespace TPXSoft.Documents.Api.Options;

/// <summary>Bound from Documents:Cors. CORS is a browser/HTTP concept, so it lives in Api rather
/// than Infrastructure (unlike JwtOptions, which Infrastructure also needs for token validation).</summary>
public sealed class DocumentsCorsOptions
{
    public string[] AllowedOrigins { get; set; } = [];
}
