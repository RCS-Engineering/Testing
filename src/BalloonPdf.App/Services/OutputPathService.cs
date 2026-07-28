using System.IO;

namespace BalloonPdf.App.Services;

public sealed class OutputPathService
{
    public string GetDefaultOutputPath(string inputPdfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPdfPath);

        return GetDefaultPath(Path.GetFullPath(inputPdfPath), ".pdf");
    }

    public string GetDefaultExcelOutputPath(string inputPdfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPdfPath);

        return GetDefaultPath(Path.GetFullPath(inputPdfPath), ".xlsx");
    }

    private static string GetDefaultPath(string inputFullPath, string extension)
    {
        var directory = Path.GetDirectoryName(inputFullPath) ?? Directory.GetCurrentDirectory();
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputFullPath);
        var candidate = Path.Combine(directory, $"{fileNameWithoutExtension}_balloons{extension}");
        var index = 1;
        while (Path.GetFullPath(candidate).Equals(inputFullPath, StringComparison.OrdinalIgnoreCase) || File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{fileNameWithoutExtension}_balloons_{index}{extension}");
            index++;
        }

        return candidate;
    }
}
