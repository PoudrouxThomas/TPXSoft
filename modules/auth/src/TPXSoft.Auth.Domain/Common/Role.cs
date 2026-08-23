namespace TPXSoft.Auth.Domain.Common;

/// <summary>
/// A User's role within their Org. Phase 1 has exactly two roles; no per-resource permissions yet.
/// </summary>
public enum Role
{
    Admin,
    Member
}
