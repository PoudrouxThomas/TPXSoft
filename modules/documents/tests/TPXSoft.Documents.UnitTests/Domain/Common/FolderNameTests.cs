using TPXSoft.Documents.Domain.Common;

namespace TPXSoft.Documents.UnitTests.Domain.Common;

public sealed class FolderNameTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\t")]
    public void TryNormalize_RejectsEmptyOrWhitespaceOnly(string rawName)
    {
        var ok = FolderName.TryNormalize(rawName, out var normalized);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalize_RejectsNameLongerThan255Characters()
    {
        var tooLong = new string('a', 256);

        var ok = FolderName.TryNormalize(tooLong, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryNormalize_Accepts255Characters()
    {
        var exactly255 = new string('a', 255);

        var ok = FolderName.TryNormalize(exactly255, out var normalized);

        Assert.True(ok);
        Assert.Equal(exactly255, normalized);
    }

    [Theory]
    [InlineData("Q3\nReports")]
    [InlineData("Q3\tReports")]
    public void TryNormalize_RejectsControlCharacters(string rawName)
    {
        var ok = FolderName.TryNormalize(rawName, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryNormalize_TrimsSurroundingWhitespace()
    {
        var ok = FolderName.TryNormalize("  Q3 Reports  ", out var normalized);

        Assert.True(ok);
        Assert.Equal("Q3 Reports", normalized);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("Reports/2026")]
    [InlineData("..")]
    public void TryNormalize_AllowsDotDotAndSlash_NamesAreDisplayStringsNotPathSegments(string rawName)
    {
        var ok = FolderName.TryNormalize(rawName, out var normalized);

        Assert.True(ok);
        Assert.Equal(rawName, normalized);
    }
}
