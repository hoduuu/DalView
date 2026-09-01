using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private bool _isPanningPdf;
    private Point _panMouseStart;
    private double _panScrollStartH;
    private double _panScrollStartV;

    /// <summary>
    /// PdfiumViewer.Net.WPF's default control template leaves its internal ScrollViewer
    /// (named PART_Scroll) at WPF's default HorizontalScrollBarVisibility=Disabled, so a page
    /// wider than the viewport (zoomed in, or a landscape page) is clipped with no way to pan
    /// to it. The template can't be safely redefined from application XAML (its ItemsPanel
    /// setter uses an internal-to-the-library panel type — PDFPanel, internal to the library's
    /// own assembly), so reach into the already-applied template for the already-named part
    /// instead and adjust what's needed from code.
    /// </summary>
    private void PDFViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Control control
            || control.Template?.FindName("PART_Scroll", control) is not ScrollViewer scrollViewer)
        {
            return;
        }

        scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        // PDFPanel (the library's internal ItemsPanel) reports its own desired WIDTH as just
        // the current page's width, not the viewport's — so when a page is narrower than the
        // window, ScrollViewer has no reason to center it; it just sits at the left edge.
        // Centering the panel itself as a block (via its host, ItemsPresenter) fixes this: since
        // the panel's own width already equals the page's width, centering that whole block
        // within the viewport is equivalent to centering the page.
        if (scrollViewer.Content is FrameworkElement itemsHost)
        {
            itemsHost.HorizontalAlignment = HorizontalAlignment.Center;
        }

        // Click-and-drag-to-pan ("grab" scrolling). The library's own PDFViewerItemContainer
        // already uses a plain left-button drag for text selection (Container_PreviewMouseDown/
        // Container_PreviewMouseMove, registered with PreviewMouseDown, i.e. tunneling). Handling
        // the same tunneling event higher up, on the ScrollViewer, and marking it Handled lets pan
        // take over cleanly before that deeper handler ever sees the drag.
        scrollViewer.PreviewMouseLeftButtonDown += PdfScroll_PreviewMouseLeftButtonDown;
        scrollViewer.PreviewMouseMove += PdfScroll_PreviewMouseMove;
        scrollViewer.PreviewMouseLeftButtonUp += PdfScroll_PreviewMouseLeftButtonUp;
        scrollViewer.Cursor = Cursors.Hand;
    }

    private void PdfScroll_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        _isPanningPdf = true;
        _panMouseStart = e.GetPosition(sv);
        _panScrollStartH = sv.HorizontalOffset;
        _panScrollStartV = sv.VerticalOffset;
        sv.CaptureMouse();
        e.Handled = true;
    }

    private void PdfScroll_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanningPdf || sender is not ScrollViewer sv || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pos = e.GetPosition(sv);
        sv.ScrollToHorizontalOffset(_panScrollStartH - (pos.X - _panMouseStart.X));
        sv.ScrollToVerticalOffset(_panScrollStartV - (pos.Y - _panMouseStart.Y));
        e.Handled = true;
    }

    private void PdfScroll_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        _isPanningPdf = false;
        sv.ReleaseMouseCapture();
        e.Handled = true;
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
