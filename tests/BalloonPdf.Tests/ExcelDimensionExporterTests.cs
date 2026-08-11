using System.IO;
using BalloonPdf.App.Models;
using BalloonPdf.App.Services;
using DocumentFormat.OpenXml;
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
    public void Export_CopiesTemplateAndWritesDimensionsToFirstWorksheet()
    {
        var templatePath = Path.Combine(tempDirectory, "template.xlsx");
        var outputPath = Path.Combine(tempDirectory, "nested", "dimensions.xlsx");
        CreateTemplateWorkbook(templatePath);
        var dimensions = new[]
        {
            new DimensionCandidate(1, "1.250", 10, 20, 30, 40, 2),
            new DimensionCandidate(1, "Ø.375", 50, 60, 70, 80, 1),
            new DimensionCandidate(2, "45°", 15, 25, 35, 45, 3)
        };

        exporter.Export(templatePath, outputPath, dimensions);

        Assert.True(File.Exists(outputPath));
        Assert.Equal("Template Sheet", ReadFirstSheetName(outputPath));
        Assert.Equal("Keep me", ReadCellValue(outputPath, "A1"));
        Assert.Equal("Dimension", ReadCellValue(outputPath, "B15"));
        Assert.Equal("Balloon Number", ReadCellValue(outputPath, "C15"));
        Assert.Equal("1.250", ReadCellValue(outputPath, "B16"));
        Assert.Equal("2", ReadCellValue(outputPath, "C16"));
        Assert.Equal("Ø.375", ReadCellValue(outputPath, "B17"));
        Assert.Equal("1", ReadCellValue(outputPath, "C17"));
        Assert.Equal("45°", ReadCellValue(outputPath, "B18"));
        Assert.Equal("3", ReadCellValue(outputPath, "C18"));
        Assert.Equal("Template tail", ReadCellValue(outputPath, "D20"));
    }

    [Fact]
    public void Export_UpdatesExistingTargetCellsWithoutDuplicatingCellReferences()
    {
        var templatePath = Path.Combine(tempDirectory, "template.xlsx");
        var outputPath = Path.Combine(tempDirectory, "dimensions.xlsx");
        CreateTemplateWorkbook(templatePath);
        var dimensions = new[]
        {
            new DimensionCandidate(1, "5.500", 10, 20, 30, 40, 7)
        };

        exporter.Export(templatePath, outputPath, dimensions);

        Assert.Equal("Dimension", ReadCellValue(outputPath, "B15"));
        Assert.Equal("Balloon Number", ReadCellValue(outputPath, "C15"));
        Assert.Equal("5.500", ReadCellValue(outputPath, "B16"));
        Assert.Equal("7", ReadCellValue(outputPath, "C16"));
        Assert.Equal(1, CountCells(outputPath, "B15"));
        Assert.Equal(1, CountCells(outputPath, "C15"));
        Assert.Equal(1, CountCells(outputPath, "B16"));
        Assert.Equal(1, CountCells(outputPath, "C16"));
    }

    [Fact]
    public void Export_DoesNotModifyTemplateWorkbook()
    {
        var templatePath = Path.Combine(tempDirectory, "template.xlsx");
        var outputPath = Path.Combine(tempDirectory, "dimensions.xlsx");
        CreateTemplateWorkbook(templatePath);
        var dimensions = new[]
        {
            new DimensionCandidate(1, "9.000", 10, 20, 30, 40, 9)
        };

        exporter.Export(templatePath, outputPath, dimensions);

        Assert.Equal("Old header", ReadCellValue(templatePath, "B15"));
        Assert.Equal("Old number", ReadCellValue(templatePath, "C15"));
        Assert.Equal("Old dimension", ReadCellValue(templatePath, "B16"));
        Assert.Equal("99", ReadCellValue(templatePath, "C16"));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static void CreateTemplateWorkbook(string workbookPath)
    {
        using var document = SpreadsheetDocument.Create(workbookPath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);
        sheetData.Append(
            CreateRow(1, CreateTextCell("A", 1, "Keep me")),
            CreateRow(15, CreateTextCell("B", 15, "Old header"), CreateTextCell("C", 15, "Old number")),
            CreateRow(16, CreateTextCell("B", 16, "Old dimension"), CreateNumberCell("C", 16, 99)),
            CreateRow(20, CreateTextCell("D", 20, "Template tail")));

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "Template Sheet"
        });

        workbookPart.Workbook.Save();
    }

    private static Row CreateRow(uint rowIndex, params Cell[] cells)
    {
        var row = new Row { RowIndex = rowIndex };
        row.Append(cells);
        return row;
    }

    private static Cell CreateTextCell(string columnName, uint rowIndex, string value)
    {
        return new Cell
        {
            CellReference = $"{columnName}{rowIndex}",
            DataType = CellValues.String,
            CellValue = new CellValue(value)
        };
    }

    private static Cell CreateNumberCell(string columnName, uint rowIndex, int value)
    {
        return new Cell
        {
            CellReference = $"{columnName}{rowIndex}",
            DataType = CellValues.Number,
            CellValue = new CellValue(value)
        };
    }

    private static string ReadFirstSheetName(string workbookPath)
    {
        using var document = SpreadsheetDocument.Open(workbookPath, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook is missing a workbook part.");
        var workbook = workbookPart.Workbook ?? throw new InvalidOperationException("Workbook is missing workbook metadata.");
        return workbook.Sheets?.Elements<Sheet>().First().Name?.Value ?? string.Empty;
    }

    private static string ReadCellValue(string workbookPath, string cellReference)
    {
        using var document = SpreadsheetDocument.Open(workbookPath, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook is missing a workbook part.");
        var worksheetPart = GetFirstWorksheetPart(workbookPart);
        var worksheet = worksheetPart.Worksheet ?? throw new InvalidOperationException("Worksheet is missing worksheet data.");
        var cell = worksheet.Descendants<Cell>()
            .FirstOrDefault(cell => cell.CellReference?.Value == cellReference)
            ?? throw new InvalidOperationException($"Cell {cellReference} was not found.");

        return ReadCellValue(workbookPart, cell);
    }

    private static int CountCells(string workbookPath, string cellReference)
    {
        using var document = SpreadsheetDocument.Open(workbookPath, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook is missing a workbook part.");
        var worksheetPart = GetFirstWorksheetPart(workbookPart);
        var worksheet = worksheetPart.Worksheet ?? throw new InvalidOperationException("Worksheet is missing worksheet data.");
        return worksheet.Descendants<Cell>()
            .Count(cell => cell.CellReference?.Value == cellReference);
    }

    private static WorksheetPart GetFirstWorksheetPart(WorkbookPart workbookPart)
    {
        var workbook = workbookPart.Workbook ?? throw new InvalidOperationException("Workbook is missing workbook metadata.");
        var firstSheet = workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new InvalidOperationException("Workbook does not contain a worksheet.");
        return (WorksheetPart)workbookPart.GetPartById(firstSheet.Id?.Value ?? throw new InvalidOperationException("Worksheet is missing a relationship id."));
    }

    private static string ReadCellValue(WorkbookPart workbookPart, Cell cell)
    {
        var value = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;
            return sharedStringTable?.ElementAt(int.Parse(value)).InnerText ?? string.Empty;
        }

        return value;
    }
}
