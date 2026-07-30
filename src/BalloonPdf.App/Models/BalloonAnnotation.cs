namespace BalloonPdf.App.Models;

public sealed record BalloonAnnotation(
    Guid Id,
    int PageNumber,
    string? SourceText,
    double CenterX,
    double CenterY,
    int BalloonNumber,
    string StrokeColorHex,
    double Radius,
    double? TargetX,
    double? TargetY)
{
    public const string DefaultStrokeColorHex = "#0000FF";

    public static BalloonAnnotation Create(
        int pageNumber,
        double centerX,
        double centerY,
        int balloonNumber,
        string? sourceText = null,
        string? strokeColorHex = null,
        double radius = 10d,
        double? targetX = null,
        double? targetY = null)
    {
        return new BalloonAnnotation(
            Guid.NewGuid(),
            pageNumber,
            sourceText,
            centerX,
            centerY,
            balloonNumber,
            string.IsNullOrWhiteSpace(strokeColorHex) ? DefaultStrokeColorHex : strokeColorHex,
            radius,
            targetX,
            targetY);
    }
}
