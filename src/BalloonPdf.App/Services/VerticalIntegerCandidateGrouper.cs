using BalloonPdf.App.Models;

namespace BalloonPdf.App.Services;

internal static class VerticalIntegerCandidateGrouper
{
    // Same page is always required.

    // Tolerance for determining whether digits are on the same
    // visual axis.
    private const double AxisTolerance = 20d;

    // Maximum gap between consecutive digits relative to their size.
    private const double MaximumGapRatio = 4.0d;

    private const double MinimumGap = 1.0d;

    // Prevent accidentally grouping very distant digits.
    private const double MaximumAbsoluteGap = 60d;

    // Prevent very large accidental groups.
    private const int MaximumDigitsInRun = 6;

    public static VerticalIntegerCandidateGroupingResult Build(
        IReadOnlyList<DimensionCandidate> wordCandidates,
        Func<DimensionCandidate, bool> isValidMergedCandidate)
    {
        ArgumentNullException.ThrowIfNull(wordCandidates);
        ArgumentNullException.ThrowIfNull(isValidMergedCandidate);

        var mergedCandidates = new List<DimensionCandidate>();
        var sourceRuns = new List<VerticalIntegerCandidateSourceRun>();
        var consumedCandidates = new List<DimensionCandidate>();
        var consumedIndexes = new HashSet<int>();

        var digitCandidates = wordCandidates
            .Select((candidate, index) =>
                new IndexedCandidate(index, candidate))
            .Where(x => IsSingleDigit(x.Candidate))
            .OrderBy(x => x.Candidate.PageNumber)
            .ThenBy(x => x.Candidate.CenterY)
            .ThenBy(x => x.Candidate.CenterX)
            .ToList();

        foreach (var first in digitCandidates)
        {
            if (consumedIndexes.Contains(first.Index))
            {
                continue;
            }

            var run = FindBestRun(
                first,
                digitCandidates,
                consumedIndexes);

            if (run.Count < 2)
            {
                continue;
            }

            var merged = Merge(run);

            if (!isValidMergedCandidate(merged))
            {
                continue;
            }

            var sourceCandidates = run
                .OrderBy(x => x.Order)
                .Select(x => x.Candidate)
                .ToList();

            mergedCandidates.Add(merged);

            sourceRuns.Add(
                new VerticalIntegerCandidateSourceRun(
                    merged,
                    sourceCandidates));

            foreach (var source in run)
            {
                consumedIndexes.Add(source.Index);
                consumedCandidates.Add(source.Candidate);
            }
        }

        return new VerticalIntegerCandidateGroupingResult(
            mergedCandidates,
            sourceRuns,
            consumedCandidates);
    }

    private static List<IndexedCandidate> FindBestRun(
        IndexedCandidate first,
        IReadOnlyList<IndexedCandidate> allDigits,
        ISet<int> consumedIndexes)
    {
        var samePage = allDigits
            .Where(candidate =>
                candidate.Candidate.PageNumber ==
                first.Candidate.PageNumber)
            .Where(candidate =>
                !consumedIndexes.Contains(candidate.Index))
            .Where(candidate =>
                candidate.Index != first.Index)
            .ToList();

        // ------------------------------------------------------------
        // OPTION 1:
        // Normal vertical PDF representation.
        //
        // Same X, changing Y:
        //
        //       4
        //       0
        //       0
        // ------------------------------------------------------------

        var normalVertical = BuildNormalVerticalRun(
            first,
            samePage);

        // ------------------------------------------------------------
        // OPTION 2:
        // Rotated PDF representation.
        //
        // The text appears vertical visually but PdfPig coordinates
        // represent the characters horizontally:
        //
        // 4 0 0
        //
        // Same Y, changing X.
        // ------------------------------------------------------------

        var rotatedVertical = BuildRotatedVerticalRun(
            first,
            samePage);

        // Select whichever run gives us the strongest grouping.
        if (rotatedVertical.Count > normalVertical.Count)
        {
            return rotatedVertical;
        }

        return normalVertical;
    }

    private static List<IndexedCandidate> BuildNormalVerticalRun(
        IndexedCandidate first,
        IReadOnlyList<IndexedCandidate> candidates)
    {
        var result = new List<IndexedCandidate>
        {
            first
        };

        var current = first;

        while (result.Count < MaximumDigitsInRun)
        {
            var nextCandidates = candidates
                .Where(candidate =>
                    !result.Any(
                        existing =>
                            existing.Index == candidate.Index))
                .Where(candidate =>
                    IsSameVerticalAxis(
                        current.Candidate,
                        candidate.Candidate))
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Distance = Math.Abs(
                        candidate.Candidate.CenterY -
                        current.Candidate.CenterY)
                })
                .Where(x =>
                    IsAcceptableGap(
                        current.Candidate,
                        x.Candidate.Candidate,
                        x.Distance))
                .OrderBy(x => x.Distance)
                .ToList();

            if (nextCandidates.Count == 0)
            {
                break;
            }

            var next = nextCandidates[0].Candidate;

