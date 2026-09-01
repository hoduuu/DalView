using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DalView.ViewModels;
using PdfiumViewer.Core;

namespace DalView;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel vm)
        {
            vm.Tabs.CollectionChanged += OnTabsCollectionChanged;
        }
    }

    public void OpenFileFromExternalRequest(string path)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenPathAsNewTab(path);
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    private void OnTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (DocumentTabViewModel tab in e.NewItems)
            {
                tab.PasswordRequired += OnPasswordRequired;
            }
        }
    }

    private void BookmarkItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: PdfBookmark bookmark } item
            && FindTabViewModel(item) is DocumentTabViewModel vm)
        {
            vm.Page = bookmark.PageIndex;
        }
        e.Handled = true;
    }

    private void ThumbnailImage_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThumbnailItem item })
        {
            item.EnsureLoaded();
        }
    }

    private void ThumbnailRow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThumbnailItem item } row
            && FindTabViewModel(row) is DocumentTabViewModel vm)
        {
            vm.Page = item.PageIndex;
        }
    }

    private void OnPasswordRequired(object? sender, string path)
    {
        var dialog = new PasswordDialog { Owner = this };
        if (dialog.ShowDialog() == true && sender is DocumentTabViewModel vm)
        {
            vm.OpenPath(path, dialog.Password);
        }
    }

    /// <summary>
    /// Walks up the visual tree from <paramref name="element"/> to find the nearest ancestor
    /// whose DataContext is a DocumentTabViewModel. Needed because, with tabs, a Window's own
    /// DataContext is the tab-container MainViewModel, not the active tab — event handlers that
    /// need the active tab's state (e.g. to set Page) must resolve it via the visual tree instead.
    /// </summary>
    private static DocumentTabViewModel? FindTabViewModel(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is FrameworkElement { DataContext: DocumentTabViewModel vm })
            {
                return vm;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
