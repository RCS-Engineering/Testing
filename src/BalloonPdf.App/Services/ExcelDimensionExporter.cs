using System.Globalization;
using System.IO;
using BalloonPdf.App.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace BalloonPdf.App.Services;

public sealed class ExcelDimensionExporter
{
    public void Export(string templateExcelPath, string outputExcelPath, IReadOnlyCollection<DimensionCandidate> dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateExcelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputExcelPath);
        ArgumentNullException.ThrowIfNull(dimensions);

        var templateFullPath = Path.GetFullPath(templateExcelPath);
        var outputFullPath = Path.GetFullPath(outputExcelPath);
        if (templateFullPath.Equals(outputFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The output Excel workbook path must be different from the template path.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.Copy(templateFullPath, outputFullPath, overwrite: true);

        using var document = SpreadsheetDocument.Open(outputFullPath, true);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("The Excel template is missing a workbook part.");
        var worksheetPart = GetFirstWorksheetPart(workbookPart);
        var sheetData = GetOrCreateSheetData(worksheetPart);

        SetTextCell(sheetData, "B", 15, "Dimension");
        SetTextCell(sheetData, "C", 15, "Balloon Number");

        var rowIndex = 16U;
        foreach (var dimension in dimensions)
        {
            SetTextCell(sheetData, "B", rowIndex, dimension.Text);
            SetNumberCell(sheetData, "C", rowIndex, dimension.BalloonNumber);
            rowIndex++;
        }

        worksheetPart.Worksheet.Save();
        workbookPart.Workbook.Save();
    }

    private static WorksheetPart GetFirstWorksheetPart(WorkbookPart workbookPart)
    {
        var firstSheet = workbookPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new InvalidOperationException("The Excel template does not contain any worksheets.");
        var relationshipId = firstSheet.Id?.Value
            ?? throw new InvalidOperationException("The first worksheet is missing a relationship id.");

        return workbookPart.GetPartById(relationshipId) as WorksheetPart
            ?? throw new InvalidOperationException("The first workbook sheet does not reference a worksheet.");
    }

    private static SheetData GetOrCreateSheetData(WorksheetPart worksheetPart)
    {
        return worksheetPart.Worksheet.GetFirstChild<SheetData>()
            ?? worksheetPart.Worksheet.AppendChild(new SheetData());
    }

    private static Row GetOrCreateRow(SheetData sheetData, uint rowIndex)
    {
        var existingRow = sheetData.Elements<Row>().FirstOrDefault(row => row.RowIndex is not null && row.RowIndex.Value == rowIndex);
        if (existingRow is not null)
        {
            return existingRow;
        }

        var newRow = new Row { RowIndex = rowIndex };
        var nextRow = sheetData.Elements<Row>().FirstOrDefault(row => row.RowIndex is not null && row.RowIndex.Value > rowIndex);
        if (nextRow is null)
        {
            sheetData.Append(newRow);
        }
        else
        {
            sheetData.InsertBefore(newRow, nextRow);
        }

        return newRow;
    }

    private static Cell GetOrCreateCell(SheetData sheetData, string columnName, uint rowIndex)
    {
        var row = GetOrCreateRow(sheetData, rowIndex);
        var cellReference = $"{columnName}{rowIndex}";
        var existingCell = row.Elements<Cell>().FirstOrDefault(cell => cell.CellReference?.Value == cellReference);
        if (existingCell is not null)
        {
            return existingCell;
        }

        var newCell = new Cell { CellReference = cellReference };
        var columnIndex = GetColumnIndex(columnName);
        var nextCell = row.Elements<Cell>()
            .FirstOrDefault(cell => GetColumnIndex(GetColumnName(cell.CellReference?.Value)) > columnIndex);
        if (nextCell is null)
        {
            row.Append(newCell);
        }
        else
        {
            row.InsertBefore(newCell, nextCell);
        }

        return newCell;
    }

    private static void SetTextCell(SheetData sheetData, string columnName, uint rowIndex, string value)
    {
        var cell = GetOrCreateCell(sheetData, columnName, rowIndex);
        cell.CellFormula = null;
        cell.DataType = CellValues.String;
        cell.CellValue = new CellValue(value);
    }

    private static void SetNumberCell(SheetData sheetData, string columnName, uint rowIndex, int value)
    {
        var cell = GetOrCreateCell(sheetData, columnName, rowIndex);
        cell.CellFormula = null;
        cell.DataType = CellValues.Number;
        cell.CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture));
    }

    private static string GetColumnName(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return string.Empty;
        }

        return new string(cellReference.TakeWhile(char.IsLetter).ToArray());
    }

    private static int GetColumnIndex(string columnName)
    {
        var columnIndex = 0;
        foreach (var columnLetter in columnName.ToUpperInvariant())
        {
            if (columnLetter < 'A' || columnLetter > 'Z')
            {
                continue;
            }

            columnIndex = (columnIndex * 26) + columnLetter - 'A' + 1;
        }

        return columnIndex;
    }
}
