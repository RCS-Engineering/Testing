using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BalloonPdf.App.Models;
using PdfSharp.Pdf.IO;

namespace BalloonPdf.App;

public sealed partial class PdfPreviewControl : UserControl
{
    private const double MaximumPageDisplayWidth = 900d;

    private readonly List<(double Width, double Height)> pageSizes = new();
    private IReadOnlyCollection<BalloonAnnotation> annotations = Array.Empty<BalloonAnnotation>();
    private int currentPageNumber = 1;
    private double currentScale = 1d;

    public PdfPreviewControl()
    {
        InitializeComponent();
    }

    public event EventHandler<BalloonPreviewClickEventArgs>? PageClicked;

    public event EventHandler<BalloonPreviewSelectionEventArgs>? BalloonClicked;

    public int CurrentPageNumber => pageSizes.Count == 0 ? 0 : currentPageNumber;

    public int PageCount => pageSizes.Count;

    public void LoadPdf(string? pdfPath, IReadOnlyCollection<BalloonAnnotation>? pageAnnotations = null)
    {
        annotations = pageAnnotations ?? Array.Empty<BalloonAnnotation>();
        pageSizes.Clear();
        currentPageNumber = 1;

        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
        {
            RenderEmpty("Choose and generate a PDF to preview balloons.");
            return;
        }

        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        for (var index = 0; index < document.PageCount; index++)
        {
            var page = document.Pages[index];
            pageSizes.Add((page.Width.Point, page.Height.Point));
        }

        RenderCurrentPage();
    }

    public void LoadAnnotations(IReadOnlyCollection<BalloonAnnotation> pageAnnotations)
    {
        annotations = pageAnnotations;
        RenderCurrentPage();
    }

    public bool NextPage()
    {
        if (currentPageNumber >= pageSizes.Count)
        {
            return false;
        }

        currentPageNumber++;
        RenderCurrentPage();
        return true;
    }

    public bool PreviousPage()
    {
        if (currentPageNumber <= 1)
        {
            return false;
        }

        currentPageNumber--;
        RenderCurrentPage();
        return true;
    }

    private void RenderCurrentPage()
    {
        PageCanvas.Children.Clear();
        if (pageSizes.Count == 0)
        {
            RenderEmpty("Choose and generate a PDF to preview balloons.");
            return;
        }

        EmptyTextBlock.Visibility = Visibility.Collapsed;
        var pageSize = pageSizes[currentPageNumber - 1];
        currentScale = Math.Min(1d, MaximumPageDisplayWidth / pageSize.Width);
        var displayWidth = pageSize.Width * currentScale;
        var displayHeight = pageSize.Height * currentScale;
        PageCanvas.Width = displayWidth;
        PageCanvas.Height = displayHeight;

        var pageBackground = new Rectangle
        {
            Width = displayWidth,
            Height = displayHeight,
            Fill = Brushes.White,
            Stroke = Brushes.LightGray,
            StrokeThickness = 1d,
            IsHitTestVisible = false
        };
        PageCanvas.Children.Add(pageBackground);

        var pageLabel = new TextBlock
        {
            Text = $"Page {currentPageNumber}",
            Foreground = Brushes.LightGray,
            FontSize = 18d,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(pageLabel, 16d);
        Canvas.SetTop(pageLabel, 16d);
        PageCanvas.Children.Add(pageLabel);

        foreach (var annotation in annotations.Where(annotation => annotation.PageNumber == currentPageNumber))
        {
            AddAnnotationOverlay(annotation, pageSize.Height);
        }
    }

    private void AddAnnotationOverlay(BalloonAnnotation annotation, double pageHeight)
    {
        var radius = annotation.Radius <= 0d ? 10d : annotation.Radius;
        var displayRadius = radius * currentScale;
        var x = annotation.CenterX * currentScale;
        var y = (pageHeight - annotation.CenterY) * currentScale;
        var brush = CreateBrush(annotation.StrokeColorHex);

        var ellipse = new Ellipse
        {
            Width = displayRadius * 2d,
            Height = displayRadius * 2d,
            Stroke = brush,
            StrokeThickness = 1.5d,
            Fill = Brushes.White,
            Tag = annotation.Id,
            Cursor = Cursors.Hand
        };
        ellipse.MouseLeftButtonDown += BalloonElement_MouseLeftButtonDown;
        Canvas.SetLeft(ellipse, x - displayRadius);
        Canvas.SetTop(ellipse, y - displayRadius);
        PageCanvas.Children.Add(ellipse);

        var label = new TextBlock
        {
            Text = annotation.BalloonNumber.ToString(),
            Foreground = brush,
            FontWeight = FontWeights.Bold,
            FontSize = Math.Max(9d, 9d * currentScale),
            TextAlignment = TextAlignment.Center,
            Width = displayRadius * 2d,
            Height = displayRadius * 2d,
            Tag = annotation.Id,
            Cursor = Cursors.Hand
        };
        label.MouseLeftButtonDown += BalloonElement_MouseLeftButtonDown;
        Canvas.SetLeft(label, x - displayRadius);
        Canvas.SetTop(label, y - (label.FontSize / 1.3d));
        PageCanvas.Children.Add(label);
    }

    private void PageCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (pageSizes.Count == 0)
        {
            return;
        }

        var point = e.GetPosition(PageCanvas);
        var pageSize = pageSizes[currentPageNumber - 1];
        if (point.X < 0 || point.Y < 0 || point.X > PageCanvas.Width || point.Y > PageCanvas.Height)
        {
            return;
        }

        PageClicked?.Invoke(this, new BalloonPreviewClickEventArgs(
            currentPageNumber,
            point.X / currentScale,
            pageSize.Height - (point.Y / currentScale)));
    }

    private void BalloonElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid annotationId })
        {
            BalloonClicked?.Invoke(this, new BalloonPreviewSelectionEventArgs(annotationId));
            e.Handled = true;
        }
    }

    private void RenderEmpty(string message)
    {
        PageCanvas.Children.Clear();
        PageCanvas.Width = 0d;
        PageCanvas.Height = 0d;
        EmptyTextBlock.Text = message;
        EmptyTextBlock.Visibility = Visibility.Visible;
    }

    private static Brush CreateBrush(string color)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(color)!;
        }
        catch (FormatException)
        {
            return Brushes.Black;
        }
    }
}

public sealed class BalloonPreviewClickEventArgs(int pageNumber, double pdfX, double pdfY) : EventArgs
{
    public int PageNumber { get; } = pageNumber;

    public double PdfX { get; } = pdfX;

    public double PdfY { get; } = pdfY;
}

public sealed class BalloonPreviewSelectionEventArgs(Guid annotationId) : EventArgs
{
    public Guid AnnotationId { get; } = annotationId;
}
