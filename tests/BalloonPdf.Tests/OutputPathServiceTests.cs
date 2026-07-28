using System.IO;
using BalloonPdf.App.Services;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class OutputPathServiceTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "BalloonPdfTests", Guid.NewGuid().ToString("N"));
    private readonly OutputPathService service = new();

    public OutputPathServiceTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void GetDefaultOutputPath_AppendsBalloonsSuffixBesideInputPdf()
    {
        var input = Path.Combine(tempDirectory, "part.pdf");

        var output = service.GetDefaultOutputPath(input);

        Assert.Equal(Path.Combine(tempDirectory, "part_balloons.pdf"), output);
        Assert.NotEqual(Path.GetFullPath(input), Path.GetFullPath(output), StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("part.jpg")]
    [InlineData("part.jpeg")]
    public void GetDefaultOutputPath_UsesPdfExtensionForJpegInputs(string fileName)
    {
        var input = Path.Combine(tempDirectory, fileName);

        var output = service.GetDefaultOutputPath(input);

        Assert.Equal(Path.Combine(tempDirectory, "part_balloons.pdf"), output);
    }

    [Fact]
    public void GetDefaultOutputPath_IncrementsWhenDefaultAlreadyExists()
    {
        var input = Path.Combine(tempDirectory, "part.pdf");
        File.WriteAllText(Path.Combine(tempDirectory, "part_balloons.pdf"), "existing");

        var output = service.GetDefaultOutputPath(input);

        Assert.Equal(Path.Combine(tempDirectory, "part_balloons_1.pdf"), output);
    }

    [Fact]
    public void GetDefaultOutputPath_NeverReturnsInputPathWhenInputAlreadyHasBalloonsName()
    {
        var input = Path.Combine(tempDirectory, "part_balloons.pdf");

        var output = service.GetDefaultOutputPath(input);

        Assert.Equal(Path.Combine(tempDirectory, "part_balloons_balloons.pdf"), output);
        Assert.NotEqual(Path.GetFullPath(input), Path.GetFullPath(output), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDefaultExcelOutputPath_AppendsBalloonsXlsxSuffixBesideInputPdf()
    {
        var input = Path.Combine(tempDirectory, "part.pdf");

        var output = service.GetDefaultExcelOutputPath(input);

        Assert.Equal(Path.Combine(tempDirectory, "part_balloons.xlsx"), output);
        Assert.NotEqual(Path.GetFullPath(input), Path.GetFullPath(output), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDefaultExcelOutputPath_IncrementsWhenDefaultAlreadyExists()
    {
        var input = Path.Combine(tempDirectory, "part.pdf");
        File.WriteAllText(Path.Combine(tempDirectory, "part_balloons.xlsx"), "existing");

        var output = service.GetDefaultExcelOutputPath(input);

        Assert.Equal(Path.Combine(tempDirectory, "part_balloons_1.xlsx"), output);
    }

    [Fact]
    public void GetDefaultExcelOutputPath_NeverReturnsInputPathForUnusualInputNames()
    {
        var input = Path.Combine(tempDirectory, "part_balloons.xlsx");

        var output = service.GetDefaultExcelOutputPath(input);

        Assert.Equal(Path.Combine(tempDirectory, "part_balloons_balloons.xlsx"), output);
        Assert.NotEqual(Path.GetFullPath(input), Path.GetFullPath(output), StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
