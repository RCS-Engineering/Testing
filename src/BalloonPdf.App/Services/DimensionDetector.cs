using System.IO;
using System.Text.RegularExpressions;
using BalloonPdf.App.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace BalloonPdf.App.Services;

public sealed class DimensionDetector
{
    private const double DetailsBoxMinimumCenterXRatio = 0.72d;
    private const double DetailsBoxMaximumCenterYRatio = 0.25d;
    private const double BorderExclusionRatio = 0.04d;
    private const double BottomWatermarkBandMaximumCenterYRatio = 0.12d;
    private const int SparsePdfPageDimensionThreshold = 1;
    private const double OcrFallbackRenderScale = 2d;
    private const int OcrFallbackMaximumPixelSide = 2400;

    private static readonly Regex DimensionRegex = new(
        @"^(?:(?:Ø|%%c|dia\.?|diam\.?|r)\s*(?:\.\d+|\d+(?:\.\d+)?|\d+\s*/\s*\d+)|(?:\d+\s*x\s*)?m\s*\d+|(?:\.\d+|\d+\.\d+|\d+\s*/\s*\d+)|\d+(?:\.\d+)?\s*(?:°|deg|degrees)|(?:\.\d+|\d+(?:\.\d+)?|\d+\s*/\s*\d+)\s*(?:±|\+/-)\s*(?:\.\d+|\d+(?:\.\d+)?|\d+\s*/\s*\d+))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex LeadingOcrDiameterRegex = new(
        @"^[oO](\s*)(?=(?:\.\d+|\d))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WholeNumberRegex = new(@"^\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ImageDimensionDetector imageDimensionDetector;

    public DimensionDetector()
        : this(new ImageDimensionDetector())
    {
    }

    public DimensionDetector(ImageDimensionDetector imageDimensionDetector)
    {
        this.imageDimensionDetector = imageDimensionDetector ?? throw new ArgumentNullException(nameof(imageDimensionDetector));
    }

    public IReadOnlyList<DimensionCandidate> Detect(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        return InputDocumentFormatExtensions.FromPath(inputPath) switch
        {
            InputDocumentFormat.Pdf => DetectPdf(inputPath),
            InputDocumentFormat.Jpeg => imageDimensionDetector.Detect(inputPath),
            _ => throw new NotSupportedException("Supported input formats are PDF, JPG, and JPEG.")
        };
    }

    private IReadOnlyList<DimensionCandidate> DetectPdf(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        var candidates = new List<DimensionCandidate>();

        foreach (var page in document.GetPages())
        {
            var vectorPageCandidates = DetectPdfPageVectorCandidates(page);
            var pageCandidates = TryDetectPdfPageWithOcrFallback(
                pdfPath,
                page.Number,
                page.Width,
                page.Height,
                vectorPageCandidates,
                out var ocrPageCandidates)
                ? ocrPageCandidates
                : vectorPageCandidates;

            candidates.AddRange(pageCandidates);
        }

        return AssignReadingOrder(candidates);
    }

    private static List<DimensionCandidate> DetectPdfPageVectorCandidates(Page page)
    {
        var pageWidth = page.Width;
        var pageHeight = page.Height;
        var candidates = new List<DimensionCandidate>();
        var words = page.GetWords()
            .Select(word => new WordCandidate(
                page.Number,
                word.Text,
                word.BoundingBox.Left,
                word.BoundingBox.Bottom,
                word.BoundingBox.Right,
                word.BoundingBox.Top))
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .OrderByDescending(word => word.Top)
            .ThenBy(word => word.Left)
            .ToList();

        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            if (i + 1 < words.Count && TryCombineTolerance(word, words[i + 1], out var combined) && IsLikelyDimension(combined.Text))
            {
                var combinedCandidate = combined.ToDimensionCandidate();
                if (IsInAllowedDrawingTextArea(combinedCandidate, pageWidth, pageHeight))
                {
                    candidates.Add(combinedCandidate);
                }

                i++;
                continue;
            }

            var candidate = word.ToDimensionCandidate();
            if (IsLikelyPdfVectorDimension(candidate, pageWidth, pageHeight))
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    private bool TryDetectPdfPageWithOcrFallback(
        string pdfPath,
        int pageNumber,
        double pageWidth,
        double pageHeight,
        IReadOnlyList<DimensionCandidate> vectorPageCandidates,
        out IReadOnlyList<DimensionCandidate> ocrPageCandidates)
    {
        ocrPageCandidates = Array.Empty<DimensionCandidate>();
        if (vectorPageCandidates.Count > SparsePdfPageDimensionThreshold)
        {
            return false;
        }

        try
        {
            var imagePath = RenderPdfPageToTemporaryImage(pdfPath, pageNumber, pageWidth, pageHeight, out var imageWidth, out var imageHeight);
            try
            {
                ocrPageCandidates = imageDimensionDetector.Detect(imagePath)
                    .Select(candidate => MapImageCandidateToPdfPage(candidate, pageNumber, pageWidth, pageHeight, imageWidth, imageHeight))
                    .ToList();
            }
            finally
            {
                File.Delete(imagePath);
            }
        }
        catch (InvalidOperationException) when (vectorPageCandidates.Count > 0)
        {
            return false;
        }

        return ocrPageCandidates.Count > vectorPageCandidates.Count;
    }

    private static string RenderPdfPageToTemporaryImage(
        string pdfPath,
        int pageNumber,
        double pageWidth,
        double pageHeight,
        out int imageWidth,
        out int imageHeight)
    {
        var (pixelWidth, pixelHeight) = GetOcrFallbackRenderSize(pageWidth, pageHeight);
        var preview = new PdfPagePreviewRenderer().RenderPage(pdfPath, pageNumber, pixelWidth, pixelHeight);
        imageWidth = preview.PixelWidth;
        imageHeight = preview.PixelHeight;

        var directory = Path.Combine(Path.GetTempPath(), "BalloonPdf", "OcrFallback");
        Directory.CreateDirectory(directory);
        var imagePath = Path.Combine(directory, $"{Guid.NewGuid():N}.png");
        using var image = Image.LoadPixelData<Bgra32>(preview.Pixels, preview.PixelWidth, preview.PixelHeight);
        image.SaveAsPng(imagePath);
        return imagePath;
    }

    private static (int Width, int Height) GetOcrFallbackRenderSize(double pageWidth, double pageHeight)
    {
        if (pageWidth <= 0d || pageHeight <= 0d)
        {
            return (1, 1);
        }

        var scale = Math.Min(OcrFallbackRenderScale, OcrFallbackMaximumPixelSide / Math.Max(pageWidth, pageHeight));
        return (
            Math.Max(1, (int)Math.Round(pageWidth * scale)),
            Math.Max(1, (int)Math.Round(pageHeight * scale)));
    }

    private static DimensionCandidate MapImageCandidateToPdfPage(
        DimensionCandidate candidate,
        int pageNumber,
        double pageWidth,
        double pageHeight,
        int imageWidth,
        int imageHeight)
    {
        var widthScale = pageWidth / imageWidth;
        var heightScale = pageHeight / imageHeight;
        return new DimensionCandidate(
            pageNumber,
            candidate.Text,
            candidate.Left * widthScale,
            candidate.Bottom * heightScale,
            candidate.Right * widthScale,
            candidate.Top * heightScale,
            0);
    }

    internal static bool IsLikelyDimension(string text)
    {
        var normalized = NormalizeDimensionText(text);
        return normalized.Length > 0 && DimensionRegex.IsMatch(normalized);
    }

    internal static string NormalizeDimensionText(string text)
    {
        var normalized = text.Trim()
            .Replace("−", "-", StringComparison.Ordinal)
            .Replace("–", "-", StringComparison.Ordinal)
            .Replace("⌀", "Ø", StringComparison.Ordinal);

        return LeadingOcrDiameterRegex.Replace(normalized, "Ø$1", count: 1);
    }

    internal static IReadOnlyList<DimensionCandidate> AssignReadingOrder(IEnumerable<DimensionCandidate> candidates)
    {
        return candidates
            .OrderBy(candidate => candidate.PageNumber)
            .ThenByDescending(candidate => candidate.Top)
            .ThenBy(candidate => candidate.Left)
            .Select((candidate, index) => candidate with { BalloonNumber = index + 1 })
            .ToList();
    }

    internal static bool IsInBottomRightDetailsBox(
        double left,
        double bottom,
        double right,
        double top,
        double pageWidth,
        double pageHeight)
    {
        if (pageWidth <= 0d || pageHeight <= 0d)
        {
            return false;
        }

        var centerX = left + ((right - left) / 2d);
        var centerY = bottom + ((top - bottom) / 2d);
        return centerX >= pageWidth * DetailsBoxMinimumCenterXRatio
            && centerY <= pageHeight * DetailsBoxMaximumCenterYRatio;
    }

    internal static bool IsLikelyStandaloneDrawingAreaIntegerDimension(DimensionCandidate candidate, double pageWidth, double pageHeight)
    {
        return WholeNumberRegex.IsMatch(candidate.Text.Trim())
            && IsInAllowedDrawingTextArea(candidate, pageWidth, pageHeight)
            && !IsInBorderOrGridArea(candidate, pageWidth, pageHeight)
            && !IsInBottomWatermarkBand(candidate, pageHeight);
    }

    private static bool IsLikelyPdfVectorDimension(DimensionCandidate candidate, double pageWidth, double pageHeight)
    {
        return (IsLikelyDimension(candidate.Text) && IsInAllowedDrawingTextArea(candidate, pageWidth, pageHeight))
            || IsLikelyStandaloneDrawingAreaIntegerDimension(candidate, pageWidth, pageHeight);
    }

    private static bool IsInAllowedDrawingTextArea(DimensionCandidate candidate, double pageWidth, double pageHeight)
    {
        return !IsInBottomRightDetailsBox(candidate.Left, candidate.Bottom, candidate.Right, candidate.Top, pageWidth, pageHeight);
    }

    private static bool IsInBorderOrGridArea(DimensionCandidate candidate, double pageWidth, double pageHeight)
    {
        if (pageWidth <= 0d || pageHeight <= 0d)
        {
            return false;
        }

        return candidate.CenterX <= pageWidth * BorderExclusionRatio
            || candidate.CenterX >= pageWidth * (1d - BorderExclusionRatio)
            || candidate.CenterY <= pageHeight * BorderExclusionRatio
            || candidate.CenterY >= pageHeight * (1d - BorderExclusionRatio);
    }

    private static bool IsInBottomWatermarkBand(DimensionCandidate candidate, double pageHeight)
    {
        return pageHeight > 0d && candidate.CenterY <= pageHeight * BottomWatermarkBandMaximumCenterYRatio;
    }

    private static bool TryCombineTolerance(WordCandidate first, WordCandidate second, out WordCandidate combined)
    {
        combined = default;
        if (first.PageNumber != second.PageNumber)
        {
            return false;
        }

        var sameLine = Math.Abs(first.CenterY - second.CenterY) <= Math.Max(first.Height, second.Height) * 0.75d;
        var closeEnough = second.Left >= first.Right && second.Left - first.Right <= Math.Max(first.Height, second.Height) * 2d;
        var normalizedSecond = NormalizeDimensionText(second.Text);
        var secondLooksLikeTolerance = normalizedSecond.StartsWith('±') || normalizedSecond.StartsWith("+/-", StringComparison.Ordinal);

        if (!sameLine || !closeEnough || !secondLooksLikeTolerance)
        {
            return false;
        }

        combined = new WordCandidate(
            first.PageNumber,
            $"{first.Text} {second.Text}",
            Math.Min(first.Left, second.Left),
            Math.Min(first.Bottom, second.Bottom),
            Math.Max(first.Right, second.Right),
            Math.Max(first.Top, second.Top));
        return true;
    }

    private readonly record struct WordCandidate(int PageNumber, string Text, double Left, double Bottom, double Right, double Top)
    {
        public double Height => Top - Bottom;

        public double CenterY => Bottom + (Height / 2d);

        public DimensionCandidate ToDimensionCandidate() => new(PageNumber, NormalizeDimensionText(Text), Left, Bottom, Right, Top, 0);
    }
}
