using System.ComponentModel.DataAnnotations;

namespace TPXSoft.Auth.Infrastructure.Options;

/// <summary>Bound from Auth:Jwt. Validated at startup (ValidateOnStart) -- a missing/short
/// signing key crashes at boot, not at first login.</summary>
public sealed class JwtOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "Auth:Jwt:SigningKey must be at least 32 bytes (256 bits) for HMAC-SHA256.")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}
