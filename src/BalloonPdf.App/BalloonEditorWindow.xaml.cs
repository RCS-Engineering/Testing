using System.Windows;
using System.Windows.Controls;
using BalloonPdf.App.Models;
using BalloonPdf.App.Services;

namespace BalloonPdf.App;

public sealed partial class BalloonEditorWindow : Window
{
    private readonly BalloonAnnotationService annotationService = new();
    private readonly string previewPdfPath;
    private List<BalloonAnnotation> annotations;
    private bool isAdding;
    private bool isRefreshingSelection;

    public BalloonEditorWindow(string previewPdfPath, IReadOnlyCollection<BalloonAnnotation> annotations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(previewPdfPath);
        ArgumentNullException.ThrowIfNull(annotations);

        this.previewPdfPath = previewPdfPath;
        this.annotations = annotations.Select(annotation => annotation with { }).ToList();
        SavedAnnotations = this.annotations;

        InitializeComponent();
        Preview.PageClicked += Preview_PageClicked;
        Preview.BalloonClicked += Preview_BalloonClicked;
        RefreshAll();
    }

    public IReadOnlyList<BalloonAnnotation> SavedAnnotations { get; private set; }

    private BalloonAnnotation? SelectedAnnotation => AnnotationListBox.SelectedItem as BalloonAnnotation;

    private void RefreshAll(Guid? selectedId = null)
    {
        annotations = annotationService.GetOrdered(annotations).ToList();
        AnnotationListBox.ItemsSource = annotations;
        Preview.LoadPdf(previewPdfPath, annotations);
        UpdatePageStatus();

        if (selectedId is not null)
        {
            AnnotationListBox.SelectedItem = annotations.FirstOrDefault(annotation => annotation.Id == selectedId.Value);
        }

        RefreshSelectionDetails();
    }

    private void RefreshAnnotations(Guid? selectedId = null)
    {
        annotations = annotationService.GetOrdered(annotations).ToList();
        AnnotationListBox.ItemsSource = annotations;
        Preview.LoadAnnotations(annotations);

        if (selectedId is not null)
        {
            AnnotationListBox.SelectedItem = annotations.FirstOrDefault(annotation => annotation.Id == selectedId.Value);
        }

        RefreshSelectionDetails();
    }

    private void AnnotationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshSelectionDetails();
    }

    private void Preview_BalloonClicked(object? sender, BalloonPreviewSelectionEventArgs e)
    {
        AnnotationListBox.SelectedItem = annotations.FirstOrDefault(annotation => annotation.Id == e.AnnotationId);
    }

    private void Preview_PageClicked(object? sender, BalloonPreviewClickEventArgs e)
    {
        if (!isAdding)
        {
            return;
        }

        annotations = annotationService.Add(annotations, e.PageNumber, e.PdfX, e.PdfY).ToList();
        isAdding = false;
        AddButton.Content = "Add balloon by clicking preview";
        var added = annotations.OrderByDescending(annotation => annotation.BalloonNumber).First();
        RefreshAnnotations(added.Id);
        EditorStatusTextBlock.Text = $"Added balloon #{added.BalloonNumber} on page {added.PageNumber}.";
    }

    private void ApplyNumber_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedAnnotation;
        if (selected is null)
        {
            EditorStatusTextBlock.Text = "Select a balloon before changing its number.";
            return;
        }

        if (!int.TryParse(NumberTextBox.Text, out var number) || number <= 0)
        {
            EditorStatusTextBlock.Text = "Enter a positive whole-number balloon number.";
            return;
        }

        annotations = annotationService.UpdateNumber(annotations, selected.Id, number).ToList();
        RefreshAnnotations(selected.Id);
        EditorStatusTextBlock.Text = $"Updated balloon number to {number}.";
    }

    private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isRefreshingSelection || SelectedAnnotation is not { } selected || ColorComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string color)
        {
            return;
        }

        annotations = annotationService.UpdateColor(annotations, selected.Id, color).ToList();
        RefreshAnnotations(selected.Id);
        EditorStatusTextBlock.Text = $"Updated balloon #{selected.BalloonNumber} color.";
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        isAdding = !isAdding;
        AddButton.Content = isAdding ? "Click the preview to place balloon" : "Add balloon by clicking preview";
        EditorStatusTextBlock.Text = isAdding ? "Click the page preview where the new balloon should appear." : string.Empty;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedAnnotation;
        if (selected is null)
        {
            EditorStatusTextBlock.Text = "Select a balloon before deleting.";
            return;
        }

        annotations = annotationService.Delete(annotations, selected.Id).ToList();
        RefreshAnnotations();
        EditorStatusTextBlock.Text = $"Deleted balloon #{selected.BalloonNumber}.";
    }

    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        Preview.PreviousPage();
        UpdatePageStatus();
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        Preview.NextPage();
        UpdatePageStatus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SavedAnnotations = annotationService.GetOrdered(annotations);
        DialogResult = true;
    }

    private void RefreshSelectionDetails()
    {
        isRefreshingSelection = true;
        try
        {
            if (SelectedAnnotation is not { } selected)
            {
                SelectedTextBlock.Text = "No balloon selected.";
                NumberTextBox.Text = string.Empty;
                ColorComboBox.SelectedIndex = -1;
                return;
            }

            SelectedTextBlock.Text = $"Balloon #{selected.BalloonNumber} on page {selected.PageNumber}";
            NumberTextBox.Text = selected.BalloonNumber.ToString();
            ColorComboBox.SelectedItem = ColorComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, selected.StrokeColorHex, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            isRefreshingSelection = false;
        }
    }

    private void UpdatePageStatus()
    {
        PageTextBlock.Text = Preview.PageCount == 0
            ? "Page 0 of 0"
            : $"Page {Preview.CurrentPageNumber} of {Preview.PageCount}";
    }
}
