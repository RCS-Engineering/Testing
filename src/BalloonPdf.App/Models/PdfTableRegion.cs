namespace BalloonPdf.App.Models;

public sealed record PdfTableRegion(
    int PageNumber,
    double Left,
    double Bottom,
    double Right,
    double Top)
{
    public double Width => Right - Left;

    public double Height => Top - Bottom;

    public bool Contains(double x, double y, double tolerance = 1d)
    {
        return x >= Left - tolerance
            && x <= Right + tolerance
            && y >= Bottom - tolerance
            && y <= Top + tolerance;
    }
}
