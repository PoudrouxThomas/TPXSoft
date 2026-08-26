using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// Owner -> can modify. Read -> can fetch metadata and content, cannot modify. None -> 403 (or
/// 404 when the document does not exist at all). Every route in documentation files 02-06
/// resolves the same question, so this lives in one place in Domain and is called everywhere
/// (documentation/02-virtual-folders.md's "Access check shape").
/// </summary>
public enum DocumentAccess
{
    None,
    Read,
    Owner
}

/// <summary>
/// Pure function over already-loaded state, deliberately: no database access, so it is
/// unit-testable on its own and makes the cost of a share-grant lookup an explicit, visible
/// argument at each call site rather than a hidden query inside the check.
/// </summary>
public static class DocumentAccessEvaluator
{
    public static DocumentAccess Evaluate(Document document, Guid callerUserId, Guid callerOrgId, bool hasShareGrant)
    {
        if (document.OwnerUserId == callerUserId)
        {
            return DocumentAccess.Owner;
        }

        if (hasShareGrant)
        {
            return DocumentAccess.Read;
        }

        if (document.Visibility == Visibility.Organization && document.OrgId == callerOrgId)
        {
            return DocumentAccess.Read;
        }

        return DocumentAccess.None;
    }
}
