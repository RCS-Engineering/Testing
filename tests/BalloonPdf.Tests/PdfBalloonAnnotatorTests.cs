using System.IO;
using BalloonPdf.App.Models;
using BalloonPdf.App.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
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

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string CreateBlankPdf(string fileName)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = 200;
        page.Height = 200;
        document.Save(path);
        return path;
    }

    private string CreatePdfWithVisibleText(string fileName, string text)
    {
        ArialFontResolver.Register();

        var path = Path.Combine(tempDirectory, fileName);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = 200;
        page.Height = 200;
        using var graphics = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 12, XFontStyleEx.Regular);
        graphics.DrawString(text, font, XBrushes.Black, new XPoint(24, 80));
        graphics.DrawRectangle(XPens.Black, 24, 100, 60, 30);
        document.Save(path);
        return path;
    }
}
