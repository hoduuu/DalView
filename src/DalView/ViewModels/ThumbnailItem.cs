using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using DalView.Services;
using PdfiumViewer;
using PdfiumViewer.Core;

namespace DalView.ViewModels;

public partial class ThumbnailItem : ObservableObject
{
    private readonly IPdfDocument _document;
    private bool _loadStarted;

    public ThumbnailItem(IPdfDocument document, int pageIndex)
    {
        _document = document;
        PageIndex = pageIndex;
    }

    public int PageIndex { get; }

    public int DisplayNumber => PageIndex + 1;

    [ObservableProperty]
    private BitmapImage? thumbnail;

    public void EnsureLoaded()
    {
        if (_loadStarted) return;
        _loadStarted = true;

        var document = _document;
        var pageIndex = PageIndex;

        Task.Run(() => ThumbnailRenderer.RenderThumbnail(document, pageIndex))
            .ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    Application.Current.Dispatcher.Invoke(() => Thumbnail = t.Result);
                }
            }, TaskScheduler.Default);
    }
}
