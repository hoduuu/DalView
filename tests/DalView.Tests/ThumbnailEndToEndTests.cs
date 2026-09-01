using System.IO;
using DalView.Services;
using DalView.ViewModels;
using PdfiumViewer.Core;
using Xunit;

namespace DalView.Tests;

public class ThumbnailEndToEndTests
{
    [Fact]
    public void MainViewModel_OpenPath_PopulatesThumbnails_WithRealPdf()
    {
        // Use a real PDF from the system if available
        var pdfPath = @"C:\Users\hyun\Downloads\sd.webui\webui\repositories\generative-models\assets\sdxl_report.pdf";

        if (!File.Exists(pdfPath))
        {
            // Skip if test PDF not available
            return;
        }

        var loader = new PdfiumDocumentLoader();
        var vm = new MainViewModel(loader);

        // Act: Open the real PDF
        vm.OpenPath(pdfPath, password: null);

        // Assert: Verify document loaded and thumbnails were created
        Assert.NotNull(vm.Document);
        Assert.True(vm.Document.PageCount > 0);
        Assert.Equal(vm.Document.PageCount, vm.Thumbnails.Count);

        // Verify first few thumbnails are properly initialized
        for (int i = 0; i < Math.Min(3, vm.Thumbnails.Count); i++)
        {
            Assert.NotNull(vm.Thumbnails[i]);
            Assert.Equal(i, vm.Thumbnails[i].PageIndex);
            Assert.Equal(i + 1, vm.Thumbnails[i].DisplayNumber);
        }
    }

}
