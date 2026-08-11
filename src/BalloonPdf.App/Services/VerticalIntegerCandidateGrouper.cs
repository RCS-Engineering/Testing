using BalloonPdf.App.Models;

namespace BalloonPdf.App.Services;

internal static class VerticalIntegerCandidateGrouper
{
    private const double MinimumHorizontalOverlapRatio = 0.6d;
    private const double MaximumCenterXDeltaRatio = 0.35d;
    private const double MaximumVerticalGapRatio = 1.25d;
    private const double MaximumVerticalOverlapRatio = 0.25d;
    private const double MinimumVerticalGap = 4d;

    public static VerticalIntegerCandidateGroupingResult Build(
        IReadOnlyList<DimensionCandidate> wordCandidates,
        Func<DimensionCandidate, bool> isValidMergedCandidate)
    {
        ArgumentNullException.ThrowIfNull(wordCandidates);
        ArgumentNullException.ThrowIfNull(isValidMergedCandidate);

        var mergedCandidates = new List<DimensionCandidate>();
        var consumedCandidates = new List<DimensionCandidate>();
        var consumedIndexes = new HashSet<int>();
        var digitCandidates = wordCandidates
            .Select((candidate, index) => new IndexedCandidate(index, candidate))
            .Where(candidate => IsSingleDigit(candidate.Candidate))
            .OrderBy(candidate => candidate.Candidate.PageNumber)
            .ThenBy(candidate => candidate.Candidate.Left)
            .ThenByDescending(candidate => candidate.Candidate.Top)
            .ToList();

        foreach (var candidate in digitCandidates)
        {
            if (consumedIndexes.Contains(candidate.Index))
            {
                continue;
            }

            var run = BuildRun(candidate, digitCandidates, consumedIndexes);
            if (run.Count < 2)
            {
                continue;
            }

            var merged = Merge(run);
            if (!isValidMergedCandidate(merged))
            {
                continue;
            }

            mergedCandidates.Add(merged);
            foreach (var runCandidate in run)
            {
                consumedIndexes.Add(runCandidate.Index);
                consumedCandidates.Add(runCandidate.Candidate);
            }
        }

        return new VerticalIntegerCandidateGroupingResult(mergedCandidates, consumedCandidates);
    }

    private static List<IndexedCandidate> BuildRun(
        IndexedCandidate first,
        IReadOnlyList<IndexedCandidate> digitCandidates,
        ISet<int> consumedIndexes)
    {
        var run = new List<IndexedCandidate> { first };
        var current = first;

        while (true)
        {
            var next = digitCandidates
                .Where(candidate => candidate.Index != current.Index
                    && !consumedIndexes.Contains(candidate.Index)
                    && run.All(runCandidate => runCandidate.Index != candidate.Index)
                    && IsStackedBelow(current.Candidate, candidate.Candidate))
                .OrderBy(candidate => current.Candidate.Bottom - candidate.Candidate.Top)
                .ThenBy(candidate => Math.Abs(current.Candidate.CenterX - candidate.Candidate.CenterX))
                .Select(candidate => (IndexedCandidate?)candidate)
                .FirstOrDefault();

            if (next is null)
            {
                return run;
            }

            run.Add(next.Value);
            current = next.Value;
        }
    }

    private static DimensionCandidate Merge(IReadOnlyList<IndexedCandidate> run)
    {
        var orderedRun = run
            .OrderByDescending(candidate => candidate.Candidate.Top)
            .ToList();
        var text = string.Concat(orderedRun.Select(candidate => candidate.Candidate.Text.Trim()));
        return new DimensionCandidate(
            orderedRun[0].Candidate.PageNumber,
            DimensionDetector.NormalizeDimensionText(text),
            orderedRun.Min(candidate => candidate.Candidate.Left),
            orderedRun.Min(candidate => candidate.Candidate.Bottom),
            orderedRun.Max(candidate => candidate.Candidate.Right),
            orderedRun.Max(candidate => candidate.Candidate.Top),
            0);
    }

    private static bool IsSingleDigit(DimensionCandidate candidate)
    {
        var text = candidate.Text.Trim();
        return text.Length == 1 && char.IsDigit(text[0]);
    }

    private static bool IsStackedBelow(DimensionCandidate upper, DimensionCandidate lower)
    {
        if (upper.PageNumber != lower.PageNumber || lower.CenterY >= upper.CenterY)
        {
            return false;
        }

        var maxHeight = Math.Max(upper.Height, lower.Height);
        var verticalGap = upper.Bottom - lower.Top;
        if (verticalGap < -maxHeight * MaximumVerticalOverlapRatio
            || verticalGap > Math.Max(MinimumVerticalGap, maxHeight * MaximumVerticalGapRatio))
        {
            return false;
        }

        var horizontalOverlap = Math.Min(upper.Right, lower.Right) - Math.Max(upper.Left, lower.Left);
        var minWidth = Math.Min(upper.Width, lower.Width);
        if (minWidth <= 0d)
        {
            return false;
        }

        var centerXDelta = Math.Abs(upper.CenterX - lower.CenterX);
        return horizontalOverlap / minWidth >= MinimumHorizontalOverlapRatio
            || centerXDelta <= minWidth * MaximumCenterXDeltaRatio;
    }

    private readonly record struct IndexedCandidate(int Index, DimensionCandidate Candidate);
}

internal sealed class VerticalIntegerCandidateGroupingResult(
    IReadOnlyList<DimensionCandidate> mergedCandidates,
    IReadOnlyList<DimensionCandidate> consumedCandidates)
{
    public IReadOnlyList<DimensionCandidate> MergedCandidates { get; } = mergedCandidates;

    public bool ContainsSource(DimensionCandidate candidate)
    {
        return consumedCandidates.Any(consumed => IsSameSource(consumed, candidate));
    }

    private static bool IsSameSource(DimensionCandidate first, DimensionCandidate second)
    {
        return first.PageNumber == second.PageNumber
            && string.Equals(first.Text, second.Text, StringComparison.Ordinal)
            && Math.Abs(first.Left - second.Left) < 0.01d
            && Math.Abs(first.Bottom - second.Bottom) < 0.01d
            && Math.Abs(first.Right - second.Right) < 0.01d
            && Math.Abs(first.Top - second.Top) < 0.01d;
    }
}
