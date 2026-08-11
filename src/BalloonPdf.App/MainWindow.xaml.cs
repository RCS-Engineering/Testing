using System.Diagnostics;
using System.IO;
using System.Windows;
using BalloonPdf.App.Models;
using BalloonPdf.App.Services;
using Microsoft.Win32;

namespace BalloonPdf.App;

public partial class MainWindow : Window
{
    private readonly DimensionDetector dimensionDetector = new();
    private readonly BalloonAnnotationService annotationService = new();
    private readonly PdfBalloonAnnotator balloonAnnotator = new();
    private readonly ExcelDimensionExporter excelExporter = new();
    private readonly OutputPathService outputPathService = new();

    private string? currentInputPath;
    private string? currentOutputPath;
    private IReadOnlyList<DimensionCandidate> currentDimensions = Array.Empty<DimensionCandidate>();
    private IReadOnlyList<BalloonAnnotation> currentAnnotations = Array.Empty<BalloonAnnotation>();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectInput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose source drawing",
            Filter = InputDocumentFormatExtensions.SupportedFileDialogFilter,
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
        currentInputPath = null;
        currentOutputPath = null;
        currentDimensions = Array.Empty<DimensionCandidate>();
        currentAnnotations = Array.Empty<BalloonAnnotation>();
        ExpandEditButton.IsEnabled = false;
        OpenPdfButton.IsEnabled = false;
        InlinePreview.LoadDocument(null);
        SetStatus("Source drawing selected. Choose Generate to create a ballooned PDF and Excel workbook.");
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

    private void SelectExcelTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose Excel template workbook",
            Filter = "Excel workbooks (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            ExcelTemplatePathTextBox.Text = dialog.FileName;
            SetStatus("Excel template selected. Choose Generate to create the files.");
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
        var excelTemplatePath = ExcelTemplatePathTextBox.Text.Trim();
        var excelOutputPath = ExcelOutputPathTextBox.Text.Trim();

        if (!ValidatePaths(inputPath, outputPath, excelTemplatePath, excelOutputPath))
        {
            return;
        }

        try
        {
            GenerateButton.IsEnabled = false;
            var inputFormat = InputDocumentFormatExtensions.FromPath(inputPath);
            SetStatus(inputFormat == InputDocumentFormat.Pdf ? "Detecting vector-text dimensions..." : "Detecting raster text dimensions with OCR...");

            var dimensions = await Task.Run(() => dimensionDetector.Detect(inputPath));
            if (dimensions.Count == 0)
            {
                SetStatus("No dimensions were detected in the source drawing.");
                return;
            }

            currentInputPath = inputPath;
            currentOutputPath = outputPath;
            currentDimensions = dimensions;
            currentAnnotations = annotationService.CreateFromDimensions(dimensions);

            SetStatus($"Detected {dimensions.Count} dimensions. Writing ballooned PDF...");
            await Task.Run(() => balloonAnnotator.AddBalloons(inputPath, outputPath, currentAnnotations));

            SetStatus("Writing Excel dimension workbook...");
            await Task.Run(() => excelExporter.Export(excelTemplatePath, excelOutputPath, dimensions));

            InlinePreview.LoadDocument(inputPath, currentAnnotations);
            ExpandEditButton.IsEnabled = true;
            OpenPdfButton.IsEnabled = true;
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

    private async void ExpandEdit_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(currentInputPath) || string.IsNullOrWhiteSpace(currentOutputPath) || currentAnnotations.Count == 0)
        {
            SetStatus("Generate a ballooned drawing before opening the editor.");
            return;
        }

        var editor = new BalloonEditorWindow(currentInputPath, currentAnnotations)
        {
            Owner = this
        };

        if (editor.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ExpandEditButton.IsEnabled = false;
            OpenPdfButton.IsEnabled = false;
            GenerateButton.IsEnabled = false;
            SetStatus("Saving edited balloons and regenerating PDF...");
            currentAnnotations = editor.SavedAnnotations;
            await Task.Run(() => balloonAnnotator.AddBalloons(currentInputPath, currentOutputPath, currentAnnotations));
            InlinePreview.LoadDocument(currentInputPath, currentAnnotations);
            SetStatus($"Saved edited ballooned PDF: {currentOutputPath}");
        }
        catch (Exception ex)
        {
            SetStatus($"Unable to save edited PDF: {ex.Message}");
        }
        finally
        {
            GenerateButton.IsEnabled = true;
            ExpandEditButton.IsEnabled = currentAnnotations.Count > 0;
            OpenPdfButton.IsEnabled = !string.IsNullOrWhiteSpace(currentOutputPath) && File.Exists(currentOutputPath);
        }
    }

    private void OpenPdf_Click(object sender, RoutedEventArgs e)
    {
        var outputPath = currentOutputPath ?? OutputPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
        {
            SetStatus("Generate a ballooned PDF before opening it.");
            return;
        }

        Process.Start(new ProcessStartInfo(outputPath)
        {
            UseShellExecute = true
        });
    }

    private bool ValidatePaths(string inputPath, string outputPath, string excelTemplatePath, string excelOutputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            SetStatus("Choose a source drawing first.");
            return false;
        }

        if (!File.Exists(inputPath))
        {
            SetStatus("The selected source drawing does not exist.");
            return false;
        }

        if (!InputDocumentFormatExtensions.IsSupported(inputPath))
        {
            SetStatus("Choose a PDF, JPG, or JPEG source drawing.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            SetStatus("Choose an output PDF path.");
            return false;
        }

        if (Path.GetFullPath(inputPath).Equals(Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("The output PDF must be separate from the source drawing.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(excelTemplatePath))
        {
            SetStatus("Choose an Excel template workbook.");
            return false;
        }

        if (!File.Exists(excelTemplatePath))
        {
            SetStatus("The selected Excel template workbook does not exist.");
            return false;
        }

        if (!Path.GetExtension(excelTemplatePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Choose an .xlsx Excel template workbook.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(excelOutputPath))
        {
            SetStatus("Choose an output Excel path.");
            return false;
        }

        if (Path.GetFullPath(inputPath).Equals(Path.GetFullPath(excelOutputPath), StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("The output Excel workbook must be separate from the source drawing.");
            return false;
        }

        if (Path.GetFullPath(excelTemplatePath).Equals(Path.GetFullPath(excelOutputPath), StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("The output Excel workbook must be separate from the template workbook.");
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
