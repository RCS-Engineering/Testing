using BalloonPdf.App.Models;
using UglyToad.PdfPig;

namespace BalloonPdf.App.Services;

public sealed class PdfTableRegionDetector
{
    private const double CoordinateTolerance = 1.5d;
    private const double MinimumSegmentLength = 12d;
    private const double MinimumRegionWidth = 24d;
    private const double MinimumRegionHeight = 16d;
    private const int MinimumIntersectionCount = 6;

    public IReadOnlyList<PdfTableRegion> Detect(string pdfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        using var document = PdfDocument.Open(pdfPath);
        var regions = new List<PdfTableRegion>();
        foreach (var page in document.GetPages())
        {
            var segments = page.ExperimentalAccess.Paths
                .Where(path => path.IsStroked)
                .SelectMany(path => path)
                .Where(subpath => !subpath.IsDrawnAsRectangle)
                .Select(subpath => subpath.GetBoundingRectangle())
                .Where(bounds => bounds.HasValue)
                .Select(bounds => bounds!.Value)
                .SelectMany(bounds => CreateOrthogonalSegments(page.Number, bounds.Left, bounds.Bottom, bounds.Right, bounds.Top))
                .ToList();

            var horizontalSegments = MergeCollinearSegments(segments.Where(segment => segment.Orientation == SegmentOrientation.Horizontal));
            var verticalSegments = MergeCollinearSegments(segments.Where(segment => segment.Orientation == SegmentOrientation.Vertical));
            regions.AddRange(DetectPageRegions(page.Number, horizontalSegments, verticalSegments));
        }

        return MergeOverlappingRegions(regions);
    }

    private static IEnumerable<GridSegment> CreateOrthogonalSegments(int pageNumber, double left, double bottom, double right, double top)
    {
        var width = right - left;
        var height = top - bottom;
        if (width >= MinimumSegmentLength && height <= CoordinateTolerance)
        {
            yield return new GridSegment(pageNumber, SegmentOrientation.Horizontal, (bottom + top) / 2d, left, right);
        }

        if (height >= MinimumSegmentLength && width <= CoordinateTolerance)
        {
            yield return new GridSegment(pageNumber, SegmentOrientation.Vertical, (left + right) / 2d, bottom, top);
        }
    }

    private static List<GridSegment> MergeCollinearSegments(IEnumerable<GridSegment> segments)
    {
        var merged = new List<GridSegment>();
        foreach (var segment in segments
            .OrderBy(segment => segment.PageNumber)
            .ThenBy(segment => segment.Orientation)
            .ThenBy(segment => segment.FixedCoordinate)
            .ThenBy(segment => segment.Start))
        {
            var existingIndex = merged.FindIndex(existing =>
                existing.PageNumber == segment.PageNumber
                && existing.Orientation == segment.Orientation
                && Math.Abs(existing.FixedCoordinate - segment.FixedCoordinate) <= CoordinateTolerance
                && segment.Start <= existing.End + CoordinateTolerance
                && segment.End >= existing.Start - CoordinateTolerance);
            if (existingIndex < 0)
            {
                merged.Add(segment);
                continue;
            }

            var existing = merged[existingIndex];
            merged[existingIndex] = existing with
            {
                FixedCoordinate = (existing.FixedCoordinate + segment.FixedCoordinate) / 2d,
                Start = Math.Min(existing.Start, segment.Start),
                End = Math.Max(existing.End, segment.End)
            };
        }

        return merged;
    }

    private static IEnumerable<PdfTableRegion> DetectPageRegions(
        int pageNumber,
        IReadOnlyList<GridSegment> horizontalSegments,
        IReadOnlyList<GridSegment> verticalSegments)
    {
        var components = BuildConnectedComponents(horizontalSegments, verticalSegments);
        foreach (var component in components)
        {
            var horizontal = component.HorizontalSegments;
            var vertical = component.VerticalSegments;
            var intersectionCount = horizontal.Sum(horizontalSegment =>
                vertical.Count(verticalSegment => SegmentsIntersect(horizontalSegment, verticalSegment)));

            if (horizontal.Count < 2
                || vertical.Count < 2
                || (horizontal.Count < 3 && vertical.Count < 3)
                || intersectionCount < MinimumIntersectionCount)
            {
                continue;
            }

            var left = vertical.Min(segment => segment.FixedCoordinate);
            var right = vertical.Max(segment => segment.FixedCoordinate);
            var bottom = horizontal.Min(segment => segment.FixedCoordinate);
            var top = horizontal.Max(segment => segment.FixedCoordinate);
            if (right - left < MinimumRegionWidth || top - bottom < MinimumRegionHeight)
            {
                continue;
            }

            yield return new PdfTableRegion(pageNumber, left, bottom, right, top);
        }
    }

