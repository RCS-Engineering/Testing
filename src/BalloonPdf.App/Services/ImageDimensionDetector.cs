using System.Text.RegularExpressions;
using BalloonPdf.App.Models;
using SixLabors.ImageSharp;

namespace BalloonPdf.App.Services;

public sealed class ImageDimensionDetector
{
    private static readonly Regex TolerancePrefixRegex = new(@"^(?:±|\+/-)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SplitPrefixRegex = new(@"^(?:[Ø⌀o]|%%c|r|m)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex ThreadCountPrefixRegex = new(@"^\d+\s*x$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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

        var words = textExtractor.ExtractWords(imagePath)
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .Select(word => ToCandidate(word, pageHeight))
            .OrderByDescending(candidate => candidate.Top)
            .ThenBy(candidate => candidate.Left)
            .ToList();

        var explicitCandidates = BuildExplicitCandidates(words, pageWidth, pageHeight);
        var candidates = new List<DimensionCandidate>(explicitCandidates);

        foreach (var word in words)
        {
            if (!IsLikelyStandaloneImageIntegerDimension(word, pageWidth, pageHeight)
                || explicitCandidates.Any(candidate => Overlaps(candidate, word)))
            {
                continue;
            }

            AddIfNew(candidates, word);
        }

        return DimensionDetector.AssignReadingOrder(candidates);
    }

    private static List<DimensionCandidate> BuildExplicitCandidates(
        IReadOnlyList<DimensionCandidate> words,
        double pageWidth,
        double pageHeight)
    {
        var verticalIntegerGrouping = VerticalIntegerCandidateGrouper.Build(
            words,
            candidate => IsLikelyStandaloneImageIntegerDimension(candidate, pageWidth, pageHeight));
        var candidates = new List<DimensionCandidate>(verticalIntegerGrouping.MergedCandidates);

        foreach (var word in words)
        {
            if (verticalIntegerGrouping.ContainsSource(word))
            {
                continue;
            }

            if (DimensionDetector.IsLikelyDimension(word.Text) && IsInAllowedImageTextArea(word, pageWidth, pageHeight))
            {
                AddIfNew(candidates, word with { Text = DimensionDetector.NormalizeDimensionText(word.Text) });
            }
        }

        foreach (var current in words)
        {
            foreach (var next in words
                .Where(candidate => !ReferenceEquals(candidate, current) && IsSameLineNearby(current, candidate))
                .OrderBy(candidate => candidate.Left)
                .Take(2))
            {
                foreach (var combinedText in BuildCombinedTextCandidates(current.Text, next.Text))
                {
                    if (!DimensionDetector.IsLikelyDimension(combinedText))
                    {
                        continue;
                    }

                    var combined = Combine(current, next, DimensionDetector.NormalizeDimensionText(combinedText));
                    if (IsInAllowedImageTextArea(combined, pageWidth, pageHeight))
                    {
                        AddIfNew(candidates, combined);
                    }

                    break;
                }
            }
        }

        return candidates;
    }

    private static DimensionCandidate ToCandidate(ImageTextWord word, int pageHeight)
    {
        var left = word.Left;
        var right = word.Right;
        var bottom = pageHeight - word.Bottom;
        var top = pageHeight - word.Top;
        return new DimensionCandidate(1, word.Text.Trim(), left, bottom, right, top, 0);
    }

    private static DimensionCandidate Combine(DimensionCandidate first, DimensionCandidate second, string text)
    {
        return new DimensionCandidate(
            1,
            text,
            Math.Min(first.Left, second.Left),
            Math.Min(first.Bottom, second.Bottom),
            Math.Max(first.Right, second.Right),
            Math.Max(first.Top, second.Top),
            0);
    }

    private static IEnumerable<string> BuildCombinedTextCandidates(string first, string second)
    {
        var trimmedFirst = first.Trim();
        var trimmedSecond = second.Trim();

        if (trimmedFirst.Length == 0 || trimmedSecond.Length == 0)
        {
            yield break;
        }

        var secondIsTolerance = TolerancePrefixRegex.IsMatch(trimmedSecond);
        var firstIsDiameterWord = trimmedFirst.StartsWith("dia", StringComparison.OrdinalIgnoreCase);
        var firstIsSplitPrefix = SplitPrefixRegex.IsMatch(trimmedFirst);
        var firstIsThreadCountPrefix = ThreadCountPrefixRegex.IsMatch(trimmedFirst)
            && trimmedSecond.StartsWith("m", StringComparison.OrdinalIgnoreCase);

        if (!secondIsTolerance && !firstIsDiameterWord && !firstIsSplitPrefix && !firstIsThreadCountPrefix)
        {
            yield break;
        }

        if (secondIsTolerance || firstIsDiameterWord)
        {
            yield return $"{trimmedFirst} {trimmedSecond}";
            yield return trimmedFirst + trimmedSecond;
            yield break;
        }

        yield return trimmedFirst + trimmedSecond;
        yield return $"{trimmedFirst} {trimmedSecond}";
    }

    private static bool IsLikelyStandaloneImageIntegerDimension(DimensionCandidate candidate, double pageWidth, double pageHeight)
    {
        return DimensionDetector.IsLikelyStandaloneDrawingAreaIntegerDimension(candidate, pageWidth, pageHeight);
    }

    private static bool IsInAllowedImageTextArea(DimensionCandidate candidate, double pageWidth, double pageHeight)
    {
        return !DimensionDetector.IsInBottomRightDetailsBox(candidate.Left, candidate.Bottom, candidate.Right, candidate.Top, pageWidth, pageHeight);
    }

    private static bool IsSameLineNearby(DimensionCandidate first, DimensionCandidate second)
    {
        if (second.Left < first.Right)
        {
            return false;
        }

        var maxHeight = Math.Max(first.Height, second.Height);
        var sameLine = Math.Abs(first.CenterY - second.CenterY) <= maxHeight * 0.75d;
        var closeEnough = second.Left - first.Right <= Math.Max(12d, maxHeight * 2.5d);
        return sameLine && closeEnough;
    }

    private static bool Overlaps(DimensionCandidate first, DimensionCandidate second)
    {
        return first.Left < second.Right
            && first.Right > second.Left
            && first.Bottom < second.Top
            && first.Top > second.Bottom;
    }

    private static void AddIfNew(ICollection<DimensionCandidate> candidates, DimensionCandidate candidate)
    {
        if (candidates.Any(existing => IsDuplicate(existing, candidate)
            || (Contains(existing, candidate) && existing.Text.Length >= candidate.Text.Length)))
        {
            return;
        }

        foreach (var existing in candidates
            .Where(existing => Contains(candidate, existing) && candidate.Text.Length > existing.Text.Length)
            .ToList())
        {
            candidates.Remove(existing);
        }

        candidates.Add(candidate);
    }

    private static bool Contains(DimensionCandidate outer, DimensionCandidate inner)
    {
        return outer.Left <= inner.Left
            && outer.Bottom <= inner.Bottom
            && outer.Right >= inner.Right
            && outer.Top >= inner.Top;
    }

    private static bool IsDuplicate(DimensionCandidate first, DimensionCandidate second)
    {
        return string.Equals(first.Text, second.Text, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(first.Left - second.Left) < 0.01d
            && Math.Abs(first.Bottom - second.Bottom) < 0.01d
            && Math.Abs(first.Right - second.Right) < 0.01d
            && Math.Abs(first.Top - second.Top) < 0.01d;
    }
}
