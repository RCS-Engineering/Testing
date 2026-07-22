namespace BalloonPdf.App.Models;

public sealed record DimensionCandidate(
    int PageNumber,
    string Text,
    double Left,
    double Bottom,
    double Right,
    double Top,
    int BalloonNumber)
{
    public double Width => Right - Left;

    public double Height => Top - Bottom;

    public double CenterX => Left + (Width / 2d);

    public double CenterY => Bottom + (Height / 2d);
}
