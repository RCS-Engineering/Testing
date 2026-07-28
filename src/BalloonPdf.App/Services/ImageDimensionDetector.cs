using BalloonPdf.App.Models;
using SixLabors.ImageSharp;

namespace BalloonPdf.App.Services;

public sealed class ImageDimensionDetector
{
    private readonly IImageTextExtractor textExtractor;

    public ImageDimensionDetector()
        : this(new TesseractImageTextExtractor())
    {
    }

    public ImageDimensionDetector(IImageTextExtractor textExtractor)
    {
        this.textExtractor = textExtractor ?? throw new ArgumentNullException(nameof(textExtractor));
    }

    public IReadOnlyList<DimensionCandidate> Detect(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        var imageInfo = Image.Identify(imagePath) ?? throw new InvalidOperationException("The selected image could not be decoded.");
        var pageWidth = imageInfo.Width;
        var pageHeight = imageInfo.Height;

        var candidates = textExtractor.ExtractWords(imagePath)
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .Select(word => ToCandidate(word, pageHeight))
            .Where(candidate => DimensionDetector.IsLikelyDimension(candidate.Text)
                && !DimensionDetector.IsInBottomRightDetailsBox(candidate.Left, candidate.Bottom, candidate.Right, candidate.Top, pageWidth, pageHeight))
            .ToList();

        return DimensionDetector.AssignReadingOrder(candidates);
    }

    private static DimensionCandidate ToCandidate(ImageTextWord word, int pageHeight)
    {
        var left = word.Left;
        var right = word.Right;
        var bottom = pageHeight - word.Bottom;
        var top = pageHeight - word.Top;
        return new DimensionCandidate(1, word.Text.Trim(), left, bottom, right, top, 0);
    }
}
