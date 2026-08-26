using Microsoft.Extensions.Time.Testing;
using TPXSoft.Documents.Domain.Entities;

namespace TPXSoft.Documents.UnitTests.Domain.Entities;

/// <summary>documentation/01-upload-document.md's "Tests -> Unit" section: Document.Create's
/// upload-relevant guarantees.</summary>
public sealed class DocumentTests
{
    [Fact]
    public void Create_WithPrivateVisibility_SetsPublicLinkTokenNull()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 1024, Visibility.Private, timeProvider);

        Assert.Equal(Visibility.Private, document.Visibility);
        Assert.Null(document.PublicLinkToken);
    }

    [Fact]
    public void Create_SetsCreatedAtEqualToUpdatedAt_FromFrozenTimeProvider()
    {
        var frozen = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(frozen);

        var document = Document.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "report.pdf", "application/pdf", 1024, Visibility.Private, timeProvider);

        Assert.Equal(frozen, document.CreatedAt);
        Assert.Equal(document.CreatedAt, document.UpdatedAt);
    }
}
