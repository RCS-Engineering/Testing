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
}
