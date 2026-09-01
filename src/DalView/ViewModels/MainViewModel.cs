using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DalView.Services;
using Microsoft.Win32;
using PdfiumViewer;
using PdfiumViewer.Core;
using PdfiumViewer.Enums;

namespace DalView.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPdfDocumentLoader _loader;

    public MainViewModel() : this(new PdfiumDocumentLoader())
    {
    }

    public MainViewModel(IPdfDocumentLoader loader)
    {
        _loader = loader;
    }

    [ObservableProperty]
    private string? pdfPath;

    [ObservableProperty]
    private IPdfDocument? document;

    [ObservableProperty]
    private int page;

    [ObservableProperty]
    private int pageCount;

    [ObservableProperty]
    private double zoom = 1.0;

    [ObservableProperty]
    private double zoomMin = 0.1;

    [ObservableProperty]
    private double zoomMax = 4.0;

    [ObservableProperty]
    private bool fitWidth;

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

    [RelayCommand]
    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        OpenPath(dialog.FileName, password: null);
    }

    public void OpenPath(string path, string? password)
    {
        try
        {
            var newDocument = _loader.Load(path, password);
            var oldDocument = Document;
            if (oldDocument != null)
            {
                Task.Delay(TimeSpan.FromSeconds(2))
                    .ContinueWith(_ => oldDocument.Dispose(), TaskScheduler.Default);
            }
            Document = newDocument;
            PdfPath = path;
            Page = 0;
            StatusMessage = $"{Path.GetFileName(path)} ({newDocument.PageCount} pages)";
            Thumbnails = new ObservableCollection<ThumbnailItem>(
                Enumerable.Range(0, newDocument.PageCount).Select(i => new ThumbnailItem(newDocument, i)));
        }
        catch (PdfException ex) when (ex.Error == PdfError.PasswordProtected)
        {
            StatusMessage = "이 PDF는 암호로 보호되어 있습니다.";
            throw;
        }
        catch (PdfException ex)
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
}
