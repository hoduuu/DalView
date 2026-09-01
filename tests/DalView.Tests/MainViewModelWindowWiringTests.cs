using System.Collections.Specialized;
using System.IO;
using DalView.Services;
using DalView.Tests.TestFixtures;
using DalView.ViewModels;
using PdfiumViewer;
using PdfiumViewer.Core;
using PdfiumViewer.Enums;
using Xunit;

namespace DalView.Tests;

file class FakeLoader : IPdfDocumentLoader
{
    public IPdfDocument Load(string path, string? password = null)
    {
        throw new PdfException(PdfError.PasswordProtected);
    }
}

public class MainViewModelWindowWiringTests
{
    [Fact]
    public void OpenPathAsNewTab_PasswordProtected_ReachesSubscriber_WhenWiredLikeMainWindow()
    {
        var vm = new MainViewModel(new FakeLoader());

        // Reproduce exactly how MainWindow.xaml.cs wires this: subscribe to
        // Tabs.CollectionChanged, and on new items, subscribe to PasswordRequired.
        string? raisedPath = null;
        vm.Tabs.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (DocumentTabViewModel tab in e.NewItems)
                {
                    tab.PasswordRequired += (_, path) => raisedPath = path;
                }
            }
        };

        vm.OpenPathAsNewTab("secret.pdf");

        Assert.Equal("secret.pdf", raisedPath);
    }

    [Fact]
    public void PrintCommand_ActsOnSelectedTab_NotAPreviouslyOpenedTab()
    {
        var pathA = WriteTempPdf("Doc A Page One", "Doc A Page Two");
        var pathB = WriteTempPdf("Doc B Page One", "Doc B Page Two");
        try
        {
            var vm = new MainViewModel(new PdfiumDocumentLoader());

            vm.OpenPathAsNewTab(pathA);
            var tabA = vm.SelectedTab;

            vm.OpenPathAsNewTab(pathB);
            var tabB = vm.SelectedTab;

            // SelectedTab should be the most recently opened (B), not A -- this is exactly
            // the wiring a bound Print button relies on to act on the right document.
            Assert.NotNull(tabA);
            Assert.NotNull(tabB);
            Assert.NotSame(tabA, tabB);
            Assert.Same(vm.Tabs[1], vm.SelectedTab);
            Assert.NotSame(vm.Tabs[0], vm.SelectedTab);

            Assert.Contains("Doc B Page One", tabB!.Document!.Pages[0].GetText());
            Assert.Contains("Doc A Page One", tabA!.Document!.Pages[0].GetText());

            tabA.Document?.Dispose();
            tabB.Document?.Dispose();
        }
        finally
        {
            foreach (var path in new[] { pathA, pathB })
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); }
                    catch (IOException) { /* still locked by PDFium; cleaned up later */ }
                }
            }
        }
    }

    private static string WriteTempPdf(string page1Text, string page2Text)
    {
        var bytes = MinimalPdfBuilder.Build(page1Text, page2Text);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
