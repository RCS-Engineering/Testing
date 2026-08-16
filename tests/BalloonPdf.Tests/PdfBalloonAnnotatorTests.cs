using System.IO;
using BalloonPdf.App.Models;
using BalloonPdf.App.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class PdfBalloonAnnotatorTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "BalloonPdfTests", Guid.NewGuid().ToString("N"));
    private readonly PdfBalloonAnnotator annotator = new();

    public PdfBalloonAnnotatorTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void AddBalloons_AcceptsAnnotationsWithCustomNumbersAndColors()
    {
        var inputPath = CreateBlankPdf("input.pdf");
        var outputPath = Path.Combine(tempDirectory, "nested", "output.pdf");
        var annotations = new[]
        {
            BalloonAnnotation.Create(1, centerX: 75, centerY: 125, balloonNumber: 42, strokeColorHex: "#FF0000")
        };

        annotator.AddBalloons(inputPath, outputPath, annotations);

        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact]
    public void AddBalloons_PreventsOverwritingInputPdf()
    {
        var inputPath = CreateBlankPdf("input.pdf");
        var annotations = new[]
        {
            BalloonAnnotation.Create(1, centerX: 75, centerY: 125, balloonNumber: 1)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => annotator.AddBalloons(inputPath, inputPath, annotations));
        Assert.Contains("output PDF path must be different", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddBalloons_DimensionCompatibilityPathProducesOutputPdf()
    {
        var inputPath = CreateBlankPdf("input.pdf");
        var outputPath = Path.Combine(tempDirectory, "output.pdf");
        var dimensions = new[]
        {
            new DimensionCandidate(1, "1.250", 20, 30, 50, 42, 7)
        };

        annotator.AddBalloons(inputPath, outputPath, dimensions);

        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact]
    public void AddBalloons_PreservesSourceTextAndWritesBalloonNumberAsExtractableText()
    {
        var inputPath = CreatePdfWithVisibleText("input-with-text.pdf", "SOURCE DRAWING TEXT");
        var outputPath = Path.Combine(tempDirectory, "output-with-text.pdf");
        var annotations = new[]
        {
            BalloonAnnotation.Create(1, centerX: 100, centerY: 100, balloonNumber: 42, strokeColorHex: "#000000")
        };

        annotator.AddBalloons(inputPath, outputPath, annotations);

        using var document = UglyToad.PdfPig.PdfDocument.Open(outputPath);
        var text = document.GetPage(1).Text;
        Assert.Contains("SOURCE DRAWING TEXT", text, StringComparison.Ordinal);
        Assert.Contains("42", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AddBalloons_CreatesRenderablePdfFromJpegInput()
    {
        var inputPath = CreateJpegWithBlackRectangle("input.jpg");
        var outputPath = Path.Combine(tempDirectory, "image-output.pdf");
        var annotations = new[]
        {
            BalloonAnnotation.Create(1, centerX: 100, centerY: 80, balloonNumber: 7, strokeColorHex: "#000000")
        };

        annotator.AddBalloons(inputPath, outputPath, annotations);

        Assert.True(File.Exists(outputPath));
        var preview = new PdfPagePreviewRenderer().RenderPage(outputPath, pageNumber: 1, pixelWidth: 160, pixelHeight: 120);
        Assert.Equal(PdfPagePreviewPixelFormat.Bgra32, preview.PixelFormat);
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact]
    public void AddBalloons_RasterizesBalloonNumberAsDenseFilledBlueGlyphs()
    {
        var inputPath = CreateBlankPdf("input-for-rasterized-number.pdf");
        var outputPath = Path.Combine(tempDirectory, "output-with-rasterized-number.pdf");
        var annotations = new[]
        {
            BalloonAnnotation.Create(1, centerX: 100, centerY: 100, balloonNumber: 88)
        };

        annotator.AddBalloons(inputPath, outputPath, annotations);

        var preview = new PdfPagePreviewRenderer().RenderPage(outputPath, pageNumber: 1, pixelWidth: 400, pixelHeight: 400);
        var bluePixels = CountBluePixels(preview, left: 185, top: 188, width: 30, height: 24);

        Assert.True(bluePixels >= 85, $"Expected a dense filled blue digit footprint, but found only {bluePixels} blue pixels.");
    }

    [Fact]
    public void AddBalloons_FromDimensionsDrawsBlueAssociationArrow()
    {
        var inputPath = CreateBlankPdf("input-for-arrow.pdf");
        var outputPath = Path.Combine(tempDirectory, "output-with-arrow.pdf");
        var dimensions = new[]
        {
            new DimensionCandidate(1, "1.250", 20, 90, 70, 110, 7)
        };

        annotator.AddBalloons(inputPath, outputPath, dimensions);

        var preview = new PdfPagePreviewRenderer().RenderPage(outputPath, pageNumber: 1, pixelWidth: 400, pixelHeight: 400);
        var arrowPixels = CountBluePixels(preview, left: 138, top: 194, width: 28, height: 14);

        Assert.True(arrowPixels >= 12, $"Expected blue pixels along the association arrow path, but found only {arrowPixels}.");
    }

    [Fact]
    public void AddBalloons_SkipsTargetAnchoredInsideVectorTableButKeepsOutsideBalloon()
    {
        var inputPath = CreatePdfWithVectorTable("input-with-table.pdf");
        var outputPath = Path.Combine(tempDirectory, "output-with-table-suppression.pdf");
        var annotations = new[]
        {
            BalloonAnnotation.Create(
                1,
                centerX: 160,
                centerY: 70,
                balloonNumber: 1,
                targetX: 60,
                targetY: 70),
            BalloonAnnotation.Create(1, centerX: 160, centerY: 150, balloonNumber: 2)
        };

        annotator.AddBalloons(inputPath, outputPath, annotations);

        var preview = new PdfPagePreviewRenderer().RenderPage(outputPath, pageNumber: 1, pixelWidth: 400, pixelHeight: 400);
        var skippedBalloonPixels = CountBluePixels(preview, left: 292, top: 242, width: 56, height: 36);
        var outsideBalloonPixels = CountBluePixels(preview, left: 292, top: 82, width: 56, height: 36);

        Assert.True(skippedBalloonPixels <= 2, $"Expected no meaningful blue pixels for the skipped table balloon, but found {skippedBalloonPixels}.");
        Assert.True(outsideBalloonPixels >= 25, $"Expected a visible outside blue balloon, but found only {outsideBalloonPixels} blue pixels.");
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static int CountBluePixels(PdfPagePreviewImage preview, int left, int top, int width, int height)
    {
        var count = 0;
        var right = Math.Min(preview.PixelWidth, left + width);
        var bottom = Math.Min(preview.PixelHeight, top + height);
        for (var y = Math.Max(0, top); y < bottom; y++)
        {
            for (var x = Math.Max(0, left); x < right; x++)
            {
                var offset = (y * preview.Stride) + (x * 4);
                var blue = preview.Pixels[offset];
                var green = preview.Pixels[offset + 1];
                var red = preview.Pixels[offset + 2];
                var alpha = preview.Pixels[offset + 3];
                if (alpha > 0 && blue > 120 && blue > red + 40 && blue > green + 40)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private string CreateJpegWithBlackRectangle(string fileName)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var image = new Image<Rgba32>(160, 120, Color.White);
        for (var y = 25; y < 75; y++)
        {
            for (var x = 20; x < 100; x++)
            {
                image[x, y] = Color.Black;
            }
        }

        image.SaveAsJpeg(path);
        return path;
    }

    private string CreateBlankPdf(string fileName)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(200);
        document.Save(path);
        return path;
    }

    private string CreatePdfWithVisibleText(string fileName, string text)
    {
        ArialFontResolver.Register();

        var path = Path.Combine(tempDirectory, fileName);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(200);
        using var graphics = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 12, XFontStyleEx.Regular);
        graphics.DrawString(text, font, XBrushes.Black, new XPoint(24, 80));
        graphics.DrawRectangle(XPens.Black, 24, 100, 60, 30);
        document.Save(path);
        return path;
    }

    private string CreatePdfWithVectorTable(string fileName)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(200);
        using var graphics = XGraphics.FromPdfPage(page);
        var pen = new XPen(XColors.Black, 1d);

        foreach (var x in new[] { 30d, 80d, 130d })
        {
            graphics.DrawLine(pen, x, 100d, x, 160d);
        }

        foreach (var y in new[] { 100d, 130d, 160d })
        {
            graphics.DrawLine(pen, 30d, y, 130d, y);
        }

        document.Save(path);
        return path;
    }
}