            result.Add(next);
            current = next;
        }

        // A valid normal vertical run must actually have
        // meaningful Y separation.
        if (result.Count >= 2)
        {
            var yRange =
                result.Max(x => x.Candidate.CenterY) -
                result.Min(x => x.Candidate.CenterY);

            if (yRange > 0.5d)
            {
                // Order top-to-bottom according to PDF coordinates.
                return result
                    .OrderByDescending(
                        x => x.Candidate.CenterY)
                    .ToList();
            }
        }

        return new List<IndexedCandidate>();
    }

    private static List<IndexedCandidate> BuildRotatedVerticalRun(
        IndexedCandidate first,
        IReadOnlyList<IndexedCandidate> candidates)
    {
        var result = new List<IndexedCandidate>
        {
            first
        };

        var current = first;

        while (result.Count < MaximumDigitsInRun)
        {
            var nextCandidates = candidates
                .Where(candidate =>
                    !result.Any(
                        existing =>
                            existing.Index == candidate.Index))
                .Where(candidate =>
                    IsSameRotatedAxis(
                        current.Candidate,
                        candidate.Candidate))
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Distance = Math.Abs(
                        candidate.Candidate.CenterX -
                        current.Candidate.CenterX)
                })
                .Where(x =>
                    IsAcceptableGap(
                        current.Candidate,
                        x.Candidate.Candidate,
                        x.Distance))
                .OrderBy(x => x.Distance)
                .ToList();

            if (nextCandidates.Count == 0)
            {
                break;
            }

            var next = nextCandidates[0].Candidate;

            result.Add(next);
            current = next;
        }

        // A rotated vertical number must actually span X.
        if (result.Count >= 2)
        {
            var xRange =
                result.Max(x => x.Candidate.CenterX) -
                result.Min(x => x.Candidate.CenterX);

            if (xRange > 0.5d)
            {
                // The order along X corresponds to the character order
                // of the rotated dimension.
                return result
                    .OrderBy(
                        x => x.Candidate.CenterX)
                    .ToList();
            }
        }

        return new List<IndexedCandidate>();
    }

    private static bool IsSameVerticalAxis(
        DimensionCandidate first,
        DimensionCandidate second)
    {
        var xDifference =
            Math.Abs(first.CenterX - second.CenterX);

        var tolerance = Math.Max(
            AxisTolerance,
            Math.Max(
                first.Width,
                second.Width) * 2.0d);

        return xDifference <= tolerance;
    }

    private static bool IsSameRotatedAxis(
        DimensionCandidate first,
        DimensionCandidate second)
    {
        var yDifference =
            Math.Abs(first.CenterY - second.CenterY);

        var tolerance = Math.Max(
            AxisTolerance,
            Math.Max(
                first.Height,
                second.Height) * 2.0d);

        return yDifference <= tolerance;
    }

    private static bool IsAcceptableGap(
        DimensionCandidate previous,
        DimensionCandidate next,
        double gap)
    {
        var referenceSize = Math.Max(
            Math.Max(
                previous.Width,
                previous.Height),
            Math.Max(
                next.Width,
                next.Height));

        var maximumGap = Math.Min(
            MaximumAbsoluteGap,
            Math.Max(
                MinimumGap,
                referenceSize * MaximumGapRatio));

        return gap >= 0d && gap <= maximumGap;
    }

    private static DimensionCandidate Merge(
        IReadOnlyList<IndexedCandidate> run)
    {
        if (run.Count == 0)
        {
            throw new ArgumentException(
                "Cannot merge an empty run.",
                nameof(run));
        }

        // The run has already been ordered according to the
        // appropriate coordinate axis.
        var text = string.Concat(
            run.Select(
                x => x.Candidate.Text.Trim()));

        return new DimensionCandidate(
            run[0].Candidate.PageNumber,
            DimensionDetector.NormalizeDimensionText(text),
            run.Min(x => x.Candidate.Left),
            run.Min(x => x.Candidate.Bottom),
            run.Max(x => x.Candidate.Right),
            run.Max(x => x.Candidate.Top),
            0);
    }

    private static bool IsSingleDigit(
        DimensionCandidate candidate)
    {
        var text = candidate.Text.Trim();

        return text.Length == 1 &&
               char.IsDigit(text[0]);
    }

    private readonly record struct IndexedCandidate(
        int Index,
        DimensionCandidate Candidate)
    {
        public int Order => Index;
    }
}


internal sealed class VerticalIntegerCandidateGroupingResult(
    IReadOnlyList<DimensionCandidate> mergedCandidates,
    IReadOnlyList<VerticalIntegerCandidateSourceRun> sourceRuns,
    IReadOnlyList<DimensionCandidate> consumedCandidates)
{
    public IReadOnlyList<DimensionCandidate> MergedCandidates
    { get; } = mergedCandidates;

    public IReadOnlyList<VerticalIntegerCandidateSourceRun> SourceRuns
    { get; } = sourceRuns;

    public bool ContainsSource(
        DimensionCandidate candidate)
    {
        return consumedCandidates.Any(
            consumed => IsSameSource(
                consumed,
                candidate));
    }

    private static bool IsSameSource(
        DimensionCandidate first,
        DimensionCandidate second)
    {
        return first.PageNumber == second.PageNumber
            && string.Equals(
                first.Text,
                second.Text,
                StringComparison.Ordinal)
            && Math.Abs(
                first.Left - second.Left) < 0.01d
            && Math.Abs(
                first.Bottom - second.Bottom) < 0.01d
            && Math.Abs(
                first.Right - second.Right) < 0.01d
            && Math.Abs(
                first.Top - second.Top) < 0.01d;
    }
}


internal sealed record VerticalIntegerCandidateSourceRun(
    DimensionCandidate MergedCandidate,
    IReadOnlyList<DimensionCandidate> SourceCandidates);
