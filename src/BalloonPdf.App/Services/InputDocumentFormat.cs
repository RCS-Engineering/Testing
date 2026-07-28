using System.IO;

namespace BalloonPdf.App.Services;

public enum InputDocumentFormat
{
    Pdf,
    Jpeg
}

public static class InputDocumentFormatExtensions
{
    public const string SupportedFileDialogFilter = "Supported drawings (*.pdf;*.jpg;*.jpeg)|*.pdf;*.jpg;*.jpeg|PDF files (*.pdf)|*.pdf|JPEG images (*.jpg;*.jpeg)|*.jpg;*.jpeg|All files (*.*)|*.*";

    public static InputDocumentFormat FromPath(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var extension = Path.GetExtension(inputPath).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => InputDocumentFormat.Pdf,
            ".jpg" or ".jpeg" => InputDocumentFormat.Jpeg,
            _ => throw new NotSupportedException("Supported input formats are PDF, JPG, and JPEG.")
        };
    }

    public static bool IsSupported(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return false;
        }

        try
        {
            _ = FromPath(inputPath);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
