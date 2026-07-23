using BalloonPdf.App.Services;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class DimensionDetectorTests
{
    public static TheoryData<string> AcceptedDimensions => new()
    {
        "12.50",
        "Ø10",
        "⌀10",
        "R5",
        "45°",
        "45 deg",
        "1/2",
        ".250",
        "10 ±0.1",
        "10+/-0.1",
        "DIA. 8"
    };

    public static TheoryData<string> RejectedText => new()
    {
        "REV A",
        "SHEET 1 OF 2",
        "UNLESS OTHERWISE SPECIFIED",
        "MATERIAL: ALUMINUM",
        "QTY 4",
        "4",
        "A-1234",
        "TRUE POSITION"
    };

    [Theory]
    [MemberData(nameof(AcceptedDimensions))]
    public void IsLikelyDimension_AcceptsCommonEngineeringDimensionFormats(string text)
    {
        Assert.True(DimensionDetector.IsLikelyDimension(text));
    }

    [Theory]
    [MemberData(nameof(RejectedText))]
    public void IsLikelyDimension_RejectsCommonNonDimensionNotes(string text)
    {
        Assert.False(DimensionDetector.IsLikelyDimension(text));
    }

    [Fact]
    public void IsInBottomRightDetailsBox_ReturnsTrueForCandidateCenteredInsideDetailsBox()
    {
        Assert.True(DimensionDetector.IsInBottomRightDetailsBox(
            left: 800d,
            bottom: 80d,
            right: 840d,
            top: 100d,
            pageWidth: 1000d,
            pageHeight: 800d));
    }

    [Theory]
    [InlineData(650d, 80d, 690d, 100d)]
    [InlineData(800d, 240d, 840d, 260d)]
    public void IsInBottomRightDetailsBox_ReturnsFalseForCandidateOutsideDetailsBox(
        double left,
        double bottom,
        double right,
        double top)
    {
        Assert.False(DimensionDetector.IsInBottomRightDetailsBox(
            left,
            bottom,
            right,
            top,
            pageWidth: 1000d,
            pageHeight: 800d));
    }

    [Theory]
    [InlineData(0d, 800d)]
    [InlineData(1000d, 0d)]
    [InlineData(-1000d, 800d)]
    [InlineData(1000d, -800d)]
    public void IsInBottomRightDetailsBox_ReturnsFalseForInvalidPageDimensions(double pageWidth, double pageHeight)
    {
        Assert.False(DimensionDetector.IsInBottomRightDetailsBox(
            left: 800d,
            bottom: 80d,
            right: 840d,
            top: 100d,
            pageWidth: pageWidth,
            pageHeight: pageHeight));
    }
}
