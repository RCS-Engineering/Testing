using BalloonPdf.App.Models;
using BalloonPdf.App.Services;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class ReadingOrderTests
{
    [Fact]
    public void AssignReadingOrder_SortsByPageTopToBottomThenLeftToRightAndNumbersSequentially()
    {
        var unordered = new[]
        {
            Candidate(page: 2, text: "P2", left: 10, top: 700),
            Candidate(page: 1, text: "Lower", left: 10, top: 100),
            Candidate(page: 1, text: "UpperRight", left: 300, top: 700),
            Candidate(page: 1, text: "UpperLeft", left: 50, top: 700)
        };

        var ordered = DimensionDetector.AssignReadingOrder(unordered);

        Assert.Collection(
            ordered,
            first => AssertCandidate(first, "UpperLeft", 1),
            second => AssertCandidate(second, "UpperRight", 2),
            third => AssertCandidate(third, "Lower", 3),
            fourth => AssertCandidate(fourth, "P2", 4));
    }

    private static DimensionCandidate Candidate(int page, string text, double left, double top)
    {
        return new DimensionCandidate(page, text, left, top - 10, left + 20, top, 0);
    }

    private static void AssertCandidate(DimensionCandidate candidate, string text, int balloonNumber)
    {
        Assert.Equal(text, candidate.Text);
        Assert.Equal(balloonNumber, candidate.BalloonNumber);
    }
}
