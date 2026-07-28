using BalloonPdf.App.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class ImageDimensionDetectorTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "BalloonPdfTests", Guid.NewGuid().ToString("N"));

    public ImageDimensionDetectorTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void Detect_ConvertsOcrTopLeftCoordinatesToBottomLeftPageCoordinates()
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(new[]
        {
            new ImageTextWord("12.50", Left: 20, Top: 100, Right: 70, Bottom: 120)
        }));

        var dimensions = detector.Detect(imagePath);

        var dimension = Assert.Single(dimensions);
        Assert.Equal(1, dimension.PageNumber);
        Assert.Equal("12.50", dimension.Text);
        Assert.Equal(20, dimension.Left);
        Assert.Equal(680, dimension.Bottom);
        Assert.Equal(70, dimension.Right);
        Assert.Equal(700, dimension.Top);
        Assert.Equal(1, dimension.BalloonNumber);
    }

    [Fact]
    public void Detect_FiltersTitleBlockDetailsTextAndNonDimensions()
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(new[]
        {
            new ImageTextWord("12.50", Left: 20, Top: 100, Right: 70, Bottom: 120),
            new ImageTextWord("REV", Left: 80, Top: 100, Right: 120, Bottom: 120),
            new ImageTextWord("45°", Left: 800, Top: 700, Right: 840, Bottom: 720)
        }));

        var dimensions = detector.Detect(imagePath);

        var dimension = Assert.Single(dimensions);
        Assert.Equal("12.50", dimension.Text);
    }

    [Fact]
    public void Detect_AssignsBalloonNumbersInSameReadingOrderAsPdfPath()
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(new[]
        {
            new ImageTextWord("R5", Left: 300, Top: 300, Right: 330, Bottom: 320),
            new ImageTextWord("12.50", Left: 20, Top: 100, Right: 70, Bottom: 120),
            new ImageTextWord("Ø10", Left: 100, Top: 100, Right: 140, Bottom: 120)
        }));

        var dimensions = detector.Detect(imagePath);

        Assert.Collection(dimensions,
            first =>
            {
                Assert.Equal("12.50", first.Text);
                Assert.Equal(1, first.BalloonNumber);
            },
            second =>
            {
                Assert.Equal("Ø10", second.Text);
                Assert.Equal(2, second.BalloonNumber);
            },
            third =>
            {
                Assert.Equal("R5", third.Text);
                Assert.Equal(3, third.BalloonNumber);
            });
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string CreateJpeg(string fileName, int width, int height)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var image = new Image<Rgba32>(width, height, Color.White);
        image.SaveAsJpeg(path);
        return path;
    }

    private sealed class FakeImageTextExtractor(IReadOnlyList<ImageTextWord> words) : IImageTextExtractor
    {
        public IReadOnlyList<ImageTextWord> ExtractWords(string imagePath) => words;
    }
}
