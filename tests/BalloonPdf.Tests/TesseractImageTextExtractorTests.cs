using BalloonPdf.App.Services;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class TesseractImageTextExtractorTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "BalloonPdfTests", Guid.NewGuid().ToString("N"));

    public TesseractImageTextExtractorTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void ResolveTessdataPath_WhenLanguageDataIsMissing_ThrowsActionableMessage()
    {
        var environmentCandidate = Path.Combine(tempDirectory, "from-environment");
        var appLocalCandidate = Path.Combine(tempDirectory, "app", "tessdata");
        Directory.CreateDirectory(environmentCandidate);
        Directory.CreateDirectory(appLocalCandidate);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TesseractImageTextExtractor.ResolveTessdataPath(new[] { environmentCandidate, appLocalCandidate }));

        Assert.Contains(TesseractImageTextExtractor.ExpectedLanguageDataFileName, exception.Message);
        Assert.Contains("TESSDATA_PREFIX", exception.Message);
        Assert.Contains("tessdata", exception.Message);
        Assert.Contains(Path.GetFullPath(environmentCandidate), exception.Message);
        Assert.Contains(Path.GetFullPath(appLocalCandidate), exception.Message);
        Assert.Contains(TesseractImageTextExtractor.TessdataDownloadUrl, exception.Message);
    }

    [Fact]
    public void ResolveTessdataPath_WhenLanguageDataExists_ReturnsContainingDirectory()
    {
        var missingCandidate = Path.Combine(tempDirectory, "missing");
        var validCandidate = Path.Combine(tempDirectory, "valid-tessdata");
        Directory.CreateDirectory(missingCandidate);
        Directory.CreateDirectory(validCandidate);
        File.WriteAllText(Path.Combine(validCandidate, TesseractImageTextExtractor.ExpectedLanguageDataFileName), string.Empty);

        var resolvedPath = TesseractImageTextExtractor.ResolveTessdataPath(new[] { missingCandidate, validCandidate });

        Assert.Equal(Path.GetFullPath(validCandidate), resolvedPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
