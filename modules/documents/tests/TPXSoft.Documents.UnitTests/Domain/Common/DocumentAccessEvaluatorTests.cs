using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.Domain.Common;

/// <summary>Exercises DocumentAccessEvaluator.Evaluate directly -- a pure function over
/// already-loaded state, no database -- per documentation/02-virtual-folders.md's "Tests ->
/// Unit" section and the "Access check shape" rules it lists in order.</summary>
public sealed class DocumentAccessEvaluatorTests
{
    private static readonly TimeProvider TimeProvider = TimeProvider.System;

    [Theory]
    [InlineData(Visibility.Private)]
    [InlineData(Visibility.Organization)]
    [InlineData(Visibility.PublicLink)]
    public void Evaluate_Owner_ReturnsOwner_RegardlessOfVisibility(Visibility visibility)
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var document = Document.Create(owner, orgId, null, "file.txt", "text/plain", 100, visibility, TimeProvider);

        var access = DocumentAccessEvaluator.Evaluate(document, callerUserId: owner, callerOrgId: orgId, hasShareGrant: false);

        Assert.Equal(DocumentAccess.Owner, access);
    }

    [Fact]
    public void Evaluate_GranteeOnPrivateDocument_ReturnsRead()
    {
        // DocumentShare does not exist yet (feature 04) so DocumentService always passes
        // hasShareGrant: false today -- Evaluate itself already supports true, so this test
        // exercises the function directly rather than through DocumentService.
        var owner = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        var granteeOrgId = Guid.NewGuid();
        var document = Document.Create(owner, Guid.NewGuid(), null, "file.txt", "text/plain", 100, Visibility.Private, TimeProvider);

        var access = DocumentAccessEvaluator.Evaluate(document, callerUserId: grantee, callerOrgId: granteeOrgId, hasShareGrant: true);

        Assert.Equal(DocumentAccess.Read, access);
    }

    [Fact]
    public void Evaluate_SameOrgCallerOnOrganizationVisibility_ReturnsRead()
    {
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var sameOrgCaller = Guid.NewGuid();
        var document = Document.Create(owner, orgId, null, "file.txt", "text/plain", 100, Visibility.Organization, TimeProvider);

        var access = DocumentAccessEvaluator.Evaluate(document, callerUserId: sameOrgCaller, callerOrgId: orgId, hasShareGrant: false);

        Assert.Equal(DocumentAccess.Read, access);
    }

    [Fact]
    public void Evaluate_SameOrgCallerOnPublicLink_ReturnsNone()
    {
        // "Only when Organization" (the previous test's positive case) -- PublicLink is the
        // documented negative case: same-org membership alone does not widen it.
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var sameOrgCaller = Guid.NewGuid();
        var document = Document.Create(owner, orgId, null, "file.txt", "text/plain", 100, Visibility.PublicLink, TimeProvider);

        var access = DocumentAccessEvaluator.Evaluate(document, callerUserId: sameOrgCaller, callerOrgId: orgId, hasShareGrant: false);

        Assert.Equal(DocumentAccess.None, access);
    }

    [Fact]
    public void Evaluate_DifferentOrgWithNoGrant_ReturnsNone()
    {
        var owner = Guid.NewGuid();
        var ownerOrgId = Guid.NewGuid();
        var differentOrgCaller = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var document = Document.Create(owner, ownerOrgId, null, "file.txt", "text/plain", 100, Visibility.Organization, TimeProvider);

        var access = DocumentAccessEvaluator.Evaluate(document, callerUserId: differentOrgCaller, callerOrgId: differentOrgId, hasShareGrant: false);

        Assert.Equal(DocumentAccess.None, access);
    }
}
