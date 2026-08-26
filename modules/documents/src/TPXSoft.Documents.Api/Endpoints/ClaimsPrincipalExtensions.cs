using System.Security.Claims;

namespace TPXSoft.Documents.Api.Endpoints;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the "sub" claim (User.Id) issued by TPXSoft.Auth's JwtAccessTokenIssuer.
    /// Relies on MapInboundClaims being disabled so "sub" isn't remapped to a long URI claim
    /// type. Documents-local copy -- the two Api projects do not reference each other.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }

    /// <summary>Reads the "orgId" claim issued by TPXSoft.Auth's JwtAccessTokenIssuer
    /// (["orgId"] = user.OrgId.ToString()). Drives Organization-visibility checks.</summary>
    public static Guid? GetOrgId(this ClaimsPrincipal principal)
    {
        var orgId = principal.FindFirstValue("orgId");
        return Guid.TryParse(orgId, out var parsed) ? parsed : null;
    }
}
