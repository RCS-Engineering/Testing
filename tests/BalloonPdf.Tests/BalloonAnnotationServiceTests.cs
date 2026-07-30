using BalloonPdf.App.Models;
using BalloonPdf.App.Services;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class BalloonAnnotationServiceTests
{
    private readonly BalloonAnnotationService service = new();

    [Fact]
    public void CreateFromDimensions_PreservesPageTextAndBalloonNumbers()
    {
        var dimensions = new[]
        {
            new DimensionCandidate(2, "45°", 15, 25, 35, 45, 3),
            new DimensionCandidate(1, "Ø.375", 50, 60, 70, 80, 1),
            new DimensionCandidate(1, "1.250", 10, 20, 30, 40, 2)
        };

        var annotations = service.CreateFromDimensions(dimensions);

        Assert.Collection(
            annotations,
            first => AssertAnnotation(first, pageNumber: 1, sourceText: "Ø.375", balloonNumber: 1, centerX: 90, centerY: 70, targetX: 70, targetY: 70),
            second => AssertAnnotation(second, pageNumber: 1, sourceText: "1.250", balloonNumber: 2, centerX: 50, centerY: 30, targetX: 30, targetY: 30),
            third => AssertAnnotation(third, pageNumber: 2, sourceText: "45°", balloonNumber: 3, centerX: 55, centerY: 35, targetX: 35, targetY: 35));
    }

    [Fact]
    public void Delete_RemovesSelectedAnnotationWithoutChangingOthers()
    {
        var annotations = SampleAnnotations();
        var deletedId = annotations[1].Id;

        var updated = service.Delete(annotations, deletedId);

        Assert.DoesNotContain(updated, annotation => annotation.Id == deletedId);
        Assert.Equal(new[] { annotations[0].Id, annotations[2].Id }, updated.Select(annotation => annotation.Id));
        Assert.Equal(new[] { 1, 3 }, updated.Select(annotation => annotation.BalloonNumber));
    }

    [Fact]
    public void UpdateNumber_ChangesOnlySelectedAnnotation()
    {
        var annotations = SampleAnnotations();

        var updated = service.UpdateNumber(annotations, annotations[1].Id, 42);

        Assert.Equal(42, updated.Single(annotation => annotation.Id == annotations[1].Id).BalloonNumber);
        Assert.Equal(1, updated.Single(annotation => annotation.Id == annotations[0].Id).BalloonNumber);
        Assert.Equal(3, updated.Single(annotation => annotation.Id == annotations[2].Id).BalloonNumber);
    }

    [Fact]
    public void Add_CreatesNewAnnotationAtRequestedPageAndPosition()
    {
        var annotations = SampleAnnotations();

        var updated = service.Add(annotations, pageNumber: 2, centerX: 125.5d, centerY: 225.25d);

        var added = Assert.Single(updated.Where(annotation => annotations.All(existing => existing.Id != annotation.Id)));
        Assert.Equal(2, added.PageNumber);
        Assert.Equal(125.5d, added.CenterX);
        Assert.Equal(225.25d, added.CenterY);
        Assert.Equal(4, added.BalloonNumber);
        Assert.Equal(BalloonAnnotation.DefaultStrokeColorHex, added.StrokeColorHex);
        Assert.Null(added.TargetX);
        Assert.Null(added.TargetY);
    }

    [Fact]
    public void UpdateColor_PersistsHexColorOnSelectedAnnotation()
    {
        var annotations = SampleAnnotations();

        var updated = service.UpdateColor(annotations, annotations[0].Id, "ff0000");

        Assert.Equal("#FF0000", updated.Single(annotation => annotation.Id == annotations[0].Id).StrokeColorHex);
        Assert.Equal(BalloonAnnotation.DefaultStrokeColorHex, updated.Single(annotation => annotation.Id == annotations[1].Id).StrokeColorHex);
    }

    private static IReadOnlyList<BalloonAnnotation> SampleAnnotations()
    {
        return new[]
        {
            BalloonAnnotation.Create(1, 10, 20, 1, "A"),
            BalloonAnnotation.Create(1, 30, 40, 2, "B"),
            BalloonAnnotation.Create(2, 50, 60, 3, "C")
        };
    }

    private static void AssertAnnotation(
        BalloonAnnotation annotation,
        int pageNumber,
        string sourceText,
        int balloonNumber,
        double centerX,
        double centerY,
        double targetX,
        double targetY)
    {
        Assert.Equal(pageNumber, annotation.PageNumber);
        Assert.Equal(sourceText, annotation.SourceText);
        Assert.Equal(balloonNumber, annotation.BalloonNumber);
        Assert.Equal(centerX, annotation.CenterX);
        Assert.Equal(centerY, annotation.CenterY);
        Assert.Equal(targetX, annotation.TargetX);
        Assert.Equal(targetY, annotation.TargetY);
        Assert.Equal("#0000FF", annotation.StrokeColorHex);
        Assert.Equal("#0000FF", BalloonAnnotation.DefaultStrokeColorHex);
    }
}
