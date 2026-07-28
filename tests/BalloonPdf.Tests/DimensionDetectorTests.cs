using BalloonPdf.App.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SixLabors.ImageSharp;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class DimensionDetectorTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "BalloonPdfTests", Guid.NewGuid().ToString("N"));

    public DimensionDetectorTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    public static TheoryData<string> AcceptedDimensions => new()
    {
        "12.50",
        "Ø10",
        "⌀10",
        "R5",
        "45°",
        "45 deg",
        "1/2",
        ".250",
        "10 ±0.1",
        "10+/-0.1",
        "DIA. 8",
        "M8",
        "4xM8",
        "4XM8"
    };

    public static TheoryData<string> RejectedText => new()
    {
        "REV A",
        "SHEET 1 OF 2",
        "UNLESS OTHERWISE SPECIFIED",
        "MATERIAL: ALUMINUM",
        "QTY 4",
        "4",
        "A-1234",
        "TRUE POSITION"
    };

    [Theory]
    [MemberData(nameof(AcceptedDimensions))]
    public void IsLikelyDimension_AcceptsCommonEngineeringDimensionFormats(string text)
    {
        Assert.True(DimensionDetector.IsLikelyDimension(text));
    }

    [Theory]
    [MemberData(nameof(RejectedText))]
    public void IsLikelyDimension_RejectsCommonNonDimensionNotes(string text)
    {
        Assert.False(DimensionDetector.IsLikelyDimension(text));
    }

    [Fact]
    public void IsInBottomRightDetailsBox_ReturnsTrueForCandidateCenteredInsideDetailsBox()
    {
        Assert.True(DimensionDetector.IsInBottomRightDetailsBox(
            left: 800d,
            bottom: 80d,
            right: 840d,
            top: 100d,
            pageWidth: 1000d,
            pageHeight: 800d));
    }

    [Theory]
    [InlineData(650d, 80d, 690d, 100d)]
    [InlineData(800d, 240d, 840d, 260d)]
    public void IsInBottomRightDetailsBox_ReturnsFalseForCandidateOutsideDetailsBox(
        double left,
        double bottom,
        double right,
        double top)
    {
        Assert.False(DimensionDetector.IsInBottomRightDetailsBox(
            left,
            bottom,
            right,
            top,
            pageWidth: 1000d,
            pageHeight: 800d));
    }

    [Theory]
    [InlineData(0d, 800d)]
    [InlineData(1000d, 0d)]
    [InlineData(-1000d, 800d)]
    [InlineData(1000d, -800d)]
    public void IsInBottomRightDetailsBox_ReturnsFalseForInvalidPageDimensions(double pageWidth, double pageHeight)
    {
        Assert.False(DimensionDetector.IsInBottomRightDetailsBox(
            left: 800d,
            bottom: 80d,
            right: 840d,
            top: 100d,
            pageWidth: pageWidth,
            pageHeight: pageHeight));
    }

    [Fact]
    public void Detect_UsesOcrFallbackWhenPdfPageHasSparseVectorDimensions()
    {
        var pdfPath = CreateBlankPdf("image-backed.pdf", width: 500, height: 400);
        var detector = new DimensionDetector(new ImageDimensionDetector(new FakeImageTextExtractor(imagePath =>
        {
            var imageInfo = Image.Identify(imagePath) ?? throw new InvalidOperationException("Rendered fallback image could not be decoded.");
            return new[]
            {
                ScaleWord("44", 100, 100, 125, 120, imageInfo.Width, imageInfo.Height),
                ScaleWord("16", 260, 120, 285, 140, imageInfo.Width, imageInfo.Height),
                ScaleWord("Ø120", 500, 140, 555, 160, imageInfo.Width, imageInfo.Height),
                ScaleWord("75", 100, 250, 125, 270, imageInfo.Width, imageInfo.Height),
                ScaleWord("R5", 400, 260, 425, 280, imageInfo.Width, imageInfo.Height),
                ScaleWord("4xM8", 600, 420, 650, 440, imageInfo.Width, imageInfo.Height)
            };
        })));

        var dimensions = detector.Detect(pdfPath);

        Assert.Collection(dimensions,
            first =>
            {
                Assert.Equal("44", first.Text);
                Assert.Equal(1, first.BalloonNumber);
                Assert.Equal(50d, first.Left, precision: 1);
                Assert.Equal(340d, first.Bottom, precision: 1);
            },
            second =>
            {
                Assert.Equal("16", second.Text);
                Assert.Equal(2, second.BalloonNumber);
            },
            third =>
            {
                Assert.Equal("Ø120", third.Text);
                Assert.Equal(3, third.BalloonNumber);
            },
            fourth =>
            {
                Assert.Equal("75", fourth.Text);
                Assert.Equal(4, fourth.BalloonNumber);
            },
            fifth =>
            {
                Assert.Equal("R5", fifth.Text);
                Assert.Equal(5, fifth.BalloonNumber);
            },
            sixth =>
            {
                Assert.Equal("4xM8", sixth.Text);
                Assert.Equal(6, sixth.BalloonNumber);
            });
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string CreateBlankPdf(string fileName, int width, int height)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(width);
        page.Height = XUnit.FromPoint(height);
        document.Save(path);
        return path;
    }

    private static ImageTextWord ScaleWord(string text, int left, int top, int right, int bottom, int imageWidth, int imageHeight)
    {
        return new ImageTextWord(
            text,
            ScaleHorizontal(left, imageWidth),
            ScaleVertical(top, imageHeight),
            ScaleHorizontal(right, imageWidth),
            ScaleVertical(bottom, imageHeight));
    }

    private static int ScaleHorizontal(int coordinate, int imageWidth) => (int)Math.Round(coordinate * (imageWidth / 1000d));

    private static int ScaleVertical(int coordinate, int imageHeight) => (int)Math.Round(coordinate * (imageHeight / 800d));

    private sealed class FakeImageTextExtractor(Func<string, IReadOnlyList<ImageTextWord>> extractWords) : IImageTextExtractor
    {
        public IReadOnlyList<ImageTextWord> ExtractWords(string imagePath) => extractWords(imagePath);
    }
}
