using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DalView.Services;
using PdfiumViewer;
using PdfiumViewer.Core;
using PdfiumViewer.Enums;

namespace DalView.ViewModels;

public partial class DocumentTabViewModel : ObservableObject
{
    private readonly IPdfDocumentLoader _loader;

    public event EventHandler<string>? PasswordRequired;

    public DocumentTabViewModel(IPdfDocumentLoader loader)
    {
        _loader = loader;
    }

    public string Title => string.IsNullOrEmpty(PdfPath) ? "새 탭" : Path.GetFileName(PdfPath);

    [ObservableProperty]
    private string? pdfPath;

    partial void OnPdfPathChanged(string? value) => OnPropertyChanged(nameof(Title));

    [ObservableProperty]
    private IPdfDocument? document;

    [ObservableProperty]
    private int page;

    /// <summary>1-based page number for display in the toolbar. <see cref="Page"/> itself stays 0-based.</summary>
    public int DisplayPage
    {
        get => Page + 1;
        set => Page = value - 1;
    }

    partial void OnPageChanged(int value) => OnPropertyChanged(nameof(DisplayPage));

    [ObservableProperty]
    private int pageCount;

    [ObservableProperty]
    private double zoom = 1.0;

    [ObservableProperty]
    private double zoomMin = 0.1;

    [ObservableProperty]
    private double zoomMax = 4.0;

    [ObservableProperty]
    private bool fitWidth = true;

    /// <summary>Whether this tab is the currently active one. Kept in sync by MainViewModel
    /// whenever SelectedTab changes, and used to show/hide this tab's persistent content
    /// (each tab owns its own PDFViewer instance so switching tabs never touches another
    /// tab's Document — see MainWindow.xaml).</summary>
    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private PdfMatches? matches;

    [ObservableProperty]
    private int matchIndex = -1;

    [ObservableProperty]
    private bool highlightAllMatches = true;

    [ObservableProperty]
    private ObservableCollection<ThumbnailItem> thumbnails = new();

    public void OpenPath(string path, string? password)
    {
        try
        {
            var newDocument = _loader.Load(path, password);
            // PDFViewer's DocumentChanged handler disposes the previous Document synchronously
            // when this property changes — do not dispose it here (would be a redundant no-op at best).
            Document = newDocument;
            PdfPath = path;
            Page = 0;
            Matches = null;
            MatchIndex = -1;
            StatusMessage = $"{Path.GetFileName(path)} ({newDocument.PageCount} pages)";
            Thumbnails = new ObservableCollection<ThumbnailItem>(
                Enumerable.Range(0, newDocument.PageCount).Select(i => new ThumbnailItem(newDocument, i)));
        }
        catch (PdfException ex) when (ex.Error == PdfError.PasswordProtected)
        {
            StatusMessage = "이 PDF는 암호로 보호되어 있습니다.";
            PasswordRequired?.Invoke(this, path);
        }
        catch (PdfException ex)
        {
            StatusMessage = $"PDF를 열 수 없습니다: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"PDF를 열 수 없습니다: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(ZoomMax, Math.Round(Zoom + 0.1, 2));

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(ZoomMin, Math.Round(Zoom - 0.1, 2));

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (Document == null || string.IsNullOrWhiteSpace(SearchText))
        {
            Matches = null;
            MatchIndex = -1;
            return;
        }

        var doc = Document;
        var query = SearchText;
        var result = await Task.Run(() => doc.Search(query, matchCase: false, wholeWord: false, 0, doc.PageCount - 1));

        if (Document != doc) return;

        Matches = result;
        MatchIndex = result.Items.Count > 0 ? 0 : -1;
        StatusMessage = result.Items.Count > 0
            ? $"{result.Items.Count}건 찾음"
            : "찾는 내용이 없습니다.";
    }

    [RelayCommand]
    private void NextMatch()
    {
        if (Matches == null) return;
        MatchIndex = SearchNavigator.Next(MatchIndex, Matches.Items.Count);
    }

    [RelayCommand]
    private void PreviousMatch()
    {
        if (Matches == null) return;
        MatchIndex = SearchNavigator.Previous(MatchIndex, Matches.Items.Count);
    }

    [RelayCommand]
    private void Print()
    {
        if (Document == null) return;

        using var printDocument = Document.CreatePrintDocument();
        using var dialog = new System.Windows.Forms.PrintDialog { Document = printDocument };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            printDocument.Print();
        }
    }
}
