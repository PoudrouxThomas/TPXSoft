using System.ComponentModel.DataAnnotations;

namespace TPXSoft.Auth.Infrastructure.Options;

/// <summary>Bound from Auth (refresh-token policy, as opposed to Auth:Jwt for access tokens).</summary>
public sealed class AuthTokenOptions
{
    [Range(1, int.MaxValue)]
    public int RefreshTokenLifetimeDays { get; set; } = 7;
}
