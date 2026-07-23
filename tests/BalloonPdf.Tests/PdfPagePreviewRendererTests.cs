using System.IO;
using BalloonPdf.App.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class PdfPagePreviewRendererTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "BalloonPdfTests", Guid.NewGuid().ToString("N"));
    private readonly PdfPagePreviewRenderer renderer = new();

    public PdfPagePreviewRendererTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void RenderPage_ReturnsPagePixelsWithVisibleSourceContent()
    {
        var pdfPath = CreatePdfWithBlackRectangle("preview-source.pdf");

        var preview = renderer.RenderPage(pdfPath, pageNumber: 1, pixelWidth: 120, pixelHeight: 120);

        AssertPreviewContainsVisibleSourceContent(preview, expectedWidth: 120, expectedHeight: 120);
    }

    [Fact]
    public void RenderPage_AcceptsLandscapeRequestAndReturnsVisibleSourceContent()
    {
        var pdfPath = CreateLandscapePdfWithBlackRectangle("landscape-preview-source.pdf");

        var preview = renderer.RenderPage(pdfPath, pageNumber: 1, pixelWidth: 200, pixelHeight: 100);

        Assert.Equal(preview.PixelWidth * 4, preview.Stride);
        Assert.Equal(PdfPagePreviewPixelFormat.Bgra32, preview.PixelFormat);
        Assert.Equal(preview.Stride * preview.PixelHeight, preview.Pixels.Length);
        Assert.True(preview.PixelWidth > preview.PixelHeight);
        AssertPreviewContainsVisibleSourceContent(preview, expectedWidth: preview.PixelWidth, expectedHeight: preview.PixelHeight);
    }

    [Fact]
    public void RenderPage_RejectsInvalidPageNumber()
    {
        var pdfPath = CreatePdfWithBlackRectangle("preview-source.pdf");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => renderer.RenderPage(pdfPath, pageNumber: 2, pixelWidth: 120, pixelHeight: 120));
        Assert.Equal("pageNumber", exception.ParamName);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static void AssertPreviewContainsVisibleSourceContent(PdfPagePreviewImage preview, int expectedWidth, int expectedHeight)
    {
        Assert.Equal(expectedWidth, preview.PixelWidth);
        Assert.Equal(expectedHeight, preview.PixelHeight);
        Assert.Equal(preview.PixelWidth * 4, preview.Stride);
        Assert.Equal(PdfPagePreviewPixelFormat.Bgra32, preview.PixelFormat);
        Assert.Equal(preview.Stride * preview.PixelHeight, preview.Pixels.Length);
        Assert.Contains(Enumerable.Range(0, preview.Pixels.Length / 4), pixelIndex =>
        {
            var offset = pixelIndex * 4;
            var blue = preview.Pixels[offset];
            var green = preview.Pixels[offset + 1];
            var red = preview.Pixels[offset + 2];
            var alpha = preview.Pixels[offset + 3];
            return alpha > 0 && red < 245 && green < 245 && blue < 245;
        });
    }

    private string CreatePdfWithBlackRectangle(string fileName)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(120);
        page.Height = XUnit.FromPoint(120);
        using var graphics = XGraphics.FromPdfPage(page);
        graphics.DrawRectangle(XBrushes.Black, 20, 20, 60, 40);
        document.Save(path);
        return path;
    }

    private string CreateLandscapePdfWithBlackRectangle(string fileName)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(100);
        using var graphics = XGraphics.FromPdfPage(page);
        graphics.DrawRectangle(XBrushes.Black, 20, 20, 80, 40);
        document.Save(path);
        return path;
    }
}