    private static List<GridComponent> BuildConnectedComponents(
        IReadOnlyList<GridSegment> horizontalSegments,
        IReadOnlyList<GridSegment> verticalSegments)
    {
        var horizontalVisited = new bool[horizontalSegments.Count];
        var verticalVisited = new bool[verticalSegments.Count];
        var components = new List<GridComponent>();

        for (var startIndex = 0; startIndex < horizontalSegments.Count; startIndex++)
        {
            if (horizontalVisited[startIndex])
            {
                continue;
            }

            var horizontalQueue = new Queue<int>();
            var verticalQueue = new Queue<int>();
            var component = new GridComponent();
            horizontalVisited[startIndex] = true;
            horizontalQueue.Enqueue(startIndex);

            while (horizontalQueue.Count > 0 || verticalQueue.Count > 0)
            {
                while (horizontalQueue.TryDequeue(out var horizontalIndex))
                {
                    var horizontal = horizontalSegments[horizontalIndex];
                    component.HorizontalSegments.Add(horizontal);
                    for (var verticalIndex = 0; verticalIndex < verticalSegments.Count; verticalIndex++)
                    {
                        if (!verticalVisited[verticalIndex] && SegmentsIntersect(horizontal, verticalSegments[verticalIndex]))
                        {
                            verticalVisited[verticalIndex] = true;
                            verticalQueue.Enqueue(verticalIndex);
                        }
                    }
                }

                while (verticalQueue.TryDequeue(out var verticalIndex))
                {
                    var vertical = verticalSegments[verticalIndex];
                    component.VerticalSegments.Add(vertical);
                    for (var horizontalIndex = 0; horizontalIndex < horizontalSegments.Count; horizontalIndex++)
                    {
                        if (!horizontalVisited[horizontalIndex] && SegmentsIntersect(horizontalSegments[horizontalIndex], vertical))
                        {
                            horizontalVisited[horizontalIndex] = true;
                            horizontalQueue.Enqueue(horizontalIndex);
                        }
                    }
                }
            }

            components.Add(component);
        }

        return components;
    }

    private static bool SegmentsIntersect(GridSegment horizontal, GridSegment vertical)
    {
        return horizontal.Orientation == SegmentOrientation.Horizontal
            && vertical.Orientation == SegmentOrientation.Vertical
            && vertical.FixedCoordinate >= horizontal.Start - CoordinateTolerance
            && vertical.FixedCoordinate <= horizontal.End + CoordinateTolerance
            && horizontal.FixedCoordinate >= vertical.Start - CoordinateTolerance
            && horizontal.FixedCoordinate <= vertical.End + CoordinateTolerance;
    }

    private static IReadOnlyList<PdfTableRegion> MergeOverlappingRegions(IEnumerable<PdfTableRegion> regions)
    {
        var merged = new List<PdfTableRegion>();
        foreach (var region in regions.OrderBy(region => region.PageNumber).ThenBy(region => region.Left).ThenBy(region => region.Bottom))
        {
            var existingIndex = merged.FindIndex(existing => RegionsOverlap(existing, region));
            if (existingIndex < 0)
            {
                merged.Add(region);
                continue;
            }

            var existing = merged[existingIndex];
            merged[existingIndex] = existing with
            {
                Left = Math.Min(existing.Left, region.Left),
                Bottom = Math.Min(existing.Bottom, region.Bottom),
                Right = Math.Max(existing.Right, region.Right),
                Top = Math.Max(existing.Top, region.Top)
            };
        }

        return merged;
    }

    private static bool RegionsOverlap(PdfTableRegion first, PdfTableRegion second)
    {
        return first.PageNumber == second.PageNumber
            && first.Left <= second.Right + CoordinateTolerance
            && first.Right >= second.Left - CoordinateTolerance
            && first.Bottom <= second.Top + CoordinateTolerance
            && first.Top >= second.Bottom - CoordinateTolerance;
    }

    private sealed class GridComponent
    {
        public List<GridSegment> HorizontalSegments { get; } = new();

        public List<GridSegment> VerticalSegments { get; } = new();
    }

    private enum SegmentOrientation
    {
        Horizontal,
        Vertical
    }

    private sealed record GridSegment(
        int PageNumber,
        SegmentOrientation Orientation,
        double FixedCoordinate,
        double Start,
        double End);
}
