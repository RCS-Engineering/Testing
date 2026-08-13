namespace BalloonPdf.App.Services;

public static class PdfZoomService
{
    public const double DefaultZoom = 1d;
    public const double MinimumZoom = 0.25d;
    public const double MaximumZoom = 4d;
    public const double ZoomStep = 0.25d;

    public static double ZoomIn(double currentZoom)
    {
        return Clamp(currentZoom + ZoomStep);
    }

    public static double ZoomOut(double currentZoom)
    {
        return Clamp(currentZoom - ZoomStep);
    }

    public static int ToPercent(double currentZoom)
    {
        return (int)Math.Round(Clamp(currentZoom) * 100d, MidpointRounding.AwayFromZero);
    }

    public static double Clamp(double zoom)
    {
        if (double.IsNaN(zoom) || double.IsInfinity(zoom))
        {
            return DefaultZoom;
        }

        return Math.Clamp(zoom, MinimumZoom, MaximumZoom);
    }
}
