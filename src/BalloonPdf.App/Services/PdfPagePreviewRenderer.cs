using System.IO;
using Docnet.Core;
using Docnet.Core.Models;

namespace BalloonPdf.App.Services;

public sealed class PdfPagePreviewRenderer
{
    public PdfPagePreviewImage RenderPage(string pdfPath, int pageNumber, int pixelWidth, int pixelHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("The PDF preview file was not found.", pdfPath);
        }

        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page numbers are one-based and must be positive.");
        }

        if (pixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Preview pixel width must be positive.");
        }

        if (pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), "Preview pixel height must be positive.");
        }

        using var documentReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(pixelWidth, pixelHeight));
        var pageCount = documentReader.GetPageCount();
        if (pageNumber > pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), $"Page {pageNumber} is outside the PDF page range 1-{pageCount}.");
        }

        using var pageReader = documentReader.GetPageReader(pageNumber - 1);
        var pageWidth = pageReader.GetPageWidth();
        var pageHeight = pageReader.GetPageHeight();
        var pixels = pageReader.GetImage();

        return new PdfPagePreviewImage(pixels, pageWidth, pageHeight, pageWidth * 4, PdfPagePreviewPixelFormat.Bgra32);
    }
}

public sealed record PdfPagePreviewImage(
    byte[] Pixels,
    int PixelWidth,
    int PixelHeight,
    int Stride,
    PdfPagePreviewPixelFormat PixelFormat);

public enum PdfPagePreviewPixelFormat
{
    Bgra32
}
