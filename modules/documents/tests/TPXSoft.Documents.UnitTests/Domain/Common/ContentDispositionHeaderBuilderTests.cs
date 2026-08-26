using TPXSoft.Documents.Domain.Common;

namespace TPXSoft.Documents.UnitTests.Domain.Common;

/// <summary>documentation/05-preview-and-download.md's "Filename encoding" section and Tests
/// list.</summary>
public sealed class ContentDispositionHeaderBuilderTests
{
    [Fact]
    public void BuildAttachment_AsciiName_QuotedInFileNameAndRepeatedInFileNameStar()
    {
        var header = ContentDispositionHeaderBuilder.BuildAttachment("report.pdf");

        Assert.Equal("attachment; filename=\"report.pdf\"; filename*=UTF-8''report.pdf", header);
    }

    [Fact]
    public void BuildAttachment_NonAsciiName_EmitsValidFileNameStarForm()
    {
        var header = ContentDispositionHeaderBuilder.BuildAttachment("rapport-été.pdf");

        // filename= gets an ASCII-folded fallback ('_' for anything outside printable ASCII);
        // filename*= carries the real UTF-8 percent-encoded name so non-ASCII survives.
        Assert.Contains("filename=\"rapport-_t_.pdf\"", header);
        Assert.Contains("filename*=UTF-8''rapport-%C3%A9t%C3%A9.pdf", header);
    }

    [Fact]
    public void BuildAttachment_NameContainingQuoteAndBackslash_EscapesBoth()
    {
        var header = ContentDispositionHeaderBuilder.BuildAttachment("a\"b\\c.txt");

        Assert.Contains("filename=\"a\\\"b\\\\c.txt\"", header);
    }

    [Theory]
    [InlineData("report\r\n.pdf")]
    [InlineData("report\r.pdf")]
    [InlineData("report\n.pdf")]
    public void BuildAttachment_NameContainingCrOrLf_StripsThemFromTheHeaderEntirely(string rawFileName)
    {
        // A raw CR or LF surviving into a response header is a response-splitting bug -- re-checked
        // here rather than trusted, because rows predating the upload-time sanitization fix would
        // still be sitting in the database.
        var header = ContentDispositionHeaderBuilder.BuildAttachment(rawFileName);

        Assert.DoesNotContain('\r', header);
        Assert.DoesNotContain('\n', header);
        Assert.Contains("report.pdf", header);
    }

    [Fact]
    public void BuildAttachment_AlwaysAttachment_NeverInline()
    {
        // v1 has no inline preview -- every response is attachment (documentation 05's "Preview vs
        // download" section).
        var header = ContentDispositionHeaderBuilder.BuildAttachment("report.pdf");

        Assert.StartsWith("attachment;", header);
    }
}
