using BalloonPdf.App.Models;

namespace BalloonPdf.App.Services;

internal static class VerticalIntegerCandidateGrouper
{
    private const double MinimumHorizontalOverlapRatio = 0.35d;
    private const double MaximumCenterXDeltaRatio = 0.75d;
    private const double MaximumCenterXDelta = 10d;
    private const double MaximumPairedWidthCenterXDeltaRatio = 1d;
    private const double MaximumStackedCenterXDeltaToCenterYDeltaRatio = 0.45d;
    private const double MaximumRunAxisCenterXDeltaRatio = 1.25d;
    private const double MaximumRunAxisCenterXDelta = 12d;
    private const double MaximumVerticalGapRatio = 4.75d;
    private const double MaximumVerticalOverlapRatio = 0.5d;
    private const double MinimumVerticalGap = 4d;

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
            .Select((candidate, index) => new IndexedCandidate(index, candidate))
            .Where(candidate => IsSingleDigit(candidate.Candidate))
            .OrderBy(candidate => candidate.Candidate.PageNumber)
            .ThenByDescending(candidate => candidate.Candidate.Top)
            .ThenBy(candidate => candidate.Candidate.Left)
            .ToList();

        foreach (var candidate in digitCandidates)
        {
            if (consumedIndexes.Contains(candidate.Index))
            {
                continue;
            }

            var run = BuildAxisRun(candidate, digitCandidates, consumedIndexes);
            if (run.Count < 2)
            {
                continue;
            }

            var merged = Merge(run);
            if (!IsTallerThanWide(merged) || !isValidMergedCandidate(merged))
            {
                continue;
            }

            var sourceCandidates = run
                .OrderByDescending(runCandidate => runCandidate.Candidate.Top)
                .ThenBy(runCandidate => runCandidate.Candidate.Left)
                .Select(runCandidate => runCandidate.Candidate)
                .ToList();
            mergedCandidates.Add(merged);
            sourceRuns.Add(new VerticalIntegerCandidateSourceRun(merged, sourceCandidates));
            foreach (var runCandidate in run)
            {
                consumedIndexes.Add(runCandidate.Index);
                consumedCandidates.Add(runCandidate.Candidate);
            }
        }

        return new VerticalIntegerCandidateGroupingResult(mergedCandidates, sourceRuns, consumedCandidates);
    }

    private static List<IndexedCandidate> BuildAxisRun(
        IndexedCandidate first,
        IReadOnlyList<IndexedCandidate> digitCandidates,
        ISet<int> consumedIndexes)
    {
        var axisCluster = digitCandidates
            .Where(candidate => candidate.Candidate.PageNumber == first.Candidate.PageNumber
                && !consumedIndexes.Contains(candidate.Index)
                && IsAlignedOnVerticalAxis(first.Candidate, candidate.Candidate))
            .OrderByDescending(candidate => candidate.Candidate.Top)
            .ThenBy(candidate => candidate.Candidate.Left)
            .ToList();
        var firstIndex = axisCluster.FindIndex(candidate => candidate.Index == first.Index);
        if (firstIndex < 0)
        {
            return new List<IndexedCandidate> { first };
        }

        var run = new List<IndexedCandidate> { first };
        for (var i = firstIndex + 1; i < axisCluster.Count; i++)
        {
            var next = axisCluster[i];
            if (!IsStackedBelow(run[^1].Candidate, next.Candidate) || !IsAlignedWithRunAxis(run, next.Candidate))
            {
                break;
            }

            run.Add(next);
        }

        return run;
    }

    private static DimensionCandidate Merge(IReadOnlyList<IndexedCandidate> run)
    {
        var orderedRun = run
            .OrderByDescending(candidate => candidate.Candidate.Top)
            .ThenBy(candidate => candidate.Candidate.Left)
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
        return verticalGap >= -maxHeight * MaximumVerticalOverlapRatio
            && verticalGap <= Math.Max(MinimumVerticalGap, maxHeight * MaximumVerticalGapRatio);
    }

    private static bool IsAlignedWithRunAxis(IReadOnlyCollection<IndexedCandidate> run, DimensionCandidate candidate)
    {
        if (run.Count == 1)
        {
            return IsAlignedOnVerticalAxis(run.Single().Candidate, candidate);
        }

        var averageCenterX = run.Average(runCandidate => runCandidate.Candidate.CenterX);
        var averageWidth = run.Average(runCandidate => runCandidate.Candidate.Width);
        var averageHeight = run.Average(runCandidate => runCandidate.Candidate.Height);
        var tolerance = Math.Max(
            MaximumRunAxisCenterXDelta,
            Math.Max(averageHeight * MaximumCenterXDeltaRatio, averageWidth * MaximumRunAxisCenterXDeltaRatio));
        return Math.Abs(candidate.CenterX - averageCenterX) <= tolerance;
    }

    private static bool IsAlignedOnVerticalAxis(DimensionCandidate first, DimensionCandidate second)
    {
        if (first.PageNumber != second.PageNumber)
        {
            return false;
        }

        var minWidth = Math.Min(first.Width, second.Width);
        if (minWidth <= 0d)
        {
            return false;
        }

        var horizontalOverlap = Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left);
        var centerXDelta = Math.Abs(first.CenterX - second.CenterX);
        var maxHeight = Math.Max(first.Height, second.Height);
        var pairedWidthTolerance = (first.Width + second.Width) * MaximumPairedWidthCenterXDeltaRatio;
        var centerXDeltaTolerance = Math.Max(
            Math.Max(MaximumCenterXDelta, maxHeight * MaximumCenterXDeltaRatio),
            pairedWidthTolerance);
        if (horizontalOverlap / minWidth >= MinimumHorizontalOverlapRatio || centerXDelta <= centerXDeltaTolerance)
        {
            return true;
        }

        if (!WouldMergeTallerThanWide(first, second))
        {
            return false;
        }

        var centerYDelta = Math.Abs(first.CenterY - second.CenterY);
        var stackedCenterXDeltaTolerance = centerYDelta * MaximumStackedCenterXDeltaToCenterYDeltaRatio;
        return centerXDelta <= stackedCenterXDeltaTolerance;
    }

    private static bool WouldMergeTallerThanWide(DimensionCandidate first, DimensionCandidate second)
    {
        var width = Math.Max(first.Right, second.Right) - Math.Min(first.Left, second.Left);
        var height = Math.Max(first.Top, second.Top) - Math.Min(first.Bottom, second.Bottom);
        return height > width;
    }

    private static bool IsTallerThanWide(DimensionCandidate candidate)
    {
        return candidate.Height > candidate.Width;
    }

    private readonly record struct IndexedCandidate(int Index, DimensionCandidate Candidate);
}

internal sealed class VerticalIntegerCandidateGroupingResult(
    IReadOnlyList<DimensionCandidate> mergedCandidates,
    IReadOnlyList<VerticalIntegerCandidateSourceRun> sourceRuns,
    IReadOnlyList<DimensionCandidate> consumedCandidates)
{
    public IReadOnlyList<DimensionCandidate> MergedCandidates { get; } = mergedCandidates;

    public IReadOnlyList<VerticalIntegerCandidateSourceRun> SourceRuns { get; } = sourceRuns;

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

internal sealed record VerticalIntegerCandidateSourceRun(
    DimensionCandidate MergedCandidate,
    IReadOnlyList<DimensionCandidate> SourceCandidates);
