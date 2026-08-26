using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.Domain.Services;

/// <summary>Exercises DocumentService.DownloadContentAsync/DownloadByPublicLinkAsync against
/// in-memory fakes -- documentation/05-preview-and-download.md is the spec for every case here.
/// </summary>
public sealed class DocumentServiceDownloadTests
{
    private static readonly byte[] Bytes = [1, 2, 3, 4];

    [Fact]
    public async Task DownloadContentAsync_Owner_ReturnsDocumentAndBytes()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", Bytes.Length, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, Bytes));
        var service = builder.Build();

        var result = await service.DownloadContentAsync(owner, Guid.NewGuid(), document.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(document, result.Value.Document);
        Assert.Equal(Bytes, result.Value.Content);
    }

    [Fact]
    public async Task DownloadContentAsync_ExplicitGrantee_ReturnsDocumentAndBytes()
    {
        // A share grant does not widen listing, but it does grant Read access to content
        // (documentation/README.md's access-rules table).
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", Bytes.Length, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, Bytes));
        builder.DocumentShareRepository.Seed(DocumentShare.Create(document.Id, grantee, owner, builder.TimeProvider));
        var service = builder.Build();

        var result = await service.DownloadContentAsync(grantee, Guid.NewGuid(), document.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Bytes, result.Value.Content);
    }

    [Fact]
    public async Task DownloadContentAsync_SameOrgOrganizationVisibility_ReturnsDocumentAndBytes()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var colleague = Guid.NewGuid();
        var document = Document.Create(
            owner, orgId, null, "report.pdf", "application/pdf", Bytes.Length, Visibility.Organization, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, Bytes));
        var service = builder.Build();

        var result = await service.DownloadContentAsync(colleague, orgId, document.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DownloadContentAsync_SameOrgPrivateVisibility_ReturnsContentForbidden()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var colleague = Guid.NewGuid();
        var document = Document.Create(
            owner, orgId, null, "report.pdf", "application/pdf", Bytes.Length, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, Bytes));
        var service = builder.Build();

        var result = await service.DownloadContentAsync(colleague, orgId, document.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ContentForbidden, result.Error);
    }

    [Fact]
    public async Task DownloadContentAsync_SameOrgColleagueOnPublicLinkDocument_ReturnsContentForbidden()
    {
        // A PublicLink document is not readable here by a non-owner -- public access goes through
        // the token route and nowhere else (documentation 05's authenticated-route section).
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var colleague = Guid.NewGuid();
        var document = Document.Create(
            owner, orgId, null, "report.pdf", "application/pdf", Bytes.Length, Visibility.PublicLink, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, Bytes));
        var service = builder.Build();

        var result = await service.DownloadContentAsync(colleague, orgId, document.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ContentForbidden, result.Error);
    }

    [Fact]
    public async Task DownloadContentAsync_DifferentOrgNoGrant_ReturnsContentForbidden()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", Bytes.Length, Visibility.Organization, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, Bytes));
        var service = builder.Build();

        var result = await service.DownloadContentAsync(Guid.NewGuid(), Guid.NewGuid(), document.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.ContentForbidden, result.Error);
    }

    [Fact]
    public async Task DownloadContentAsync_UnknownId_ReturnsNotFound()
    {
        var service = new DocumentServiceTestBuilder().Build();

        var result = await service.DownloadContentAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotFound, result.Error);
    }

    [Fact]
    public async Task DownloadByPublicLinkAsync_ValidTokenOnPublicLinkDocument_ReturnsDocumentAndBytes()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", Bytes.Length, Visibility.PublicLink, builder.TimeProvider);
        document.ChangeVisibility(Visibility.PublicLink, "the-token", builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, Bytes));
        var service = builder.Build();

        var result = await service.DownloadByPublicLinkAsync("the-token", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Bytes, result.Value.Content);
    }

    [Fact]
    public async Task DownloadByPublicLinkAsync_TokenPresentButVisibilityNotPublicLink_ReturnsPublicLinkNotFound()
    {
        // Rule 2 of the public route: assert Visibility == PublicLink explicitly rather than
        // relying on the token being null whenever visibility isn't PublicLink. The normal
        // ChangeVisibility(Private, ...) call always clears the token, so this state should never
        // arise through the API -- but a bug (or a row predating that invariant) could still leave
        // a non-null token on a non-PublicLink document, and the lookup is by token alone, so this
        // explicit check is the only thing standing between that row and a leak.
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", Bytes.Length, Visibility.PublicLink, builder.TimeProvider);
        document.ChangeVisibility(Visibility.Private, "stale-token-on-private-doc", builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, Bytes));
        var service = builder.Build();

        var result = await service.DownloadByPublicLinkAsync("stale-token-on-private-doc", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.PublicLinkNotFound, result.Error);
    }

    [Fact]
    public async Task DownloadByPublicLinkAsync_UnknownToken_ReturnsPublicLinkNotFound()
    {
        var service = new DocumentServiceTestBuilder().Build();

        var result = await service.DownloadByPublicLinkAsync("garbage-token", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.PublicLinkNotFound, result.Error);
    }

    [Fact]
    public async Task DownloadByPublicLinkAsync_DocumentSwitchedToPrivate_ReturnsPublicLinkNotFound()
    {
        // ChangeVisibility(Private, ...) clears PublicLinkToken as part of the same state
        // transition (Document's own invariant), so the old token no longer matches anything --
        // DownloadByPublicLinkAsync still asserts Visibility == PublicLink explicitly on top of
        // that (rule 2 of the public route), rather than relying on token nullability alone.
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", Bytes.Length, Visibility.PublicLink, builder.TimeProvider);
        document.ChangeVisibility(Visibility.PublicLink, "stale-token", builder.TimeProvider);
        document.ChangeVisibility(Visibility.Private, null, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentRepository.SeedContent(DocumentContent.Create(document.Id, Bytes));
        var service = builder.Build();

        var result = await service.DownloadByPublicLinkAsync("stale-token", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.PublicLinkNotFound, result.Error);
    }
}
