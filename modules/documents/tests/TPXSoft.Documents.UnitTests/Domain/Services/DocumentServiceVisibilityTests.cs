using TPXSoft.Documents.Domain.Common;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.Domain.Services;

/// <summary>Exercises DocumentService.SetVisibilityAsync against in-memory fakes --
/// documentation/04-sharing-and-visibility.md's "setDocumentVisibility" section and "Tests -> Unit"
/// list are the spec for every case here.</summary>
public sealed class DocumentServiceVisibilityTests
{
    [Fact]
    public async Task SetVisibilityAsync_ToPublicLink_GeneratesToken()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.SetVisibilityAsync(owner, document.Id, Visibility.PublicLink, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Visibility.PublicLink, result.Value.Visibility);
        Assert.NotNull(result.Value.PublicLinkToken);
    }

    [Fact]
    public async Task SetVisibilityAsync_ToPublicLinkTwice_YieldsTwoDifferentTokens()
    {
        // "(Re)generates a fresh token, every time" -- going PublicLink -> PublicLink again mints a
        // different token, breaking every link already handed out (doc 04's "re-generation rule").
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        // Document is a reference type and FakeDocumentRepository.GetByIdAsync returns the same
        // tracked instance both times, so the token must be captured right after each call --
        // reading it later off "first.Value" would actually be reading the second call's mutation
        // of the same object.
        var first = await service.SetVisibilityAsync(owner, document.Id, Visibility.PublicLink, CancellationToken.None);
        var firstToken = first.Value.PublicLinkToken;
        var second = await service.SetVisibilityAsync(owner, document.Id, Visibility.PublicLink, CancellationToken.None);
        var secondToken = second.Value.PublicLinkToken;

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotNull(firstToken);
        Assert.NotNull(secondToken);
        Assert.NotEqual(firstToken, secondToken);
    }

    [Fact]
    public async Task SetVisibilityAsync_ToPrivate_ClearsToken()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();
        await service.SetVisibilityAsync(owner, document.Id, Visibility.PublicLink, CancellationToken.None);

        var result = await service.SetVisibilityAsync(owner, document.Id, Visibility.Private, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Visibility.Private, result.Value.Visibility);
        Assert.Null(result.Value.PublicLinkToken);
    }

    [Fact]
    public async Task SetVisibilityAsync_ToOrganization_ClearsToken()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();
        await service.SetVisibilityAsync(owner, document.Id, Visibility.PublicLink, CancellationToken.None);

        var result = await service.SetVisibilityAsync(owner, document.Id, Visibility.Organization, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Visibility.Organization, result.Value.Visibility);
        Assert.Null(result.Value.PublicLinkToken);
    }

    [Fact]
    public async Task SetVisibilityAsync_NeverTouchesShareGrants()
    {
        // "Changing visibility never touches grants" (doc 04's "Two independent axes" section).
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Organization, builder.TimeProvider);
        var grant = DocumentShare.Create(document.Id, Guid.NewGuid(), owner, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        builder.DocumentShareRepository.Seed(grant);
        var service = builder.Build();

        var result = await service.SetVisibilityAsync(owner, document.Id, Visibility.Private, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(builder.DocumentShareRepository.Added);
        Assert.Empty(builder.DocumentShareRepository.Removed);
        var shares = await builder.DocumentShareRepository.ListByDocumentAsync(document.Id, CancellationToken.None);
        Assert.Single(shares);
        Assert.Equal(grant.Id, shares[0].Id);
    }

    [Fact]
    public async Task SetVisibilityAsync_NonOwner_ReturnsNotOwner_AndVisibilityUnchanged()
    {
        var builder = new DocumentServiceTestBuilder();
        var owner = Guid.NewGuid();
        var document = Document.Create(
            owner, Guid.NewGuid(), null, "report.pdf", "application/pdf", 100, Visibility.Private, builder.TimeProvider);
        builder.DocumentRepository.Seed(document);
        var service = builder.Build();

        var result = await service.SetVisibilityAsync(Guid.NewGuid(), document.Id, Visibility.PublicLink, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DocumentError.NotOwner, result.Error);
        Assert.Equal(Visibility.Private, document.Visibility);
        Assert.Null(document.PublicLinkToken);
    }
}
