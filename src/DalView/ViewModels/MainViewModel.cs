using System.IO;
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
            Document?.Dispose();
            Document = newDocument;
            PdfPath = path;
            Page = 0;
            StatusMessage = $"{Path.GetFileName(path)} ({newDocument.PageCount} pages)";
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
}
