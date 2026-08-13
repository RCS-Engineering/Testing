using System.IO;
using System.Xml.Linq;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class BalloonEditorWindowXamlTests
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void EditorToolbar_AddsZoomButtonsWithClickHandlers()
    {
        var document = XDocument.Load(GetRepoFile("src", "BalloonPdf.App", "BalloonEditorWindow.xaml"));
        var buttons = document.Descendants(PresentationNamespace + "Button").ToList();
        var zoomOutButton = buttons.Single(button => (string?)button.Attribute(XamlNamespace + "Name") == "ZoomOutButton");
        var zoomInButton = buttons.Single(button => (string?)button.Attribute(XamlNamespace + "Name") == "ZoomInButton");

        Assert.Equal("ZoomOut_Click", (string?)zoomOutButton.Attribute("Click"));
        Assert.Equal("Zoom In", GetButtonText(zoomInButton));
        Assert.Equal("ZoomIn_Click", (string?)zoomInButton.Attribute("Click"));
        Assert.Equal("Zoom Out", GetButtonText(zoomOutButton));
        Assert.Contains(document.Descendants(PresentationNamespace + "TextBlock"), textBlock =>
            (string?)textBlock.Attribute(XamlNamespace + "Name") == "ZoomTextBlock"
            && (string?)textBlock.Attribute("Text") == "Zoom 100%");
    }

    [Fact]
    public void EditorToolbar_PreservesPreviewAndPageNavigationControls()
    {
        var document = XDocument.Load(GetRepoFile("src", "BalloonPdf.App", "BalloonEditorWindow.xaml"));
        var buttons = document.Descendants(PresentationNamespace + "Button").ToList();
        var previousPageIndex = buttons.FindIndex(button => (string?)button.Attribute("Click") == "PreviousPage_Click");
        var nextPageIndex = buttons.FindIndex(button => (string?)button.Attribute("Click") == "NextPage_Click");
        var zoomOutIndex = buttons.FindIndex(button => (string?)button.Attribute("Click") == "ZoomOut_Click");
        var zoomInIndex = buttons.FindIndex(button => (string?)button.Attribute("Click") == "ZoomIn_Click");

        Assert.NotEqual(-1, previousPageIndex);
        Assert.NotEqual(-1, nextPageIndex);
        Assert.True(previousPageIndex < nextPageIndex);
        Assert.True(nextPageIndex < zoomOutIndex);
        Assert.True(zoomOutIndex < zoomInIndex);
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "PdfPreviewControl"
            && (string?)element.Attribute(XamlNamespace + "Name") == "Preview");
        Assert.Contains(document.Descendants(PresentationNamespace + "TextBlock"), textBlock =>
            (string?)textBlock.Attribute(XamlNamespace + "Name") == "PageTextBlock"
            && (string?)textBlock.Attribute("Text") == "Page 0 of 0");
    }

    private static string? GetButtonText(XElement button)
    {
        return (string?)button.Descendants(PresentationNamespace + "TextBlock").FirstOrDefault()?.Attribute("Text");
    }

    private static string GetRepoFile(params string[] pathSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(pathSegments)} from the test output directory.");
    }
}
