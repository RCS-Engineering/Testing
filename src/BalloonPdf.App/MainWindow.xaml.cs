using System.IO;
using System.Windows;
using BalloonPdf.App.Services;
using Microsoft.Win32;

namespace BalloonPdf.App;

public partial class MainWindow : Window
{
    private readonly DimensionDetector dimensionDetector = new();
    private readonly PdfBalloonAnnotator balloonAnnotator = new();
    private readonly ExcelDimensionExporter excelExporter = new();
    private readonly OutputPathService outputPathService = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectInput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose input PDF",
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        InputPathTextBox.Text = dialog.FileName;
        OutputPathTextBox.Text = outputPathService.GetDefaultOutputPath(dialog.FileName);
        ExcelOutputPathTextBox.Text = outputPathService.GetDefaultExcelOutputPath(dialog.FileName);
        SetStatus("Input selected. Choose Generate to create a ballooned PDF and Excel workbook.");
    }

    private void SelectOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Choose output PDF",
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            AddExtension = true,
            DefaultExt = ".pdf",
            OverwritePrompt = true,
            FileName = string.IsNullOrWhiteSpace(OutputPathTextBox.Text)
                ? "drawing_balloons.pdf"
                : Path.GetFileName(OutputPathTextBox.Text),
            InitialDirectory = GetInitialDirectory(OutputPathTextBox.Text)
        };

        if (dialog.ShowDialog(this) == true)
        {
            OutputPathTextBox.Text = dialog.FileName;
            SetStatus("PDF output selected. Choose Generate to create the files.");
        }
    }

    private void SelectExcelOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Choose output Excel workbook",
            Filter = "Excel workbooks (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            AddExtension = true,
            DefaultExt = ".xlsx",
            OverwritePrompt = true,
            FileName = string.IsNullOrWhiteSpace(ExcelOutputPathTextBox.Text)
                ? "drawing_balloons.xlsx"
                : Path.GetFileName(ExcelOutputPathTextBox.Text),
            InitialDirectory = GetInitialDirectory(ExcelOutputPathTextBox.Text)
        };

        if (dialog.ShowDialog(this) == true)
        {
            ExcelOutputPathTextBox.Text = dialog.FileName;
            SetStatus("Excel output selected. Choose Generate to create the files.");
        }
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        var inputPath = InputPathTextBox.Text.Trim();
        var outputPath = OutputPathTextBox.Text.Trim();
        var excelOutputPath = ExcelOutputPathTextBox.Text.Trim();

        if (!ValidatePaths(inputPath, outputPath, excelOutputPath))
        {
            return;
        }

        try
        {
            GenerateButton.IsEnabled = false;
            SetStatus("Detecting vector-text dimensions...");

            var dimensions = await Task.Run(() => dimensionDetector.Detect(inputPath));
            if (dimensions.Count == 0)
            {
                SetStatus("No vector-text dimensions were detected. Scanned-image/OCR detection is not supported in this version.");
                return;
            }

            SetStatus($"Detected {dimensions.Count} dimensions. Writing ballooned PDF...");
            await Task.Run(() => balloonAnnotator.AddBalloons(inputPath, outputPath, dimensions));

            SetStatus("Writing Excel dimension workbook...");
            await Task.Run(() => excelExporter.Export(excelOutputPath, dimensions));

            SetStatus($"Created ballooned PDF: {outputPath}\nCreated Excel workbook: {excelOutputPath}");
        }
        catch (Exception ex)
        {
            SetStatus($"Unable to generate files: {ex.Message}");
        }
        finally
        {
            GenerateButton.IsEnabled = true;
        }
    }

    private bool ValidatePaths(string inputPath, string outputPath, string excelOutputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            SetStatus("Choose an input PDF first.");
            return false;
        }

        if (!File.Exists(inputPath))
        {
            SetStatus("The selected input PDF does not exist.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            SetStatus("Choose an output PDF path.");
            return false;
        }

        if (Path.GetFullPath(inputPath).Equals(Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("The output PDF must be separate from the input PDF.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(excelOutputPath))
        {
            SetStatus("Choose an output Excel path.");
            return false;
        }

        if (Path.GetFullPath(inputPath).Equals(Path.GetFullPath(excelOutputPath), StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("The output Excel workbook must be separate from the input PDF.");
            return false;
        }

        return true;
    }

    private string? GetInitialDirectory(string preferredPath)
    {
        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            return Path.GetDirectoryName(preferredPath);
        }

        if (!string.IsNullOrWhiteSpace(InputPathTextBox.Text))
        {
            return Path.GetDirectoryName(InputPathTextBox.Text);
        }

        return null;
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }
}
