using System.IO;
using BalloonPdf.App.Models;
using BalloonPdf.App.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class ExcelDimensionExporterTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "BalloonPdfTests", Guid.NewGuid().ToString("N"));
    private readonly ExcelDimensionExporter exporter = new();

    public ExcelDimensionExporterTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void Export_CreatesWorkbookWithHeadersAndDimensionsInInputOrder()
    {
        var outputPath = Path.Combine(tempDirectory, "nested", "dimensions.xlsx");
        var dimensions = new[]
        {
            new DimensionCandidate(1, "1.250", 10, 20, 30, 40, 2),
            new DimensionCandidate(1, "Ø.375", 50, 60, 70, 80, 1),
            new DimensionCandidate(2, "45°", 15, 25, 35, 45, 3)
        };

        exporter.Export(outputPath, dimensions);

        Assert.True(File.Exists(outputPath));
        Assert.Equal(
            new[]
            {
                new[] { "Dimension", "Balloon Number" },
                new[] { "1.250", "2" },
                new[] { "Ø.375", "1" },
                new[] { "45°", "3" }
            },
            ReadRows(outputPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadRows(string workbookPath)
    {
        using var document = SpreadsheetDocument.Open(workbookPath, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook is missing a workbook part.");
        var worksheetPart = workbookPart.WorksheetParts.Single();
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>() ?? throw new InvalidOperationException("Worksheet is missing sheet data.");

        return sheetData.Elements<Row>()
            .Select(row => row.Elements<Cell>().Select(cell => ReadCellValue(workbookPart, cell)).ToList())
            .ToList();
    }

    private static string ReadCellValue(WorkbookPart workbookPart, Cell cell)
    {
        var value = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            return workbookPart.SharedStringTablePart?.SharedStringTable.ElementAt(int.Parse(value)).InnerText ?? string.Empty;
        }

        return value;
    }
}
