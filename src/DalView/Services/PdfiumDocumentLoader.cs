using PdfiumViewer;
using PdfiumViewer.Core;

namespace DalView.Services;

public class PdfiumDocumentLoader : IPdfDocumentLoader
{
    public IPdfDocument Load(string path, string? password = null)
    {
        return PdfDocument.Load(path, password);
    }
}
