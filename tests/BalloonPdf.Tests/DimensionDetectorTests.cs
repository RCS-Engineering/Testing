using System.Text;
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
        "o15",
        "O15",
        "o 15",
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
    public void DetectPdfPageVectorCandidates_AcceptsDrawingAreaWholeNumbersAndNormalizesOcrDiameterText()
    {
        var pdfPath = CreateVectorTextPdf(
            "vector-dimensions.pdf",
            width: 600,
            height: 400,
            new[]
            {
                TextAt("30", 80, 60),
                TextAt("9", 160, 70),
                TextAt("75", 240, 85),
                TextAt("18", 100, 140),
                TextAt("7", 190, 150),
                TextAt("9", 300, 160),
                TextAt("60", 120, 220),
                TextAt("R15", 250, 240),
                TextAt("o15", 360, 260),
                TextAt("4", 8, 80),
                TextAt("22", 460, 360),
                TextAt("REV", 480, 330)
            });
        var detector = new DimensionDetector(new ImageDimensionDetector(new FakeImageTextExtractor(_ => Array.Empty<ImageTextWord>())));

        var dimensions = detector.Detect(pdfPath);

        Assert.Collection(dimensions,
            first =>
            {
                Assert.Equal("30", first.Text);
                Assert.Equal(1, first.BalloonNumber);
            },
            second =>
            {
                Assert.Equal("9", second.Text);
                Assert.Equal(2, second.BalloonNumber);
            },
            third =>
            {
                Assert.Equal("75", third.Text);
                Assert.Equal(3, third.BalloonNumber);
            },
            fourth =>
            {
                Assert.Equal("18", fourth.Text);
                Assert.Equal(4, fourth.BalloonNumber);
            },
            fifth =>
            {
                Assert.Equal("7", fifth.Text);
                Assert.Equal(5, fifth.BalloonNumber);
            },
            sixth =>
            {
                Assert.Equal("9", sixth.Text);
                Assert.Equal(6, sixth.BalloonNumber);
            },
            seventh =>
            {
                Assert.Equal("60", seventh.Text);
                Assert.Equal(7, seventh.BalloonNumber);
            },
            eighth =>
            {
                Assert.Equal("R15", eighth.Text);
                Assert.Equal(8, eighth.BalloonNumber);
            },
            ninth =>
            {
                Assert.Equal("Ø15", ninth.Text);
                Assert.Equal(9, ninth.BalloonNumber);
            });
    }

    [Fact]
    public void DetectPdfPageVectorCandidates_AcceptsNearMarginAndLowerStandaloneWholeNumbers()
    {
        var pdfPath = CreateVectorTextPdf(
            "standalone-integers-near-drawing-margins.pdf",
            width: 600,
            height: 400,
            new[]
            {
                TextAt("25", 12, 120),
                TextAt("12", 180, 345),
                TextAt("15", 320, 360),
                TextAt("4", 1, 120),
                TextAt("9", 300, 390),
                TextAt("33", 470, 330)
            });
        var detector = new DimensionDetector(new ImageDimensionDetector(new FakeImageTextExtractor(_ => Array.Empty<ImageTextWord>())));

        var dimensions = detector.Detect(pdfPath);

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
    public void DetectPdfPageVectorCandidates_MergesVerticallyStackedDigitsIntoOneWholeNumber()
    {
        var pdfPath = CreateVectorTextPdf(
            "vertical-vector-integer.pdf",
            width: 600,
            height: 400,
            new[]
            {
                TextAt("4", 120, 80),
                TextAt("0", 121, 96),
                TextAt("0", 120, 112),
                TextAt("55", 220, 140)
            });
        var detector = new DimensionDetector(new ImageDimensionDetector(new FakeImageTextExtractor(_ => Array.Empty<ImageTextWord>())));

        var dimensions = detector.Detect(pdfPath);

        Assert.Collection(dimensions,
            first =>
            {
                Assert.Equal("400", first.Text);
                Assert.Equal(1, first.BalloonNumber);
                Assert.True(first.Height > first.Width);
            },
            second =>
            {
                Assert.Equal("55", second.Text);
                Assert.Equal(2, second.BalloonNumber);
            });
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

    private string CreateVectorTextPdf(string fileName, int width, int height, IReadOnlyList<PositionedText> labels)
    {
        var path = Path.Combine(tempDirectory, fileName);
        var textCommands = new StringBuilder();
        foreach (var label in labels)
        {
            textCommands.AppendLine($"BT /F1 12 Tf {label.Left:0.###} {height - label.Top:0.###} Td ({EscapePdfText(label.Text)}) Tj ET");
        }

        WriteSimplePdf(path, width, height, textCommands.ToString());
        return path;
    }

    private static void WriteSimplePdf(string path, int width, int height, string contentStream)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}endstream"
        };

        using var stream = File.Create(path);
        WriteAscii(stream, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefOffset = stream.Position;
        WriteAscii(stream, $"xref\n0 {offsets.Count}\n");
        WriteAscii(stream, "0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            WriteAscii(stream, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(stream, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
    }

    private static void WriteAscii(Stream stream, string text)
    {
        stream.Write(Encoding.ASCII.GetBytes(text));
    }

    private static string EscapePdfText(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static PositionedText TextAt(string text, double left, double top) => new(text, left, top);

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

    private sealed record PositionedText(string Text, double Left, double Top);

    private sealed class FakeImageTextExtractor(Func<string, IReadOnlyList<ImageTextWord>> extractWords) : IImageTextExtractor
    {
        public IReadOnlyList<ImageTextWord> ExtractWords(string imagePath) => extractWords(imagePath);
    }
}
