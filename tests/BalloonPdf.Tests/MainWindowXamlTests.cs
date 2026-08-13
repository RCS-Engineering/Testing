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

        var disabledTrigger = buttonStyle.Descendants(PresentationNamespace + "Trigger")
            .Single(trigger =>
                (string?)trigger.Attribute("Property") == "IsEnabled"
                && (string?)trigger.Attribute("Value") == "False");
        Assert.Contains(disabledTrigger.Elements(PresentationNamespace + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Foreground"
            && (string?)setter.Attribute("Value") == "White");
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

    private static Predicate<XElement> IsNamed(string name)
    {
        return element => (string?)element.Attribute(XamlNamespace + "Name") == name;
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
