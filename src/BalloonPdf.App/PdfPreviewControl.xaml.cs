using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using BalloonPdf.App.Models;
using BalloonPdf.App.Services;
using PdfSharp.Pdf.IO;

namespace BalloonPdf.App;

public sealed partial class PdfPreviewControl : UserControl
{
    private const double MaximumPageDisplayWidth = 900d;

    private readonly List<(double Width, double Height)> pageSizes = new();
    private readonly PdfPagePreviewRenderer pagePreviewRenderer = new();
    private IReadOnlyCollection<BalloonAnnotation> annotations = Array.Empty<BalloonAnnotation>();
    private string? currentPdfPath;
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
        currentPdfPath = null;
        currentPageNumber = 1;

        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
        {
            RenderEmpty("Choose and generate a PDF to preview balloons.");
            return;
        }

        currentPdfPath = pdfPath;
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

        if (!TryAddRenderedPageImage(displayWidth, displayHeight, out var previewErrorMessage))
        {
            AddFallbackPageLabel(previewErrorMessage ?? $"Page {currentPageNumber}");
        }

        foreach (var annotation in annotations.Where(annotation => annotation.PageNumber == currentPageNumber))
        {
            AddAnnotationOverlay(annotation, pageSize.Height);
        }
    }

    private bool TryAddRenderedPageImage(double displayWidth, double displayHeight, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(currentPdfPath))
        {
            return false;
        }

        try
        {
            var pixelWidth = Math.Max(1, (int)Math.Round(displayWidth));
            var pixelHeight = Math.Max(1, (int)Math.Round(displayHeight));
            var preview = pagePreviewRenderer.RenderPage(currentPdfPath, currentPageNumber, pixelWidth, pixelHeight);
            var bitmap = BitmapSource.Create(
                preview.PixelWidth,
                preview.PixelHeight,
                96d,
                96d,
                PixelFormats.Bgra32,
                null,
                preview.Pixels,
                preview.Stride);

            var image = new Image
            {
                Source = bitmap,
                Width = displayWidth,
                Height = displayHeight,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(image, 0d);
            Canvas.SetTop(image, 0d);
            PageCanvas.Children.Add(image);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Preview unavailable: {ex.Message}";
            return false;
        }
    }

    private void AddFallbackPageLabel(string message)
    {
        var pageLabel = new TextBlock
        {
            Text = message,
            Foreground = Brushes.LightGray,
            FontSize = 18d,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = Math.Max(0d, PageCanvas.Width - 32d),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(pageLabel, 16d);
        Canvas.SetTop(pageLabel, 16d);
        PageCanvas.Children.Add(pageLabel);
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
