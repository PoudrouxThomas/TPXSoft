using System.ComponentModel.DataAnnotations;

namespace TPXSoft.Documents.Infrastructure.Options;

/// <summary>
/// Bound from Documents:Jwt. This module issues no tokens of its own -- it validates the same
/// access tokens TPXSoft.Auth issues, so Issuer/Audience/SigningKey must be configured to match
/// Auth's Auth:Jwt:* values exactly. Validated at startup (ValidateOnStart) -- a missing/short
/// signing key crashes at boot, not at the first authenticated request.
/// </summary>
public sealed class JwtOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "Documents:Jwt:SigningKey must be at least 32 bytes (256 bits) for HMAC-SHA256.")]
    public string SigningKey { get; set; } = string.Empty;
}
