using TPXSoft.Documents.Domain.Common;

namespace TPXSoft.Documents.UnitTests.Domain.Common;

/// <summary>documentation/01-upload-document.md's "Content type" section.</summary>
public sealed class ContentTypeSanitizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_MissingOrBlank_FallsBackToOctetStream(string? rawContentType)
    {
        var normalized = ContentTypeSanitizer.Normalize(rawContentType);

        Assert.Equal(ContentTypeSanitizer.Fallback, normalized);
    }

    [Theory]
    [InlineData("not-a-media-type")]
    [InlineData("/missing-type")]
    [InlineData("missing-subtype/")]
    [InlineData("text/plain/extra")]
    [InlineData("text plain")]
    public void Normalize_MalformedMediaType_FallsBackToOctetStream(string rawContentType)
    {
        var normalized = ContentTypeSanitizer.Normalize(rawContentType);

        Assert.Equal(ContentTypeSanitizer.Fallback, normalized);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("image/png")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void Normalize_ValidMediaType_IsUnchanged(string rawContentType)
    {
        var normalized = ContentTypeSanitizer.Normalize(rawContentType);

        Assert.Equal(rawContentType, normalized);
    }

    [Fact]
    public void Normalize_ValidMediaTypeWithParameter_IsUnchanged()
    {
        const string rawContentType = "text/plain; charset=utf-8";

        var normalized = ContentTypeSanitizer.Normalize(rawContentType);

        Assert.Equal(rawContentType, normalized);
    }

    [Fact]
    public void Normalize_OverMaxLength_FallsBackToOctetStream()
    {
        var tooLong = "application/" + new string('a', ContentTypeSanitizer.MaxLength);

        var normalized = ContentTypeSanitizer.Normalize(tooLong);

        Assert.Equal(ContentTypeSanitizer.Fallback, normalized);
    }
}
