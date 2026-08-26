namespace TPXSoft.Documents.Domain.Entities;

/// <summary>
/// Private: owner plus explicit share grants only. Organization: any authenticated user in the
/// owner's org, discoverable. PublicLink: reachable only via PublicLinkToken through the
/// unauthenticated /public/documents/{token}/content route -- never listed for other users.
/// Independent of explicit per-user share grants, which apply under any visibility (mirrors the
/// contract's Visibility schema).
/// </summary>
public enum Visibility
{
    Private,
    Organization,
    PublicLink
}
