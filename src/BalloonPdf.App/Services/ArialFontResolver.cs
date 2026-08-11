using System.Collections.Concurrent;
using System.IO;
using PdfSharp.Fonts;

namespace BalloonPdf.App.Services;

public sealed class ArialFontResolver : IFontResolver
{
    internal const string RegularFaceName = "Arial#Regular";
    internal const string BoldFaceName = "Arial#Bold";
    internal const string ItalicFaceName = "Arial#Italic";
    internal const string BoldItalicFaceName = "Arial#BoldItalic";

    private const string ArialFamilyName = "Arial";
    private const string RegularFileName = "arial.ttf";
    private const string BoldFileName = "arialbd.ttf";
    private const string ItalicFileName = "ariali.ttf";
    private const string BoldItalicFileName = "arialbi.ttf";

    private static readonly object RegistrationLock = new();

    private readonly ConcurrentDictionary<string, byte[]> fontBytesByFaceName = new(StringComparer.Ordinal);
    private readonly Func<string, byte[]?> loadFontBytes;

    public ArialFontResolver()
        : this(LoadFontBytesFromKnownLocations)
    {
    }

    internal ArialFontResolver(Func<string, byte[]?> loadFontBytes)
    {
        this.loadFontBytes = loadFontBytes ?? throw new ArgumentNullException(nameof(loadFontBytes));
    }

    public static void Register()
    {
        lock (RegistrationLock)
        {
            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new ArialFontResolver();
            }
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (!ArialFamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new FontResolverInfo(GetFaceName(isBold, isItalic));
    }

    public byte[] GetFont(string faceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(faceName);

        if (fontBytesByFaceName.TryGetValue(faceName, out var cachedFontBytes))
        {
            return cachedFontBytes;
        }

        var fileName = GetFileName(faceName);
        var fontBytes = loadFontBytes(fileName);
        if (fontBytes is null)
        {
            throw new FileNotFoundException(
                $"Arial font face '{faceName}' requires '{fileName}', but it was not found in any known font location: {GetSearchedLocationsMessage(fileName)}.");
        }

        fontBytesByFaceName.TryAdd(faceName, fontBytes);
        return fontBytesByFaceName[faceName];
    }

    private static string GetFaceName(bool isBold, bool isItalic)
    {
        return (isBold, isItalic) switch
        {
            (false, false) => RegularFaceName,
            (true, false) => BoldFaceName,
            (false, true) => ItalicFaceName,
            (true, true) => BoldItalicFaceName
        };
    }

    private static string GetFileName(string faceName)
    {
        return faceName switch
        {
            RegularFaceName => RegularFileName,
            BoldFaceName => BoldFileName,
            ItalicFaceName => ItalicFileName,
            BoldItalicFaceName => BoldItalicFileName,
            _ => throw new ArgumentException($"Unsupported Arial font face '{faceName}'.", nameof(faceName))
        };
    }

    private static byte[]? LoadFontBytesFromKnownLocations(string fileName)
    {
        foreach (var directory in GetKnownFontDirectories())
        {
            foreach (var candidateFileName in GetCandidateFileNames(fileName))
            {
                var path = Path.Combine(directory, candidateFileName);
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetKnownFontDirectories()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Fonts");

        var windowsDirectory = Environment.GetEnvironmentVariable("WINDIR");
        if (!string.IsNullOrWhiteSpace(windowsDirectory))
        {
            yield return Path.Combine(windowsDirectory, "Fonts");
        }

        var fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrWhiteSpace(fontsDirectory))
        {
            yield return fontsDirectory;
        }

        yield return "/usr/share/fonts/truetype/msttcorefonts";
        yield return "/usr/share/fonts/truetype/liberation2";
        yield return "/usr/share/fonts/truetype/liberation";
        yield return "/usr/share/fonts/truetype/dejavu";
    }

    private static IEnumerable<string> GetCandidateFileNames(string fileName)
    {
        yield return fileName;

        foreach (var fallbackFileName in fileName.ToLowerInvariant() switch
        {
            RegularFileName => new[] { "LiberationSans-Regular.ttf", "DejaVuSans.ttf" },
            BoldFileName => new[] { "LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf" },
            ItalicFileName => new[] { "LiberationSans-Italic.ttf", "DejaVuSans-Oblique.ttf" },
            BoldItalicFileName => new[] { "LiberationSans-BoldItalic.ttf", "DejaVuSans-BoldOblique.ttf" },
            _ => Array.Empty<string>()
        })
        {
            yield return fallbackFileName;
        }
    }

    private static string GetSearchedLocationsMessage(string fileName)
    {
        return string.Join(
            "; ",
            GetKnownFontDirectories()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(directory => Path.Combine(directory, fileName)));
    }
}
