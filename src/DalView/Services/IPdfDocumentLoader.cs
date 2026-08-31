using PdfiumViewer;

namespace DalView.Services;

public interface IPdfDocumentLoader
{
    IPdfDocument Load(string path, string? password = null);
}
