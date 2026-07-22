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

    public void AddBalloons(string inputPdfPath, string outputPdfPath, IReadOnlyCollection<DimensionCandidate> dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPdfPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPdfPath);
        ArgumentNullException.ThrowIfNull(dimensions);

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
            DrawPageBalloons(page, dimensions.Where(dimension => dimension.PageNumber == pageIndex + 1));
        }

        output.Save(outputFullPath);
    }

    private static void DrawPageBalloons(PdfPage page, IEnumerable<DimensionCandidate> dimensions)
    {
        var pageWidth = page.Width.Point;
        var pageHeight = page.Height.Point;
        using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        var pen = new XPen(XColors.Black, StrokeWidth);
        var brush = XBrushes.White;
        var textBrush = XBrushes.Black;
        var font = new XFont(BalloonFontFamily, BalloonFontSize, XFontStyleEx.Bold);

        foreach (var dimension in dimensions)
        {
            var center = ConvertToPdfSharpPoint(dimension, pageWidth, pageHeight);
            var bounds = new XRect(
                center.X - BalloonRadius,
                center.Y - BalloonRadius,
                BalloonRadius * 2d,
                BalloonRadius * 2d);

            graphics.DrawEllipse(pen, brush, bounds);
            graphics.DrawString(dimension.BalloonNumber.ToString(), font, textBrush, bounds, XStringFormats.Center);
        }
    }

    internal static XPoint ConvertToPdfSharpPoint(DimensionCandidate dimension, double pageWidth, double pageHeight)
    {
        var desiredX = dimension.Right + BalloonOffsetX;
        var desiredY = pageHeight - dimension.CenterY;

        var x = Math.Clamp(desiredX, MinimumMargin, pageWidth - MinimumMargin);
        var y = Math.Clamp(desiredY, MinimumMargin, pageHeight - MinimumMargin);
        return new XPoint(x, y);
    }
}
