using System.IO;
using System.Xml.Linq;
using Xunit;

namespace BalloonPdf.Tests;

public sealed class MainWindowXamlTests
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void GlobalButtonStyle_UsesBoldWhiteTextEvenWhenDisabled()
    {
        var document = XDocument.Load(GetRepoFile("src", "BalloonPdf.App", "App.xaml"));
        var buttonStyle = document.Descendants(PresentationNamespace + "Style")
            .Single(element => (string?)element.Attribute("TargetType") == "Button");

        Assert.Contains(buttonStyle.Elements(PresentationNamespace + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "FontWeight"
            && (string?)setter.Attribute("Value") == "Bold");
        Assert.Contains(buttonStyle.Elements(PresentationNamespace + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Foreground"
            && (string?)setter.Attribute("Value") == "White");
        Assert.Contains(buttonStyle.Elements(PresentationNamespace + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "MinHeight"
            && (string?)setter.Attribute("Value") == "42");

        var disabledTrigger = buttonStyle.Descendants(PresentationNamespace + "Trigger")
            .Single(trigger =>
                (string?)trigger.Attribute("Property") == "IsEnabled"
                && (string?)trigger.Attribute("Value") == "False");
        Assert.Contains(disabledTrigger.Elements(PresentationNamespace + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Foreground"
            && (string?)setter.Attribute("Value") == "White");
        Assert.Contains(disabledTrigger.Elements(PresentationNamespace + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Background"
            && (string?)setter.Attribute("Value") == "{StaticResource DisabledButtonBrush}");
    }

    [Fact]
    public void AppTheme_ExposesHeaderAccentAndLargerInputResources()
    {
        var document = XDocument.Load(GetRepoFile("src", "BalloonPdf.App", "App.xaml"));
        var resources = document.Descendants()
            .Where(element => element.Attribute(XamlNamespace + "Key") is not null)
            .ToList();

        Assert.Contains(resources, HasResourceKey("HeaderBrush"));
        Assert.Contains(resources, HasResourceKey("HeaderAccentBrush"));
        Assert.Contains(resources, HasResourceKey("AccentBrush"));
        Assert.Contains(resources, HasResourceKey("AppBackgroundBrush"));
        Assert.Contains(resources, HasResourceKey("PanelBackgroundBrush"));

        var textBoxStyle = document.Descendants(PresentationNamespace + "Style")
            .Single(element => (string?)element.Attribute("TargetType") == "TextBox");
        Assert.Contains(textBoxStyle.Elements(PresentationNamespace + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "MinHeight"
            && (string?)setter.Attribute("Value") == "44");
        Assert.Contains(textBoxStyle.Elements(PresentationNamespace + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "MinWidth"
            && (string?)setter.Attribute("Value") == "220");
    }

    [Fact]
    public void MainWindow_AddsBrandedHeaderWithoutRemovingPreviewOrStatus()
    {
        var document = XDocument.Load(GetRepoFile("src", "BalloonPdf.App", "MainWindow.xaml"));

        Assert.Contains(document.Descendants(PresentationNamespace + "TextBlock"), textBlock =>
            (string?)textBlock.Attribute("Text") == "Engineering Balloon Generator"
            && (string?)textBlock.Attribute("Foreground") == "White");
        Assert.Contains(document.Descendants(PresentationNamespace + "Border"), border =>
            (string?)border.Attribute("Background") == "{StaticResource HeaderBrush}");
        Assert.Contains(document.Descendants(), IsNamed("InlinePreview"));
        Assert.Contains(document.Descendants(), IsNamed("StatusTextBlock"));
    }

    [Fact]
    public void ActionRow_AddsOpenExcelButtonAfterOpenPdfButton()
    {
        var document = XDocument.Load(GetRepoFile("src", "BalloonPdf.App", "MainWindow.xaml"));
        var buttons = document.Descendants(PresentationNamespace + "Button").ToList();
        var openPdfIndex = buttons.FindIndex(IsNamed("OpenPdfButton"));
        var openExcelIndex = buttons.FindIndex(IsNamed("OpenExcelButton"));

        Assert.NotEqual(-1, openPdfIndex);
        Assert.Equal(openPdfIndex + 1, openExcelIndex);

        var openExcelButton = buttons[openExcelIndex];
        Assert.Equal("Open Excel", (string?)openExcelButton.Attribute("Content"));
        Assert.Equal("False", (string?)openExcelButton.Attribute("IsEnabled"));
        Assert.Equal("OpenExcel_Click", (string?)openExcelButton.Attribute("Click"));
    }

    [Fact]
    public void ActionButtons_PreserveContentOrderEnabledStatesAndClickHandlers()
    {
        var document = XDocument.Load(GetRepoFile("src", "BalloonPdf.App", "MainWindow.xaml"));
        var actionButtons = document.Descendants(PresentationNamespace + "Button")
            .Where(button => ActionButtonNames.Contains((string?)button.Attribute(XamlNamespace + "Name")))
            .ToList();

        Assert.Equal(ActionButtonNames, actionButtons.Select(button => (string?)button.Attribute(XamlNamespace + "Name")));
        Assert.Equal(new[] { "Generate", "Expand/Edit", "Open PDF", "Open Excel" }, actionButtons.Select(button => (string?)button.Attribute("Content")));
        Assert.Equal(new[] { "Generate_Click", "ExpandEdit_Click", "OpenPdf_Click", "OpenExcel_Click" }, actionButtons.Select(button => (string?)button.Attribute("Click")));
        Assert.Null(actionButtons[0].Attribute("IsEnabled"));
        Assert.Equal(new[] { "False", "False", "False" }, actionButtons.Skip(1).Select(button => (string?)button.Attribute("IsEnabled")));
    }

    private static readonly string[] ActionButtonNames =
    {
        "GenerateButton",
        "ExpandEditButton",
        "OpenPdfButton",
        "OpenExcelButton"
    };

    private static Predicate<XElement> IsNamed(string name)
    {
        return element => (string?)element.Attribute(XamlNamespace + "Name") == name;
    }

    private static Predicate<XElement> HasResourceKey(string key)
    {
        return element => (string?)element.Attribute(XamlNamespace + "Key") == key;
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
