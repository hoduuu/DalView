using System.Collections.ObjectModel;
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

    [ObservableProperty]
    private ObservableCollection<DocumentTabViewModel> tabs = new();

    [ObservableProperty]
    private DocumentTabViewModel? selectedTab;

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
        tab.OpenPath(path, password: null);
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    [RelayCommand]
    private void CloseTab(DocumentTabViewModel tab)
    {
        Tabs.Remove(tab);
        if (Tabs.Count == 0)
        {
            Application.Current?.Shutdown();
        }
    }
}
