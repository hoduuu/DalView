using DalView.Services;
using DalView.ViewModels;
using PdfiumViewer;
using PdfiumViewer.Core;
using PdfiumViewer.Enums;
using Xunit;

namespace DalView.Tests;

file class FakeLoader : IPdfDocumentLoader
{
    private readonly PdfError? _throwError;

    public FakeLoader(PdfError? throwError = null)
    {
        _throwError = throwError;
    }

    public IPdfDocument Load(string path, string? password = null)
    {
        if (_throwError.HasValue)
        {
            throw new PdfException(_throwError.Value);
        }

        throw new InvalidOperationException("FakeLoader was not configured to throw, and has no document to return.");
    }
}

public class MainViewModelTests
{
    [Fact]
    public void OpenPath_CorruptedFile_SetsStatusMessage_WithoutThrowing()
    {
        var vm = new MainViewModel(new FakeLoader(PdfError.InvalidFormat));

        vm.OpenPath("bad.pdf", null);

        Assert.Contains("PDF를 열 수 없습니다", vm.StatusMessage);
    }

    [Fact]
    public void OpenPath_PasswordProtected_RaisesPasswordRequired()
    {
        var vm = new MainViewModel(new FakeLoader(PdfError.PasswordProtected));
        string? raisedPath = null;
        vm.PasswordRequired += (_, path) => raisedPath = path;

        vm.OpenPath("secret.pdf", null);

        Assert.Equal("secret.pdf", raisedPath);
    }
}
