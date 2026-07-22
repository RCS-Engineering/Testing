using System.Globalization;
using System.IO;
using BalloonPdf.App.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace BalloonPdf.App.Services;

public sealed class ExcelDimensionExporter
{
    public void Export(string outputExcelPath, IReadOnlyCollection<DimensionCandidate> dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputExcelPath);
        ArgumentNullException.ThrowIfNull(dimensions);

        var outputFullPath = Path.GetFullPath(outputExcelPath);
        var outputDirectory = Path.GetDirectoryName(outputFullPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var document = SpreadsheetDocument.Create(outputFullPath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "Dimensions"
        });

        sheetData.Append(CreateTextRow(1, "Dimension", "Balloon Number"));

        var rowIndex = 2U;
        foreach (var dimension in dimensions)
        {
            sheetData.Append(CreateDimensionRow(rowIndex, dimension));
            rowIndex++;
        }

        workbookPart.Workbook.Save();
    }

    private static Row CreateTextRow(uint rowIndex, string firstValue, string secondValue)
    {
        var row = new Row { RowIndex = rowIndex };
        row.Append(CreateTextCell(rowIndex, "A", firstValue));
        row.Append(CreateTextCell(rowIndex, "B", secondValue));
        return row;
    }

    private static Row CreateDimensionRow(uint rowIndex, DimensionCandidate dimension)
    {
        var row = new Row { RowIndex = rowIndex };
        row.Append(CreateTextCell(rowIndex, "A", dimension.Text));
        row.Append(CreateNumberCell(rowIndex, "B", dimension.BalloonNumber));
        return row;
    }

    private static Cell CreateTextCell(uint rowIndex, string columnName, string value)
    {
        return new Cell
        {
            CellReference = $"{columnName}{rowIndex}",
            DataType = CellValues.String,
            CellValue = new CellValue(value)
        };
    }

    private static Cell CreateNumberCell(uint rowIndex, string columnName, int value)
    {
        return new Cell
        {
            CellReference = $"{columnName}{rowIndex}",
            DataType = CellValues.Number,
            CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture))
        };
    }
}
