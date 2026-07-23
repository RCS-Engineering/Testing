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

    private static readonly IReadOnlyDictionary<char, int[]> DigitSegments = new Dictionary<char, int[]>
    {
        ['0'] = new[] { 0, 1, 2, 3, 4, 5 },
        ['1'] = new[] { 1, 2 },
        ['2'] = new[] { 0, 1, 6, 4, 3 },
        ['3'] = new[] { 0, 1, 6, 2, 3 },
        ['4'] = new[] { 5, 6, 1, 2 },
        ['5'] = new[] { 0, 5, 6, 2, 3 },
        ['6'] = new[] { 0, 5, 6, 4, 2, 3 },
        ['7'] = new[] { 0, 1, 2 },
        ['8'] = new[] { 0, 1, 2, 3, 4, 5, 6 },
        ['9'] = new[] { 0, 1, 2, 3, 5, 6 }
    };

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
        var text = Math.Max(0, balloonNumber).ToString(CultureInfo.InvariantCulture);
        var digitHeight = bounds.Height * 0.62d;
        var digitWidth = Math.Min(bounds.Width / Math.Max(1.8d, text.Length + 0.35d), digitHeight * 0.48d);
        var spacing = digitWidth * 0.24d;
        var totalWidth = (digitWidth * text.Length) + (spacing * Math.Max(0, text.Length - 1));
        var left = bounds.X + ((bounds.Width - totalWidth) / 2d);
        var top = bounds.Y + ((bounds.Height - digitHeight) / 2d);
        var numberPen = new XPen(pen.Color, Math.Max(0.8d, bounds.Height * 0.055d));

        for (var index = 0; index < text.Length; index++)
        {
            DrawDigit(graphics, numberPen, text[index], left + (index * (digitWidth + spacing)), top, digitWidth, digitHeight);
        }
    }

    private static void DrawDigit(XGraphics graphics, XPen pen, char digit, double x, double y, double width, double height)
    {
        if (!DigitSegments.TryGetValue(digit, out var segments))
        {
            return;
        }

        var middleY = y + (height / 2d);
        var right = x + width;
        var bottom = y + height;
        var inset = width * 0.12d;

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case 0:
                    graphics.DrawLine(pen, x + inset, y, right - inset, y);
                    break;
                case 1:
                    graphics.DrawLine(pen, right, y + inset, right, middleY - inset);
                    break;
                case 2:
                    graphics.DrawLine(pen, right, middleY + inset, right, bottom - inset);
                    break;
                case 3:
                    graphics.DrawLine(pen, x + inset, bottom, right - inset, bottom);
                    break;
                case 4:
                    graphics.DrawLine(pen, x, middleY + inset, x, bottom - inset);
                    break;
                case 5:
                    graphics.DrawLine(pen, x, y + inset, x, middleY - inset);
                    break;
                case 6:
                    graphics.DrawLine(pen, x + inset, middleY, right - inset, middleY);
                    break;
            }
        }
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
