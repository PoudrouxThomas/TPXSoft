using System.Security.Claims;

namespace TPXSoft.Auth.Api.Endpoints;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the "sub" claim (User.Id) issued by JwtAccessTokenIssuer. Relies on
    /// MapInboundClaims being disabled so "sub" isn't remapped to a long URI claim type.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
