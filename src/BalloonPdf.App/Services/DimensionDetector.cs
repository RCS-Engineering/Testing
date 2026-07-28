using System.Text.RegularExpressions;
using BalloonPdf.App.Models;
using UglyToad.PdfPig;

namespace BalloonPdf.App.Services;

public sealed class DimensionDetector
{
    private const double DetailsBoxMinimumCenterXRatio = 0.72d;
    private const double DetailsBoxMaximumCenterYRatio = 0.25d;

    private static readonly Regex DimensionRegex = new(
        @"^(?:(?:[Ø⌀]|%%c|dia\.?|diam\.?|r)\s*(?:\.\d+|\d+(?:\.\d+)?|\d+\s*/\s*\d+)|(?:\d+\s*x\s*)?m\s*\d+|(?:\.\d+|\d+\.\d+|\d+\s*/\s*\d+)|\d+(?:\.\d+)?\s*(?:°|deg|degrees)|(?:\.\d+|\d+(?:\.\d+)?|\d+\s*/\s*\d+)\s*(?:±|\+/-)\s*(?:\.\d+|\d+(?:\.\d+)?|\d+\s*/\s*\d+))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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

    private static IReadOnlyList<DimensionCandidate> DetectPdf(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        var candidates = new List<DimensionCandidate>();

        foreach (var page in document.GetPages())
        {
            var pageWidth = page.Width;
            var pageHeight = page.Height;
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
                    if (!IsInBottomRightDetailsBox(combined.Left, combined.Bottom, combined.Right, combined.Top, pageWidth, pageHeight))
                    {
                        candidates.Add(combined.ToDimensionCandidate());
                    }

                    i++;
                    continue;
                }

                if (IsLikelyDimension(word.Text)
                    && !IsInBottomRightDetailsBox(word.Left, word.Bottom, word.Right, word.Top, pageWidth, pageHeight))
                {
                    candidates.Add(word.ToDimensionCandidate());
                }
            }
        }

        return AssignReadingOrder(candidates);
    }

    internal static bool IsLikelyDimension(string text)
    {
        var normalized = Normalize(text);
        return normalized.Length > 0 && DimensionRegex.IsMatch(normalized);
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

    private static bool TryCombineTolerance(WordCandidate first, WordCandidate second, out WordCandidate combined)
    {
        combined = default;
        if (first.PageNumber != second.PageNumber)
        {
            return false;
        }

        var sameLine = Math.Abs(first.CenterY - second.CenterY) <= Math.Max(first.Height, second.Height) * 0.75d;
        var closeEnough = second.Left >= first.Right && second.Left - first.Right <= Math.Max(first.Height, second.Height) * 2d;
        var secondLooksLikeTolerance = Normalize(second.Text).StartsWith('±') || Normalize(second.Text).StartsWith("+/-", StringComparison.Ordinal);

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

    private static string Normalize(string text)
    {
        return text.Trim()
            .Replace("−", "-", StringComparison.Ordinal)
            .Replace("–", "-", StringComparison.Ordinal)
            .Replace("⌀", "Ø", StringComparison.Ordinal);
    }

    private readonly record struct WordCandidate(int PageNumber, string Text, double Left, double Bottom, double Right, double Top)
    {
        public double Height => Top - Bottom;

        public double CenterY => Bottom + (Height / 2d);

        public DimensionCandidate ToDimensionCandidate() => new(PageNumber, Normalize(Text), Left, Bottom, Right, Top, 0);
    }
}
