using System.Globalization;
using System.IO;
using BalloonPdf.App.Models;
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
        if (!File.Exists(templateFullPath))
        {
            throw new FileNotFoundException("The Excel template workbook does not exist.", templateFullPath);
        }

        var outputFullPath = Path.GetFullPath(outputExcelPath);
        if (templateFullPath.Equals(outputFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The output Excel workbook must be separate from the template workbook.", nameof(outputExcelPath));
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.Copy(templateFullPath, outputFullPath, overwrite: true);

        using var document = SpreadsheetDocument.Open(outputFullPath, true);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Template workbook is missing a workbook part.");
        var workbook = workbookPart.Workbook ?? throw new InvalidOperationException("Template workbook is missing workbook metadata.");
        var firstSheet = workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new InvalidOperationException("Template workbook does not contain a worksheet.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(firstSheet.Id?.Value ?? throw new InvalidOperationException("Template worksheet is missing a relationship id."));
        var worksheet = worksheetPart.Worksheet ?? throw new InvalidOperationException("Template worksheet is missing worksheet data.");
        var sheetData = GetOrCreateSheetData(worksheet);

        SetTextCell(sheetData, "B", 15, "Dimension");
        SetTextCell(sheetData, "C", 15, "Balloon Number");

        var rowIndex = 16U;
        foreach (var dimension in dimensions)
        {
            SetTextCell(sheetData, "B", rowIndex, dimension.Text);
            SetNumberCell(sheetData, "C", rowIndex, dimension.BalloonNumber);
            rowIndex++;
        }

        worksheet.Save();
        workbook.Save();
    }

    private static SheetData GetOrCreateSheetData(Worksheet worksheet)
    {
        var sheetData = worksheet.GetFirstChild<SheetData>();
        if (sheetData is not null)
        {
            return sheetData;
        }

        sheetData = new SheetData();
        worksheet.PrependChild(sheetData);
        return sheetData;
    }

    private static void SetTextCell(SheetData sheetData, string columnName, uint rowIndex, string value)
    {
        var cell = GetOrCreateCell(sheetData, columnName, rowIndex);
        cell.DataType = CellValues.String;
        cell.CellValue = new CellValue(value);
    }

    private static void SetNumberCell(SheetData sheetData, string columnName, uint rowIndex, int value)
    {
        var cell = GetOrCreateCell(sheetData, columnName, rowIndex);
        cell.DataType = CellValues.Number;
        cell.CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture));
    }

    private static Cell GetOrCreateCell(SheetData sheetData, string columnName, uint rowIndex)
    {
        var row = GetOrCreateRow(sheetData, rowIndex);
        var cellReference = $"{columnName}{rowIndex}";
        var matchingCells = row.Elements<Cell>()
            .Where(cell => string.Equals(cell.CellReference?.Value, cellReference, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingCells.Count > 0)
        {
            foreach (var duplicate in matchingCells.Skip(1))
            {
                duplicate.Remove();
            }

            return matchingCells[0];
        }

        var newCell = new Cell { CellReference = cellReference };
        var targetColumnIndex = GetColumnIndex(columnName);
        var nextCell = row.Elements<Cell>()
            .FirstOrDefault(cell => GetColumnIndex(GetColumnName(cell.CellReference?.Value)) > targetColumnIndex);

        row.InsertBefore(newCell, nextCell);
        return newCell;
    }

    private static Row GetOrCreateRow(SheetData sheetData, uint rowIndex)
    {
        var existingRow = sheetData.Elements<Row>().FirstOrDefault(row => row.RowIndex?.Value == rowIndex);
        if (existingRow is not null)
        {
            return existingRow;
        }

        var newRow = new Row { RowIndex = rowIndex };
        var nextRow = sheetData.Elements<Row>().FirstOrDefault(row => row.RowIndex?.Value > rowIndex);
        sheetData.InsertBefore(newRow, nextRow);
        return newRow;
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
        foreach (var character in columnName.ToUpperInvariant())
        {
            columnIndex *= 26;
            columnIndex += character - 'A' + 1;
        }

        return columnIndex;
    }
}
