using System.Globalization;
using System.IO;
using BalloonPdf.App.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

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

    public void AddBalloons(string inputPdfPath, string outputPdfPath, IReadOnlyCollection<DimensionCandidate> dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        AddBalloons(inputPdfPath, outputPdfPath, annotationService.CreateFromDimensions(dimensions));
    }

    public void AddBalloons(string inputPdfPath, string outputPdfPath, IReadOnlyCollection<BalloonAnnotation> annotations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPdfPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPdfPath);
        ArgumentNullException.ThrowIfNull(annotations);

        var inputFullPath = Path.GetFullPath(inputPdfPath);
        var outputFullPath = Path.GetFullPath(outputPdfPath);
        if (inputFullPath.Equals(outputFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The output PDF path must be different from the input PDF path.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var input = PdfReader.Open(inputFullPath, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();
        output.Info.Title = input.Info.Title;
        output.Info.Author = input.Info.Author;
        output.Info.Subject = input.Info.Subject;
        output.Info.Keywords = input.Info.Keywords;

        for (var pageIndex = 0; pageIndex < input.PageCount; pageIndex++)
        {
            var page = output.AddPage(input.Pages[pageIndex]);
            DrawPageBalloons(page, annotations.Where(annotation => annotation.PageNumber == pageIndex + 1));
        }

        output.Save(outputFullPath);
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

    private static void DrawBalloonNumber(XGraphics graphics, XPen pen, XRect bounds, int balloonNumber)
    {
        ArialFontResolver.Register();

        var text = Math.Max(0, balloonNumber).ToString(CultureInfo.InvariantCulture);
        var scaledFontSize = BalloonFontSize * (bounds.Height / (BalloonRadius * 2d));
        var widthFitFontSize = bounds.Width / Math.Max(1.2d, text.Length * 0.62d);
        var fontSize = Math.Max(1d, Math.Min(scaledFontSize, widthFitFontSize));
        var font = new XFont(BalloonFontFamily, fontSize, XFontStyleEx.Bold);
        var brush = new XSolidBrush(pen.Color);

        graphics.DrawString(text, font, brush, bounds, XStringFormats.Center);
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
