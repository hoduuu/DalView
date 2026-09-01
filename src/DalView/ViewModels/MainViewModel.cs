using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DalView.Services;
using Microsoft.Win32;

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

    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = new();

    [ObservableProperty]
    private DocumentTabViewModel? selectedTab;

    partial void OnSelectedTabChanged(DocumentTabViewModel? oldValue, DocumentTabViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

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

        OpenPathAsNewTab(dialog.FileName);
    }

    public void OpenPathAsNewTab(string path)
    {
        var tab = new DocumentTabViewModel(_loader);
        Tabs.Add(tab);
        SelectedTab = tab;
        tab.OpenPath(path, password: null);
    }

    [RelayCommand]
    private void CloseTab(DocumentTabViewModel tab)
    {
        Tabs.Remove(tab);

        // Each tab now owns a persistent, never-shared PDFViewer (see MainWindow.xaml), so
        // closing a tab is the only thing that should ever dispose its document. Give any
        // in-flight background thumbnail render (ThumbnailItem.EnsureLoaded) a wide grace
        // window to finish before freeing the native handle out from under it.
        var doc = tab.Document;
        if (doc != null)
        {
            Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ => doc.Dispose(), TaskScheduler.Default);
        }

        if (Tabs.Count == 0)
        {
            Application.Current?.Shutdown();
        }
    }
}
