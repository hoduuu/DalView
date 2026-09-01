using System.IO;
using DalView.Services;
using DalView.Tests.TestFixtures;
using DalView.ViewModels;
using Xunit;

namespace DalView.Tests;

public class MainViewModelTabTests
{
    private static string WriteTempPdf(string page1Text, string page2Text)
    {
        var bytes = MinimalPdfBuilder.Build(page1Text, page2Text);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void OpenPathAsNewTab_AddsIndependentTabs_WithDistinctState()
    {
        var pathA = WriteTempPdf("Alpha One", "Alpha Two");
        var pathB = WriteTempPdf("Beta One", "Beta Two");
        try
        {
            var vm = new MainViewModel(new PdfiumDocumentLoader());

            vm.OpenPathAsNewTab(pathA);
            var tabA = vm.SelectedTab;

            vm.OpenPathAsNewTab(pathB);
            var tabB = vm.SelectedTab;

            Assert.Equal(2, vm.Tabs.Count);
            Assert.NotSame(tabA, tabB);
            Assert.Same(tabB, vm.SelectedTab);

            Assert.NotNull(tabA);
            Assert.NotNull(tabB);
            Assert.Equal(Path.GetFileName(pathA), tabA!.Title);
            Assert.Equal(Path.GetFileName(pathB), tabB!.Title);
            Assert.Equal(2, tabA.Document!.PageCount);
            Assert.Equal(2, tabB.Document!.PageCount);

            // Changing tab A's page must not affect tab B.
            tabA.Page = 1;
            Assert.Equal(0, tabB.Page);

            tabA.Document?.Dispose();
            tabB.Document?.Dispose();
        }
        finally
        {
            foreach (var path in new[] { pathA, pathB })
            {
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                        // File may still be locked by PDFium; cleaned up later by the temp folder.
                    }
                }
            }
        }
    }

    [Fact]
    public void CloseTabCommand_RemovesOnlyThatTab()
    {
        var pathA = WriteTempPdf("Alpha One", "Alpha Two");
        var pathB = WriteTempPdf("Beta One", "Beta Two");
        try
        {
            var vm = new MainViewModel(new PdfiumDocumentLoader());
            vm.OpenPathAsNewTab(pathA);
            var tabA = vm.SelectedTab!;
            vm.OpenPathAsNewTab(pathB);
            var tabB = vm.SelectedTab!;

            vm.CloseTabCommand.Execute(tabA);

            Assert.Single(vm.Tabs);
            Assert.Same(tabB, vm.Tabs[0]);

            tabA.Document?.Dispose();
            tabB.Document?.Dispose();
        }
        finally
        {
            foreach (var path in new[] { pathA, pathB })
            {
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                        // File may still be locked by PDFium; cleaned up later by the temp folder.
                    }
                }
            }
        }
    }
}
