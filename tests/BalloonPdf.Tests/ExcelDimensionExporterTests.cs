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
    public void Export_CopiesTemplateAndWritesHeadersAndDimensionsInInputOrder()
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
        Assert.Equal("Dimension", ReadCellValue(outputPath, "B15"));
        Assert.Equal("Balloon Number", ReadCellValue(outputPath, "C15"));
        Assert.Equal("1.250", ReadCellValue(outputPath, "B16"));
        Assert.Equal("2", ReadCellValue(outputPath, "C16"));
        Assert.Equal("Ø.375", ReadCellValue(outputPath, "B17"));
        Assert.Equal("1", ReadCellValue(outputPath, "C17"));
        Assert.Equal("45°", ReadCellValue(outputPath, "B18"));
        Assert.Equal("3", ReadCellValue(outputPath, "C18"));
        Assert.Equal("template sentinel", ReadCellValue(outputPath, "A15"));
        Assert.Equal("neighbor sentinel", ReadCellValue(outputPath, "D16"));
        Assert.Equal(1U, ReadStyleIndex(outputPath, "B15"));
        Assert.Equal(1U, ReadStyleIndex(outputPath, "C16"));
    }

    [Fact]
    public void Export_LeavesSourceTemplateUnmodified()
    {
        var templatePath = Path.Combine(tempDirectory, "template.xlsx");
        var outputPath = Path.Combine(tempDirectory, "dimensions.xlsx");
        CreateTemplateWorkbook(templatePath);

        exporter.Export(
            templatePath,
            outputPath,
            new[] { new DimensionCandidate(1, ".500", 10, 20, 30, 40, 1) });

        Assert.True(File.Exists(templatePath));
        Assert.Equal("existing header", ReadCellValue(templatePath, "B15"));
        Assert.Null(ReadCellValue(templatePath, "B16"));
        Assert.Equal("999", ReadCellValue(templatePath, "C16"));
        Assert.Equal("template sentinel", ReadCellValue(templatePath, "A15"));
        Assert.Equal("neighbor sentinel", ReadCellValue(templatePath, "D16"));
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
        AddStyles(workbookPart);

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData(
            new Row(
                CreateTextCell("A15", "template sentinel"),
                CreateTextCell("B15", "existing header", 1),
                CreateTextCell("D15", "right sentinel"))
            {
                RowIndex = 15
            },
            new Row(
                CreateTextCell("A16", "left sentinel"),
                CreateNumberCell("C16", 999, 1),
                CreateTextCell("D16", "neighbor sentinel"))
            {
                RowIndex = 16
            });
        worksheetPart.Worksheet = new Worksheet(sheetData);

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "Template"
        });

        workbookPart.Workbook.Save();
    }

    private static void AddStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet(
            new Fonts(new Font()) { Count = 1 },
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
            {
                Count = 2
            },
            new Borders(new Border()) { Count = 1 },
            new CellFormats(
                new CellFormat(),
                new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, ApplyFont = true })
            {
                Count = 2
            });
        stylesPart.Stylesheet.Save();
    }

    private static Cell CreateTextCell(string cellReference, string value, uint? styleIndex = null)
    {
        var cell = new Cell
        {
            CellReference = cellReference,
            DataType = CellValues.String,
            CellValue = new CellValue(value)
        };
        if (styleIndex.HasValue)
        {
            cell.StyleIndex = styleIndex.Value;
        }

        return cell;
    }

    private static Cell CreateNumberCell(string cellReference, int value, uint? styleIndex = null)
    {
        var cell = new Cell
        {
            CellReference = cellReference,
            DataType = CellValues.Number,
            CellValue = new CellValue(value.ToString())
        };
        if (styleIndex.HasValue)
        {
            cell.StyleIndex = styleIndex.Value;
        }

        return cell;
    }

    private static string? ReadCellValue(string workbookPath, string cellReference)
    {
        using var document = SpreadsheetDocument.Open(workbookPath, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook is missing a workbook part.");
        var cell = GetFirstWorksheetCell(workbookPart, cellReference);
        if (cell is null)
        {
            return null;
        }

        var value = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            return workbookPart.SharedStringTablePart?.SharedStringTable.ElementAt(int.Parse(value)).InnerText ?? string.Empty;
        }

        return value;
    }

    private static uint? ReadStyleIndex(string workbookPath, string cellReference)
    {
        using var document = SpreadsheetDocument.Open(workbookPath, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Workbook is missing a workbook part.");
        return GetFirstWorksheetCell(workbookPart, cellReference)?.StyleIndex?.Value;
    }

    private static Cell? GetFirstWorksheetCell(WorkbookPart workbookPart, string cellReference)
    {
        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new InvalidOperationException("Workbook does not contain a worksheet.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id?.Value ?? throw new InvalidOperationException("Worksheet is missing an id."));
        return worksheetPart.Worksheet.GetFirstChild<SheetData>()?
            .Descendants<Cell>()
            .FirstOrDefault(cell => cell.CellReference?.Value == cellReference);
    }
}
