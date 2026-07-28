using System.IO;
using Docnet.Core;
using Docnet.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BalloonPdf.App.Services;

public sealed class PdfPagePreviewRenderer
{
    public PdfPagePreviewImage RenderPage(string inputPath, int pageNumber, int pixelWidth, int pixelHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("The preview file was not found.", inputPath);
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

        return InputDocumentFormatExtensions.FromPath(inputPath) switch
        {
            InputDocumentFormat.Pdf => RenderPdfPage(inputPath, pageNumber, pixelWidth, pixelHeight),
            InputDocumentFormat.Jpeg => RenderImage(inputPath, pageNumber),
            _ => throw new NotSupportedException("Supported input formats are PDF, JPG, and JPEG.")
        };
    }

    private static PdfPagePreviewImage RenderPdfPage(string pdfPath, int pageNumber, int pixelWidth, int pixelHeight)
    {
        var pageDimensions = new PageDimensions(Math.Min(pixelWidth, pixelHeight), Math.Max(pixelWidth, pixelHeight));
        using var documentReader = DocLib.Instance.GetDocReader(pdfPath, pageDimensions);
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

    private static PdfPagePreviewImage RenderImage(string imagePath, int pageNumber)
    {
        if (pageNumber != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "JPEG inputs are single-page documents. Page number must be 1.");
        }

        using var image = Image.Load<Rgba32>(imagePath);
        var stride = image.Width * 4;
        var pixels = new byte[stride * image.Height];

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    var offset = (y * stride) + (x * 4);
                    pixels[offset] = pixel.B;
                    pixels[offset + 1] = pixel.G;
                    pixels[offset + 2] = pixel.R;
                    pixels[offset + 3] = pixel.A;
                }
            }
        });

        return new PdfPagePreviewImage(pixels, image.Width, image.Height, stride, PdfPagePreviewPixelFormat.Bgra32);
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
