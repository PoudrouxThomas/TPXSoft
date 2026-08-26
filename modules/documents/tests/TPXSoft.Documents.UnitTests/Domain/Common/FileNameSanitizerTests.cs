using TPXSoft.Documents.Domain.Common;

namespace TPXSoft.Documents.UnitTests.Domain.Common;

/// <summary>documentation/01-upload-document.md's "File name sanitization" section.</summary>
public sealed class FileNameSanitizerTests
{
    [Theory]
    [InlineData(@"..\..\etc\passwd", "passwd")]
    [InlineData("../etc/passwd", "passwd")]
    [InlineData(@"C:\Users\alice\report.pdf", "report.pdf")]
    [InlineData("report.pdf", "report.pdf")]
    public void TryNormalize_StripsEverythingBeforeTheLastPathSeparator(string rawFileName, string expected)
    {
        var ok = FileNameSanitizer.TryNormalize(rawFileName, out var normalized);

        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void TryNormalize_RemovesControlCharacters()
    {
        var ok = FileNameSanitizer.TryNormalize("report\u0000\u001F.pdf", out var normalized);

        Assert.True(ok);
        Assert.Equal("report.pdf", normalized);
    }

    [Fact]
    public void TryNormalize_RemovesDeleteCharacter()
    {
        var ok = FileNameSanitizer.TryNormalize("report\u007F.pdf", out var normalized);

        Assert.True(ok);
        Assert.Equal("report.pdf", normalized);
    }

    [Fact]
    public void TryNormalize_OverMaxLength_TruncatesButPreservesExtension()
    {
        var longName = new string('a', 300) + ".pdf";

        var ok = FileNameSanitizer.TryNormalize(longName, out var normalized);

        Assert.True(ok);
        Assert.Equal(FileNameSanitizer.MaxLength, normalized.Length);
        Assert.EndsWith(".pdf", normalized);
    }

    [Fact]
    public void TryNormalize_AtExactlyMaxLength_IsUnchanged()
    {
        var exactly255 = new string('a', 251) + ".pdf";
        Assert.Equal(FileNameSanitizer.MaxLength, exactly255.Length);

        var ok = FileNameSanitizer.TryNormalize(exactly255, out var normalized);

        Assert.True(ok);
        Assert.Equal(exactly255, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0000\u0001\u001F\u007F")]
    [InlineData(@"\")]
    [InlineData("/")]
    public void TryNormalize_NothingSurvives_ReturnsFalse(string rawFileName)
    {
        var ok = FileNameSanitizer.TryNormalize(rawFileName, out var normalized);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalize_TrimsSurroundingWhitespace()
    {
        var ok = FileNameSanitizer.TryNormalize("  report.pdf  ", out var normalized);

        Assert.True(ok);
        Assert.Equal("report.pdf", normalized);
    }

    [Fact]
    public void TryNormalize_CollapsesInternalWhitespaceRuns()
    {
        var ok = FileNameSanitizer.TryNormalize("my    report.pdf", out var normalized);

        Assert.True(ok);
        Assert.Equal("my report.pdf", normalized);
    }

    // documentation/03-rename-move-delete-document.md: "truncation is upload-only" -- rename uses
    // TryNormalizeStrict, which applies the exact same sanitization as TryNormalize but rejects
    // rather than truncates an overlong result.

    [Fact]
    public void TryNormalizeStrict_OverMaxLength_ReturnsFalse_RatherThanTruncating()
    {
        var longName = new string('a', 300) + ".pdf";

        var ok = FileNameSanitizer.TryNormalizeStrict(longName, out var normalized);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalizeStrict_AtExactlyMaxLength_Succeeds()
    {
        var exactly255 = new string('a', 251) + ".pdf";
        Assert.Equal(FileNameSanitizer.MaxLength, exactly255.Length);

        var ok = FileNameSanitizer.TryNormalizeStrict(exactly255, out var normalized);

        Assert.True(ok);
        Assert.Equal(exactly255, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalizeStrict_EmptyOrWhitespace_ReturnsFalse(string rawFileName)
    {
        var ok = FileNameSanitizer.TryNormalizeStrict(rawFileName, out var normalized);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalizeStrict_StripsPathSeparatorsAndControlCharacters_SameAsTryNormalize()
    {
        var ok = FileNameSanitizer.TryNormalizeStrict(@"..\..\etc\passwd", out var normalized);

        Assert.True(ok);
        Assert.Equal("passwd", normalized);
    }
}
