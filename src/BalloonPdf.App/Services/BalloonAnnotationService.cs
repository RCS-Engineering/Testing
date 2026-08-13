using BalloonPdf.App.Models;

namespace BalloonPdf.App.Services;

public sealed class BalloonAnnotationService
{
    public IReadOnlyList<BalloonAnnotation> CreateFromDimensions(IEnumerable<DimensionCandidate> dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);

        var normalizedDimensions = GroupVerticalDigitCandidates(dimensions.ToList());

        return normalizedDimensions
            .OrderBy(dimension => dimension.PageNumber)
            .ThenBy(dimension => dimension.BalloonNumber <= 0)
            .ThenBy(dimension => dimension.BalloonNumber > 0 ? dimension.BalloonNumber : int.MaxValue)
            .ThenByDescending(dimension => dimension.BalloonNumber > 0 ? double.NegativeInfinity : dimension.Top)
            .ThenBy(dimension => dimension.BalloonNumber > 0 ? double.NegativeInfinity : dimension.Left)
            .Select((dimension, index) => BalloonAnnotation.Create(
                dimension.PageNumber,
                dimension.Right + PdfBalloonAnnotator.BalloonOffsetX,
                dimension.CenterY,
                index + 1,
                dimension.Text,
                BalloonAnnotation.DefaultStrokeColorHex,
                PdfBalloonAnnotator.BalloonRadius,
                dimension.Right,
                dimension.CenterY))
            .ToList();
    }

    private static IReadOnlyList<DimensionCandidate> GroupVerticalDigitCandidates(IReadOnlyList<DimensionCandidate> dimensions)
    {
        if (dimensions.Count(candidate => IsSingleDigit(candidate)) < 2)
        {
            return dimensions;
        }

        var grouping = VerticalIntegerCandidateGrouper.Build(dimensions, _ => true);
        if (grouping.MergedCandidates.Count == 0)
        {
            return dimensions;
        }

        var normalizedDimensions = new List<DimensionCandidate>();
        foreach (var sourceRun in grouping.SourceRuns)
        {
            var firstSourceBalloonNumber = sourceRun.SourceCandidates
                .Select(candidate => candidate.BalloonNumber)
                .FirstOrDefault(balloonNumber => balloonNumber > 0);
            normalizedDimensions.Add(firstSourceBalloonNumber > 0
                ? sourceRun.MergedCandidate with { BalloonNumber = firstSourceBalloonNumber }
                : sourceRun.MergedCandidate);
        }

        normalizedDimensions.AddRange(dimensions.Where(dimension => !grouping.ContainsSource(dimension)));
        return normalizedDimensions;
    }

    private static bool IsSingleDigit(DimensionCandidate candidate)
    {
        var text = candidate.Text.Trim();
        return text.Length == 1 && char.IsDigit(text[0]);
    }

    public IReadOnlyList<BalloonAnnotation> Add(
        IEnumerable<BalloonAnnotation> annotations,
        int pageNumber,
        double centerX,
        double centerY,
        string? sourceText = null,
        string? strokeColorHex = null)
    {
        var current = RequireAnnotations(annotations);
        var nextNumber = current.Count == 0 ? 1 : current.Max(annotation => annotation.BalloonNumber) + 1;
        current.Add(BalloonAnnotation.Create(
            pageNumber,
            centerX,
            centerY,
            nextNumber,
            sourceText,
            NormalizeColor(strokeColorHex ?? BalloonAnnotation.DefaultStrokeColorHex),
            PdfBalloonAnnotator.BalloonRadius));
        return GetOrdered(current);
    }

    public IReadOnlyList<BalloonAnnotation> Delete(IEnumerable<BalloonAnnotation> annotations, Guid annotationId)
    {
        return GetOrdered(RequireAnnotations(annotations).Where(annotation => annotation.Id != annotationId));
    }

    public IReadOnlyList<BalloonAnnotation> UpdateNumber(IEnumerable<BalloonAnnotation> annotations, Guid annotationId, int balloonNumber)
    {
        if (balloonNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balloonNumber), "Balloon numbers must be positive.");
        }

        return GetOrdered(RequireAnnotations(annotations).Select(annotation =>
            annotation.Id == annotationId
                ? annotation with { BalloonNumber = balloonNumber }
                : annotation));
    }

    public IReadOnlyList<BalloonAnnotation> UpdateColor(IEnumerable<BalloonAnnotation> annotations, Guid annotationId, string strokeColorHex)
    {
        var normalizedColor = NormalizeColor(strokeColorHex);
        return GetOrdered(RequireAnnotations(annotations).Select(annotation =>
            annotation.Id == annotationId
                ? annotation with { StrokeColorHex = normalizedColor }
                : annotation));
    }

    public IReadOnlyList<BalloonAnnotation> GetOrdered(IEnumerable<BalloonAnnotation> annotations)
    {
        return RequireAnnotations(annotations)
            .OrderBy(annotation => annotation.PageNumber)
            .ThenBy(annotation => annotation.BalloonNumber)
            .ThenBy(annotation => annotation.CenterY)
            .ThenBy(annotation => annotation.CenterX)
            .ToList();
    }

    private static List<BalloonAnnotation> RequireAnnotations(IEnumerable<BalloonAnnotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        return annotations.ToList();
    }

    private static string NormalizeColor(string strokeColorHex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeColorHex);

        var color = strokeColorHex.Trim();
        if (!color.StartsWith('#'))
        {
            color = $"#{color}";
        }

        if (color.Length != 7 || color.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Balloon colors must use #RRGGBB hex format.", nameof(strokeColorHex));
        }

        return color.ToUpperInvariant();
    }
}
