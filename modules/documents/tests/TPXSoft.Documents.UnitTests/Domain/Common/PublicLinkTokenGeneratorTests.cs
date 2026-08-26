using TPXSoft.Documents.Domain.Common;

namespace TPXSoft.Documents.UnitTests.Domain.Common;

/// <summary>documentation/04-sharing-and-visibility.md's "Token generation and storage" section and
/// "Tests -> Unit" list: 32 CSPRNG bytes, base64url-encoded without padding -- 43 characters, and
/// distinct across repeated draws.</summary>
public sealed class PublicLinkTokenGeneratorTests
{
    [Fact]
    public void Generate_ReturnsFortyThreeCharacterToken()
    {
        var token = PublicLinkTokenGenerator.Generate();

        Assert.Equal(43, token.Length);
    }

    [Fact]
    public void Generate_ReturnsOnlyUrlSafeBase64Characters()
    {
        // base64url alphabet without padding: A-Z, a-z, 0-9, '-', '_' -- no '+', '/', or '='.
        var token = PublicLinkTokenGenerator.Generate();

        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    [Fact]
    public void Generate_OneThousandDraws_AreAllDistinct()
    {
        var tokens = new HashSet<string>();
        for (var i = 0; i < 1000; i++)
        {
            tokens.Add(PublicLinkTokenGenerator.Generate());
        }

        Assert.Equal(1000, tokens.Count);
    }
}
