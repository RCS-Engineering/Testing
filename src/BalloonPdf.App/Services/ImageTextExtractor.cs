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
    public IReadOnlyList<ImageTextWord> ExtractWords(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("The image file was not found.", imagePath);
        }

        var tessdataPath = ResolveTessdataPath();
        using var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
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
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX"),
            Path.Combine(AppContext.BaseDirectory, "tessdata")
        };

        foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
        {
            var fullPath = Path.GetFullPath(candidate!);
            if (File.Exists(Path.Combine(fullPath, "eng.traineddata")))
            {
                return fullPath;
            }
        }

        throw new InvalidOperationException("Tesseract English OCR data was not found. Add eng.traineddata to a tessdata folder beside the app, or set TESSDATA_PREFIX to the tessdata folder.");
    }
}
