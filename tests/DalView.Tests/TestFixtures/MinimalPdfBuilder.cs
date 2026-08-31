using System.IO;
using System.Text;

namespace DalView.Tests.TestFixtures;

public static class MinimalPdfBuilder
{
    public static byte[] Build(string page1Text, string page2Text)
    {
        var offsets = new List<long>();
        using var ms = new MemoryStream();

        void WriteObj(int num, string body)
        {
            offsets.Add(ms.Position);
            var text = $"{num} 0 obj\n{body}\nendobj\n";
            var bytes = Encoding.ASCII.GetBytes(text);
            ms.Write(bytes, 0, bytes.Length);
        }

        var header = Encoding.ASCII.GetBytes("%PDF-1.4\n");
        ms.Write(header, 0, header.Length);

        WriteObj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObj(2, "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>");
        WriteObj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 5 0 R /Resources << /Font << /F1 6 0 R >> >> >>");
        WriteObj(4, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 7 0 R /Resources << /Font << /F1 6 0 R >> >> >>");

        var stream1 = $"BT /F1 18 Tf 20 150 Td ({page1Text}) Tj ET";
        WriteObj(5, $"<< /Length {stream1.Length} >>\nstream\n{stream1}\nendstream");

        WriteObj(6, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        var stream2 = $"BT /F1 18 Tf 20 150 Td ({page2Text}) Tj ET";
        WriteObj(7, $"<< /Length {stream2.Length} >>\nstream\n{stream2}\nendstream");

        var xrefOffset = ms.Position;
        var sb = new StringBuilder();
        sb.Append("xref\n");
        sb.Append($"0 {offsets.Count + 1}\n");
        sb.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }
        sb.Append("trailer\n");
        sb.Append($"<< /Size {offsets.Count + 1} /Root 1 0 R >>\n");
        sb.Append("startxref\n");
        sb.Append(xrefOffset).Append('\n');
        sb.Append("%%EOF");

        var tail = Encoding.ASCII.GetBytes(sb.ToString());
        ms.Write(tail, 0, tail.Length);

        return ms.ToArray();
    }
}
