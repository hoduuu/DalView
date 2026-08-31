using System.IO;
using DalView.Services;
using DalView.Tests.TestFixtures;
using PdfiumViewer.Core;
using Xunit;

namespace DalView.Tests;

public class ThumbnailRendererTests
{
    [Fact]
    public void RenderThumbnail_ProducesImage_WithExpectedWidth()
    {
        var bytes = MinimalPdfBuilder.Build("Hello DalView", "Page Two");
        using var document = PdfDocument.Load(new MemoryStream(bytes));

        var image = ThumbnailRenderer.RenderThumbnail(document, 0);

        Assert.Equal(120, image.PixelWidth);
        Assert.Equal(120, image.PixelHeight); // fixture page is 200x200 (square), so height == width at fixed thumbnail width
    }

    [Fact]
    public void RenderThumbnail_Page1_ProducesImage_WithExpectedWidth()
    {
        var bytes = MinimalPdfBuilder.Build("Page One", "Page Two");
        using var document = PdfDocument.Load(new MemoryStream(bytes));

        var image = ThumbnailRenderer.RenderThumbnail(document, 1);

        Assert.Equal(120, image.PixelWidth);
        Assert.Equal(120, image.PixelHeight); // fixture page is 200x200 (square), so height == width at fixed thumbnail width
    }
}
