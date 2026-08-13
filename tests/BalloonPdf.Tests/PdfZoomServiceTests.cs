using BalloonPdf.App.Services;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class PdfZoomServiceTests
{
    [Fact]
    public void DefaultZoom_IsOneHundredPercent()
    {
        Assert.Equal(1d, PdfZoomService.DefaultZoom);
        Assert.Equal(100, PdfZoomService.ToPercent(PdfZoomService.DefaultZoom));
    }

    [Fact]
    public void ZoomIn_AddsTwentyFivePercentStep()
    {
        var zoom = PdfZoomService.ZoomIn(PdfZoomService.DefaultZoom);

        Assert.Equal(1.25d, zoom);
        Assert.Equal(125, PdfZoomService.ToPercent(zoom));
    }

    [Fact]
    public void ZoomOut_SubtractsTwentyFivePercentStep()
    {
        var zoom = PdfZoomService.ZoomOut(PdfZoomService.DefaultZoom);

        Assert.Equal(0.75d, zoom);
        Assert.Equal(75, PdfZoomService.ToPercent(zoom));
    }

    [Fact]
    public void ZoomOut_ClampsAtMinimumZoom()
    {
        var zoom = PdfZoomService.MinimumZoom;

        for (var count = 0; count < 10; count++)
        {
            zoom = PdfZoomService.ZoomOut(zoom);
        }

        Assert.Equal(PdfZoomService.MinimumZoom, zoom);
        Assert.Equal(25, PdfZoomService.ToPercent(zoom));
    }

    [Fact]
    public void ZoomIn_ClampsAtMaximumZoom()
    {
        var zoom = PdfZoomService.MaximumZoom;

        for (var count = 0; count < 10; count++)
        {
            zoom = PdfZoomService.ZoomIn(zoom);
        }

        Assert.Equal(PdfZoomService.MaximumZoom, zoom);
        Assert.Equal(400, PdfZoomService.ToPercent(zoom));
    }

    [Theory]
    [InlineData(1.234, 123)]
    [InlineData(1.235, 124)]
    [InlineData(-1, 25)]
    [InlineData(5, 400)]
    public void ToPercent_RoundsAndClampsZoom(double zoom, int expectedPercent)
    {
        Assert.Equal(expectedPercent, PdfZoomService.ToPercent(zoom));
    }
}
