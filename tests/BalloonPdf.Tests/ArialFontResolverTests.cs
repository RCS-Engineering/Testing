using BalloonPdf.App.Services;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class ArialFontResolverTests
{
    public static TheoryData<bool, bool, string> ArialFaces => new()
    {
        { false, false, ArialFontResolver.RegularFaceName },
        { true, false, ArialFontResolver.BoldFaceName },
        { false, true, ArialFontResolver.ItalicFaceName },
        { true, true, ArialFontResolver.BoldItalicFaceName }
    };

    [Theory]
    [MemberData(nameof(ArialFaces))]
    public void ResolveTypeface_MapsArialStyleToExpectedFace(bool isBold, bool isItalic, string expectedFaceName)
    {
        var resolver = new ArialFontResolver(_ => null);

        var resolved = resolver.ResolveTypeface("Arial", isBold, isItalic);

        Assert.NotNull(resolved);
        Assert.Equal(expectedFaceName, resolved.FaceName);
    }

    [Fact]
    public void ResolveTypeface_MatchesArialCaseInsensitively()
    {
        var resolver = new ArialFontResolver(_ => null);

        var resolved = resolver.ResolveTypeface("aRiAl", isBold: true, isItalic: false);

        Assert.NotNull(resolved);
        Assert.Equal(ArialFontResolver.BoldFaceName, resolved.FaceName);
    }

    [Fact]
    public void ResolveTypeface_ReturnsNullForUnsupportedFamily()
    {
        var resolver = new ArialFontResolver(_ => null);

        var resolved = resolver.ResolveTypeface("Calibri", isBold: false, isItalic: false);

        Assert.Null(resolved);
    }

    [Fact]
    public void GetFont_LoadsBytesForResolvedFaceName()
    {
        var expectedBytes = new byte[] { 1, 2, 3, 4 };
        var resolver = new ArialFontResolver(fileName =>
            fileName == "arialbd.ttf"
                ? expectedBytes
                : null);
        var faceName = resolver.ResolveTypeface("Arial", isBold: true, isItalic: false)!.FaceName;

        var fontBytes = resolver.GetFont(faceName);

        Assert.Same(expectedBytes, fontBytes);
    }

    [Fact]
    public void GetFont_CachesLoadedBytesByFaceName()
    {
        var loadCount = 0;
        var resolver = new ArialFontResolver(_ =>
        {
            loadCount++;
            return new byte[] { 5, 6, 7, 8 };
        });

        resolver.GetFont(ArialFontResolver.RegularFaceName);
        resolver.GetFont(ArialFontResolver.RegularFaceName);

        Assert.Equal(1, loadCount);
    }
}
