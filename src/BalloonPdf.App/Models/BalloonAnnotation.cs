namespace BalloonPdf.App.Models;

public sealed record BalloonAnnotation(
    Guid Id,
    int PageNumber,
    string? SourceText,
    double CenterX,
    double CenterY,
    int BalloonNumber,
    string StrokeColorHex,
    double Radius)
{
    public const string DefaultStrokeColorHex = "#000000";

    public static BalloonAnnotation Create(
        int pageNumber,
        double centerX,
        double centerY,
        int balloonNumber,
        string? sourceText = null,
        string? strokeColorHex = null,
        double radius = 10d)
    {
        return new BalloonAnnotation(
            Guid.NewGuid(),
            pageNumber,
            sourceText,
            centerX,
            centerY,
            balloonNumber,
            string.IsNullOrWhiteSpace(strokeColorHex) ? DefaultStrokeColorHex : strokeColorHex,
            radius);
    }
}
