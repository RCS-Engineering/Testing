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

    [Fact]
    public void Detect_AcceptsDrawingAreaWholeNumberDimensionsAndExcludesImageDistractors()
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(new[]
        {
            new ImageTextWord("44", Left: 100, Top: 100, Right: 125, Bottom: 120),
            new ImageTextWord("16", Left: 260, Top: 120, Right: 285, Bottom: 140),
            new ImageTextWord("Ø120", Left: 500, Top: 140, Right: 555, Bottom: 160),
            new ImageTextWord("44", Left: 180, Top: 180, Right: 205, Bottom: 200),
            new ImageTextWord("75", Left: 100, Top: 250, Right: 125, Bottom: 270),
            new ImageTextWord("R5", Left: 400, Top: 260, Right: 425, Bottom: 280),
            new ImageTextWord("4xM8", Left: 600, Top: 420, Right: 650, Bottom: 440),
            new ImageTextWord("1", Left: 5, Top: 100, Right: 20, Bottom: 120),
            new ImageTextWord("2", Left: 500, Top: 5, Right: 515, Bottom: 25),
            new ImageTextWord("Part18", Left: 750, Top: 650, Right: 830, Bottom: 670),
            new ImageTextWord("Image", Left: 100, Top: 735, Right: 155, Bottom: 755),
            new ImageTextWord("ID", Left: 160, Top: 735, Right: 185, Bottom: 755),
            new ImageTextWord("18", Left: 500, Top: 740, Right: 525, Bottom: 760),
            new ImageTextWord("1/1", Left: 800, Top: 710, Right: 840, Bottom: 730)
        }));

        var dimensions = detector.Detect(imagePath);

        Assert.Collection(dimensions,
            first =>
            {
                Assert.Equal("44", first.Text);
                Assert.Equal(1, first.BalloonNumber);
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
                Assert.Equal("44", fourth.Text);
                Assert.Equal(4, fourth.BalloonNumber);
            },
            fifth =>
            {
                Assert.Equal("75", fifth.Text);
                Assert.Equal(5, fifth.BalloonNumber);
            },
            sixth =>
            {
                Assert.Equal("R5", sixth.Text);
                Assert.Equal(6, sixth.BalloonNumber);
            },
            seventh =>
            {
                Assert.Equal("4xM8", seventh.Text);
                Assert.Equal(7, seventh.BalloonNumber);
            });
    }

    [Fact]
    public void Detect_AcceptsNearMarginAndLowerStandaloneWholeNumbersFromOcr()
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(new[]
        {
            new ImageTextWord("25", Left: 22, Top: 120, Right: 44, Bottom: 142),
            new ImageTextWord("12", Left: 260, Top: 720, Right: 284, Bottom: 742),
            new ImageTextWord("15", Left: 500, Top: 735, Right: 524, Bottom: 755),
            new ImageTextWord("4", Left: 2, Top: 120, Right: 12, Bottom: 142),
            new ImageTextWord("9", Left: 400, Top: 775, Right: 412, Bottom: 790),
            new ImageTextWord("33", Left: 760, Top: 690, Right: 784, Bottom: 712)
        }));

        var dimensions = detector.Detect(imagePath);

        Assert.Collection(dimensions,
            first =>
            {
                Assert.Equal("25", first.Text);
                Assert.Equal(1, first.BalloonNumber);
            },
            second =>
            {
                Assert.Equal("12", second.Text);
                Assert.Equal(2, second.BalloonNumber);
            },
            third =>
            {
                Assert.Equal("15", third.Text);
                Assert.Equal(3, third.BalloonNumber);
            });
    }

    [Fact]
    public void Detect_NormalizesSingleAndSplitOcrDiameterVariants()
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(new[]
        {
            new ImageTextWord("o15", Left: 100, Top: 100, Right: 140, Bottom: 122),
            new ImageTextWord("o", Left: 300, Top: 100, Right: 315, Bottom: 122),
            new ImageTextWord("15", Left: 319, Top: 101, Right: 345, Bottom: 123),
            new ImageTextWord("O", Left: 500, Top: 100, Right: 515, Bottom: 122),
            new ImageTextWord("18", Left: 519, Top: 101, Right: 545, Bottom: 123)
        }));

        var dimensions = detector.Detect(imagePath);

        Assert.Collection(dimensions,
            first => Assert.Equal("Ø15", first.Text),
            second => Assert.Equal("Ø15", second.Text),
            third => Assert.Equal("Ø18", third.Text));
    }

    [Fact]
    public void Detect_MergesVerticallyStackedOcrDigitsIntoOneWholeNumber()
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(new[]
        {
            new ImageTextWord("4", Left: 100, Top: 100, Right: 114, Bottom: 120),
            new ImageTextWord("0", Left: 101, Top: 123, Right: 115, Bottom: 143),
            new ImageTextWord("0", Left: 100, Top: 146, Right: 114, Bottom: 166),
            new ImageTextWord("55", Left: 220, Top: 190, Right: 248, Bottom: 212)
        }));

        var dimensions = detector.Detect(imagePath);

        Assert.Collection(dimensions,
            first =>
            {
                Assert.Equal("400", first.Text);
                Assert.Equal(1, first.BalloonNumber);
                Assert.Equal(100, first.Left);
                Assert.Equal(634, first.Bottom);
                Assert.Equal(115, first.Right);
                Assert.Equal(700, first.Top);
            },
            second =>
            {
                Assert.Equal("55", second.Text);
                Assert.Equal(2, second.BalloonNumber);
            });
    }

    [Theory]
    [InlineData("60", "6", "0", 298, 100, 310, 122, 320, 160, 336, 182, "48", 420, 230, 450, 252)]
    [InlineData("300", "3", "0", 298, 100, 310, 122, 320, 160, 336, 182, "48", 420, 285, 450, 307)]
    public void Detect_MergesOffsetStackedOcrIntegersWithoutKeepingSourceDigits(
        string expectedVerticalText,
        string topDigit,
        string lowerDigit,
        int topDigitLeft,
        int topDigitTop,
        int topDigitRight,
        int topDigitBottom,
        int lowerDigitLeft,
        int lowerDigitTop,
        int lowerDigitRight,
        int lowerDigitBottom,
        string followingText,
        int followingLeft,
        int followingTop,
        int followingRight,
        int followingBottom)
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var words = new List<ImageTextWord>
        {
            new(topDigit, topDigitLeft, topDigitTop, topDigitRight, topDigitBottom),
            new(lowerDigit, lowerDigitLeft, lowerDigitTop, lowerDigitRight, lowerDigitBottom),
            new(followingText, followingLeft, followingTop, followingRight, followingBottom)
        };
        if (expectedVerticalText.Length == 3)
        {
            words.Add(new ImageTextWord("0", lowerDigitLeft - 2, lowerDigitTop + 60, lowerDigitRight - 2, lowerDigitBottom + 60));
        }

        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(words));

        var dimensions = detector.Detect(imagePath);

        Assert.Collection(dimensions,
            first =>
            {
                Assert.Equal(expectedVerticalText, first.Text);
                Assert.Equal(1, first.BalloonNumber);
                Assert.True(first.Height > first.Width);
            },
            second =>
            {
                Assert.Equal(followingText, second.Text);
                Assert.Equal(2, second.BalloonNumber);
            });
        Assert.Single(dimensions, dimension => dimension.Text == expectedVerticalText);
        Assert.DoesNotContain(dimensions, dimension => dimension.Text == topDigit || dimension.Text == lowerDigit);
    }

    [Fact]
    public void Detect_MergesOffsetVertical400OcrDigitsWithoutShiftingBalloonNumbers()
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(new[]
        {
            new ImageTextWord("500", Left: 120, Top: 100, Right: 160, Bottom: 122),
            new ImageTextWord("4", Left: 300, Top: 150, Right: 308, Bottom: 170),
            new ImageTextWord("0", Left: 309, Top: 202, Right: 326, Bottom: 222),
            new ImageTextWord("0", Left: 307, Top: 254, Right: 324, Bottom: 274),
            new ImageTextWord("82", Left: 420, Top: 320, Right: 450, Bottom: 342)
        }));

        var dimensions = detector.Detect(imagePath);

        Assert.Collection(dimensions,
            first =>
            {
                Assert.Equal("500", first.Text);
                Assert.Equal(1, first.BalloonNumber);
            },
            second =>
            {
                Assert.Equal("400", second.Text);
                Assert.Equal(2, second.BalloonNumber);
                Assert.Equal(300, second.Left);
                Assert.Equal(526, second.Bottom);
                Assert.Equal(326, second.Right);
                Assert.Equal(650, second.Top);
                Assert.True(second.Height > second.Width);
            },
            third =>
            {
                Assert.Equal("82", third.Text);
                Assert.Equal(3, third.BalloonNumber);
            });
        Assert.Single(dimensions, dimension => dimension.Text == "500");
        Assert.Single(dimensions, dimension => dimension.Text == "400");
        Assert.DoesNotContain(dimensions, dimension => dimension.Text is "4" or "0");
    }

    [Fact]
    public void Detect_MergesWideSpacedAttachmentLikeVertical420OcrDigits()
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(new[]
        {
            new ImageTextWord("500", Left: 120, Top: 100, Right: 160, Bottom: 122),
            new ImageTextWord("4", Left: 300, Top: 150, Right: 308, Bottom: 170),
            new ImageTextWord("2", Left: 301, Top: 194, Right: 309, Bottom: 214),
            new ImageTextWord("0", Left: 300, Top: 238, Right: 308, Bottom: 258),
            new ImageTextWord("82", Left: 420, Top: 320, Right: 450, Bottom: 342)
        }));

        var dimensions = detector.Detect(imagePath);

        Assert.Collection(dimensions,
            first =>
            {
                Assert.Equal("500", first.Text);
                Assert.Equal(1, first.BalloonNumber);
            },
            second =>
            {
                Assert.Equal("420", second.Text);
                Assert.Equal(2, second.BalloonNumber);
                Assert.Equal(300, second.Left);
                Assert.Equal(542, second.Bottom);
                Assert.Equal(309, second.Right);
                Assert.Equal(650, second.Top);
                Assert.True(second.Height > second.Width);
            },
            third =>
            {
                Assert.Equal("82", third.Text);
                Assert.Equal(3, third.BalloonNumber);
            });
        Assert.Single(dimensions, dimension => dimension.Text == "420");
        Assert.DoesNotContain(dimensions, dimension => dimension.Text is "4" or "2" or "0");
    }

    [Fact]
    public void Detect_CombinesSplitSameLineOcrTokensIntoDimensionCandidates()
    {
        var imagePath = CreateJpeg("drawing.jpg", width: 1000, height: 800);
        var detector = new ImageDimensionDetector(new FakeImageTextExtractor(new[]
        {
            new ImageTextWord("Ø", Left: 100, Top: 100, Right: 120, Bottom: 122),
            new ImageTextWord("16", Left: 124, Top: 101, Right: 150, Bottom: 123),
            new ImageTextWord("R", Left: 300, Top: 100, Right: 315, Bottom: 122),
            new ImageTextWord("5", Left: 319, Top: 101, Right: 332, Bottom: 123),
            new ImageTextWord("DIA.", Left: 100, Top: 200, Right: 150, Bottom: 222),
            new ImageTextWord("8", Left: 160, Top: 201, Right: 173, Bottom: 223),
            new ImageTextWord("REV", Left: 200, Top: 200, Right: 245, Bottom: 222)
        }));

        var dimensions = detector.Detect(imagePath);

        Assert.Collection(dimensions,
            first => Assert.Equal("Ø16", first.Text),
            second => Assert.Equal("R5", second.Text),
            third => Assert.Equal("DIA. 8", third.Text));
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
