using System.IO;
using Tesseract;

namespace BalloonPdf.App.Services;

public interface IImageTextExtractor
{
    IReadOnlyList<ImageTextWord> ExtractWords(string imagePath);
}

public sealed record ImageTextWord(string Text, int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}

public sealed class TesseractImageTextExtractor : IImageTextExtractor
{
    internal const string LanguageCode = "eng";
    internal const string ExpectedLanguageDataFileName = LanguageCode + ".traineddata";
    internal const string TessdataDownloadUrl = "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata";

    private const string TessdataEnvironmentVariableName = "TESSDATA_PREFIX";
    private const string AppLocalTessdataDirectoryName = "tessdata";

    public IReadOnlyList<ImageTextWord> ExtractWords(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("The image file was not found.", imagePath);
        }

        var tessdataPath = ResolveTessdataPath();
        using var engine = new TesseractEngine(tessdataPath, LanguageCode, EngineMode.Default);
        using var image = Pix.LoadFromFile(imagePath);
        using var page = engine.Process(image);
        using var iterator = page.GetIterator();
        iterator.Begin();

        var words = new List<ImageTextWord>();
        do
        {
            var text = iterator.GetText(PageIteratorLevel.Word);
            if (string.IsNullOrWhiteSpace(text) || !iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds))
            {
                continue;
            }

            words.Add(new ImageTextWord(text.Trim(), bounds.X1, bounds.Y1, bounds.X2, bounds.Y2));
        }
        while (iterator.Next(PageIteratorLevel.Word));

        return words;
    }

    private static string ResolveTessdataPath()
    {
        return ResolveTessdataPath(new[]
        {
            Environment.GetEnvironmentVariable(TessdataEnvironmentVariableName),
            Path.Combine(AppContext.BaseDirectory, AppLocalTessdataDirectoryName)
        });
    }

    internal static string ResolveTessdataPath(IEnumerable<string?> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var checkedPaths = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => Path.GetFullPath(candidate!))
            .ToArray();

        foreach (var checkedPath in checkedPaths)
        {
            if (File.Exists(Path.Combine(checkedPath, ExpectedLanguageDataFileName)))
            {
                return checkedPath;
            }
        }

        throw new InvalidOperationException(BuildMissingLanguageDataMessage(checkedPaths));
    }

    private static string BuildMissingLanguageDataMessage(IReadOnlyList<string> checkedPaths)
    {
        var checkedDirectories = checkedPaths.Count == 0
            ? "  (no tessdata directories were checked)"
            : string.Join(Environment.NewLine, checkedPaths.Select(path => $"  - {path}"));

        return $"Tesseract English OCR language data file '{ExpectedLanguageDataFileName}' was not found.{Environment.NewLine}" +
            $"Checked directories:{Environment.NewLine}{checkedDirectories}{Environment.NewLine}" +
            $"To enable OCR, create an '{AppLocalTessdataDirectoryName}' folder beside the built or published app executable and place '{ExpectedLanguageDataFileName}' in that folder, or set {TessdataEnvironmentVariableName} to the directory that contains '{ExpectedLanguageDataFileName}'.{Environment.NewLine}" +
            $"Download the official Tesseract English language data from: {TessdataDownloadUrl}";
    }
}
