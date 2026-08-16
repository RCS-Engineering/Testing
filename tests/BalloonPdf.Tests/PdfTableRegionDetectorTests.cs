using BalloonPdf.App.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class PdfTableRegionDetectorTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "BalloonPdfTests", Guid.NewGuid().ToString("N"));
    private readonly PdfTableRegionDetector detector = new();

    public PdfTableRegionDetectorTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void Detect_ReturnsRegionForVectorGridTable()
    {
        var inputPath = CreatePdf("grid-table.pdf", graphics =>
        {
            var pen = new XPen(XColors.Black, 1d);
            foreach (var x in new[] { 30d, 80d, 130d })
            {
                graphics.DrawLine(pen, x, 40d, x, 100d);
            }

            foreach (var y in new[] { 40d, 70d, 100d })
            {
                graphics.DrawLine(pen, 30d, y, 130d, y);
            }
        });

        var regions = detector.Detect(inputPath);
        var region = Assert.Single(regions);

        Assert.Equal(1, region.PageNumber);
        Assert.True(region.Contains(60, 130), "Expected the detected table region to contain an interior table-cell point.");
        Assert.False(region.Contains(160, 130), "Expected the detected table region to exclude points outside the table.");
    }

    [Fact]
    public void Detect_RejectsOrdinarySingleRectangle()
    {
        var inputPath = CreatePdf("single-rectangle.pdf", graphics =>
        {
            graphics.DrawRectangle(new XPen(XColors.Black, 1d), 30d, 90d, 100d, 60d);
        });

        var regions = detector.Detect(inputPath);

        Assert.Empty(regions);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string CreatePdf(string fileName, Action<XGraphics> draw)
    {
        var path = Path.Combine(tempDirectory, fileName);
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(200);
        using var graphics = XGraphics.FromPdfPage(page);
        draw(graphics);
        document.Save(path);
        return path;
    }
}
