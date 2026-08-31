using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using PdfiumViewer;
using PdfiumViewer.Core;
using PdfiumViewer.Enums;

namespace DalView.Services;

public static class ThumbnailRenderer
{
    private const int ThumbnailWidth = 120;

    public static BitmapImage RenderThumbnail(IPdfDocument document, int pageIndex)
    {
        var page = document.Pages[pageIndex];
        var aspect = page.Size.Height / page.Size.Width;
        var width = ThumbnailWidth;
        var height = Math.Max(1, (int)Math.Round(ThumbnailWidth * aspect));

        using var rendered = page.Render(width, height, 96, 96, PdfRotation.Rotate0, PdfRenderFlags.None);
        using var memory = new MemoryStream();
        rendered.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = memory;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
