using System.Globalization;
using System.IO;
using BalloonPdf.App.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SixLabors.ImageSharp;

namespace BalloonPdf.App.Services;

public sealed class PdfBalloonAnnotator
{
    internal const double BalloonRadius = 10d;
    internal const double BalloonOffsetX = 20d;
    internal const double MinimumMargin = 12d;
    internal const double StrokeWidth = 1.2d;
    internal const string BalloonFontFamily = "Arial";
    internal const double BalloonFontSize = 9d;

    private readonly BalloonAnnotationService annotationService = new();

    public void AddBalloons(string inputPath, string outputPdfPath, IReadOnlyCollection<DimensionCandidate> dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        AddBalloons(inputPath, outputPdfPath, annotationService.CreateFromDimensions(dimensions));
    }

    public void AddBalloons(string inputPath, string outputPdfPath, IReadOnlyCollection<BalloonAnnotation> annotations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPdfPath);
        ArgumentNullException.ThrowIfNull(annotations);

        var inputFullPath = Path.GetFullPath(inputPath);
        var outputFullPath = Path.GetFullPath(outputPdfPath);
        if (inputFullPath.Equals(outputFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The output PDF path must be different from the input path.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var output = new PdfDocument();
        switch (InputDocumentFormatExtensions.FromPath(inputFullPath))
        {
            case InputDocumentFormat.Pdf:
                AddPdfPages(inputFullPath, output, annotations);
                break;
            case InputDocumentFormat.Jpeg:
                AddImagePage(inputFullPath, output, annotations);
                break;
            default:
                throw new NotSupportedException("Supported input formats are PDF, JPG, and JPEG.");
        }

        output.Save(outputFullPath);
    }

    private static void AddPdfPages(string inputFullPath, PdfDocument output, IReadOnlyCollection<BalloonAnnotation> annotations)
    {
        using var input = PdfReader.Open(inputFullPath, PdfDocumentOpenMode.Import);
        output.Info.Title = input.Info.Title;
        output.Info.Author = input.Info.Author;
        output.Info.Subject = input.Info.Subject;
        output.Info.Keywords = input.Info.Keywords;

        for (var pageIndex = 0; pageIndex < input.PageCount; pageIndex++)
        {
            var page = output.AddPage(input.Pages[pageIndex]);
            DrawPageBalloons(page, annotations.Where(annotation => annotation.PageNumber == pageIndex + 1));
        }
    }

    private static void AddImagePage(string inputFullPath, PdfDocument output, IReadOnlyCollection<BalloonAnnotation> annotations)
    {
        var imageInfo = Image.Identify(inputFullPath) ?? throw new InvalidOperationException("The selected image could not be decoded.");
        var page = output.AddPage();
        page.Width = XUnit.FromPoint(imageInfo.Width);
        page.Height = XUnit.FromPoint(imageInfo.Height);

        using (var graphics = XGraphics.FromPdfPage(page))
        using (var image = XImage.FromFile(inputFullPath))
        {
            graphics.DrawImage(image, 0d, 0d, imageInfo.Width, imageInfo.Height);
        }

        DrawPageBalloons(page, annotations.Where(annotation => annotation.PageNumber == 1));
    }

    private static void DrawPageBalloons(PdfPage page, IEnumerable<BalloonAnnotation> annotations)
    {
        var pageAnnotations = annotations.ToList();
        if (pageAnnotations.Count == 0)
        {
            return;
        }

        var pageWidth = page.Width.Point;
        var pageHeight = page.Height.Point;
        using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        var brush = XBrushes.White;

        foreach (var annotation in pageAnnotations)
        {
            var center = ConvertToPdfSharpPoint(annotation, pageWidth, pageHeight);
            var radius = annotation.Radius <= 0d ? BalloonRadius : annotation.Radius;
            var bounds = new XRect(
                center.X - radius,
                center.Y - radius,
                radius * 2d,
                radius * 2d);
            var strokeColor = ParseColor(annotation.StrokeColorHex);
            var pen = new XPen(strokeColor, StrokeWidth);

            DrawBalloonArrow(graphics, pen, annotation, center, radius, pageHeight);
            graphics.DrawEllipse(pen, brush, bounds);
            DrawBalloonNumber(graphics, pen, bounds, annotation.BalloonNumber);
        }
    }

    internal static XPoint ConvertToPdfSharpPoint(DimensionCandidate dimension, double pageWidth, double pageHeight)
    {
        var desiredX = dimension.Right + BalloonOffsetX;
        var desiredY = pageHeight - dimension.CenterY;

        return ClampToPage(desiredX, desiredY, pageWidth, pageHeight);
    }

    internal static XPoint ConvertToPdfSharpPoint(BalloonAnnotation annotation, double pageWidth, double pageHeight)
    {
        var desiredX = annotation.CenterX;
        var desiredY = pageHeight - annotation.CenterY;

        return ClampToPage(desiredX, desiredY, pageWidth, pageHeight);
    }

    private static XPoint ClampToPage(double desiredX, double desiredY, double pageWidth, double pageHeight)
    {
        var x = Math.Clamp(desiredX, MinimumMargin, pageWidth - MinimumMargin);
        var y = Math.Clamp(desiredY, MinimumMargin, pageHeight - MinimumMargin);
        return new XPoint(x, y);
    }

    private static void DrawBalloonArrow(
        XGraphics graphics,
        XPen pen,
        BalloonAnnotation annotation,
        XPoint center,
        double radius,
        double pageHeight)
    {
        if (annotation.TargetX is not { } targetX || annotation.TargetY is not { } targetY)
        {
            return;
        }

        var target = new XPoint(targetX, pageHeight - targetY);
        var deltaX = target.X - center.X;
        var deltaY = target.Y - center.Y;
        var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= radius || distance <= double.Epsilon)
        {
            return;
        }

        var unitX = deltaX / distance;
        var unitY = deltaY / distance;
        var start = new XPoint(center.X + (unitX * radius), center.Y + (unitY * radius));
        graphics.DrawLine(pen, start, target);

        const double arrowHeadLength = 6d;
        const double arrowHeadAngle = Math.PI / 6d;
        DrawArrowHeadLine(graphics, pen, target, unitX, unitY, arrowHeadLength, arrowHeadAngle);
        DrawArrowHeadLine(graphics, pen, target, unitX, unitY, arrowHeadLength, -arrowHeadAngle);
    }

