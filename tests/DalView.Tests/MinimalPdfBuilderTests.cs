using System.IO;
using DalView.Tests.TestFixtures;
using PdfiumViewer.Core;
using Xunit;

namespace DalView.Tests;

public class MinimalPdfBuilderTests
{
    [Fact]
    public void Build_ProducesTwoPageDocument_WithExpectedText()
    {
        var bytes = MinimalPdfBuilder.Build("Hello DalView", "Page Two");
        using var document = PdfDocument.Load(new MemoryStream(bytes));

        Assert.Equal(2, document.PageCount);
        Assert.Contains("Hello DalView", document.Pages[0].GetText());
        Assert.Contains("Page Two", document.Pages[1].GetText());
    }
}