    private static void DrawArrowHeadLine(
        XGraphics graphics,
        XPen pen,
        XPoint target,
        double unitX,
        double unitY,
        double length,
        double angle)
    {
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var x = (unitX * cos) - (unitY * sin);
        var y = (unitX * sin) + (unitY * cos);
        var end = new XPoint(target.X - (x * length), target.Y - (y * length));
        graphics.DrawLine(pen, target, end);
    }

    private static void DrawBalloonNumber(XGraphics graphics, XPen pen, XRect bounds, int balloonNumber)
    {
        var text = Math.Max(0, balloonNumber).ToString(CultureInfo.InvariantCulture);
        var scaledFontSize = BalloonFontSize * (bounds.Height / (BalloonRadius * 2d));
        var widthFitFontSize = bounds.Width / Math.Max(1.2d, text.Length * 0.62d);
        var fontSize = Math.Max(1d, Math.Min(scaledFontSize, widthFitFontSize));
        var font = CreateBalloonNumberFont(fontSize);
        var brush = new XSolidBrush(pen.Color);

        graphics.DrawString(text, font, brush, bounds, XStringFormats.Center);
    }

    private static XFont CreateBalloonNumberFont(double fontSize)
    {
        ArialFontResolver.Register();

        var options = new XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.EmbedCompleteFontFile);
        return new XFont(BalloonFontFamily, fontSize, XFontStyleEx.Bold, options);
    }

    private static XColor ParseColor(string strokeColorHex)
    {
        var color = string.IsNullOrWhiteSpace(strokeColorHex)
            ? BalloonAnnotation.DefaultStrokeColorHex
            : strokeColorHex.Trim();
        if (!color.StartsWith('#'))
        {
            color = $"#{color}";
        }

        if (color.Length != 7
            || !int.TryParse(color.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
            || !int.TryParse(color.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
            || !int.TryParse(color.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
        {
            throw new ArgumentException("Balloon colors must use #RRGGBB hex format.", nameof(strokeColorHex));
        }

        return XColor.FromArgb(red, green, blue);
    }
}
