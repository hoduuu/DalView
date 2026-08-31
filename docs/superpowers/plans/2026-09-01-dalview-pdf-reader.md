# 달뷰 (DalView) PDF Reader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build 달뷰 (DalView), a lightweight, ad-free, view-only Windows desktop PDF reader in C#/WPF.

**Architecture:** MVVM WPF app. All PDF parsing/rendering is delegated to the `PdfiumViewer.Net.WPF` NuGet package (wraps Google's PDFium engine, the same engine Chrome uses). Its `PDFViewer` control already implements scroll-range-based virtualized page rendering, zoom, rotation, page-mode, and search-match highlighting internally, so the app does not need to hand-roll a virtualizing panel — it only needs to wire a ViewModel and add the two things the library doesn't provide: a lazy-loaded thumbnail sidebar and a bookmarks (outline) sidebar. This is a simplification versus the original design doc's "custom `VirtualizingStackPanel`" idea — same virtualization outcome, less code, because the library's built-in `RenderRange` scroll tracking already does it.

**Tech Stack:** C# / .NET 8.0-windows, WPF, `PdfiumViewer.Net.WPF` 3.0.4, `CommunityToolkit.Mvvm` 8.4.2 (source-generated `ObservableObject`/`RelayCommand` — cuts MVVM boilerplate, no heavier than hand-written equivalents), xUnit for tests.

**Spec:** [docs/superpowers/specs/2026-09-01-dalview-pdf-reader-design.md](../specs/2026-09-01-dalview-pdf-reader-design.md)

## Global Constraints

- Target framework: `net8.0-windows` everywhere (app project and test project).
- PDF engine: `PdfiumViewer.Net.WPF` version `3.0.4` exactly (pin this version — verified API surface below is from this version's source).
- No network calls anywhere in the app (no update checks, no telemetry, no cloud sync) — per spec's explicit exclusions.
- No editing/annotation/signing/printing features — view-only, per spec scope.
- Namespace root for the app: `DalView`.
- Library namespaces used: `PdfiumViewer` (the `PDFViewer` control), `PdfiumViewer.Core` (`PdfDocument`, `IPdfDocument`, `PdfBookmark`, `PdfBookmarkCollection`, `PdfMatch`, `PdfMatches`, `PdfPage`, `PdfException`), `PdfiumViewer.Enums` (`PdfError`, `PdfRenderFlags`, `PdfRotation`, `PdfPageMode`).

---

### Task 1: Project scaffold + minimal working viewer (open, page nav, zoom, fit-width)

**Files:**
- Create: `DalView.sln`
- Create: `src/DalView/DalView.csproj`
- Create: `src/DalView/App.xaml`
- Create: `src/DalView/App.xaml.cs`
- Create: `src/DalView/MainWindow.xaml`
- Create: `src/DalView/MainWindow.xaml.cs`
- Create: `src/DalView/Services/IPdfDocumentLoader.cs`
- Create: `src/DalView/Services/PdfiumDocumentLoader.cs`
- Create: `src/DalView/ViewModels/MainViewModel.cs`
- Create: `tests/DalView.Tests/DalView.Tests.csproj`
- Create: `.gitignore`

**Interfaces:**
- Produces: `IPdfDocumentLoader.Load(string path, string? password = null) : IPdfDocument` — every later task that opens a PDF goes through this, so error handling (Task 5) can substitute a fake loader in tests.
- Produces: `MainViewModel` with public settable/bindable members: `PdfPath (string?)`, `Document (IPdfDocument?)`, `Page (int)`, `PageCount (int)`, `Zoom (double)`, `FitWidth (bool)`, `StatusMessage (string?)`, and command `OpenFileCommand`.

- [ ] **Step 1: Create the solution and app project**

```bash
mkdir -p "D:/claude/DalView/src/DalView" "D:/claude/DalView/tests/DalView.Tests"
cd "D:/claude/DalView"
dotnet new sln -n DalView
```

Create `src/DalView/DalView.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>DalView</AssemblyName>
    <RootNamespace>DalView</RootNamespace>
    <ApplicationTitle>달뷰</ApplicationTitle>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="PdfiumViewer.Net.WPF" Version="3.0.4" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
  </ItemGroup>

</Project>
```

```bash
dotnet sln add "src/DalView/DalView.csproj"
```

- [ ] **Step 2: Verify the bare project builds**

Run: `cd "D:/claude/DalView" && dotnet build src/DalView/DalView.csproj`
Expected: Build succeeds (0 errors) even though `App.xaml`/`MainWindow.xaml` don't exist yet — actually this will FAIL until Step 3 adds them, since `OutputType=WinExe` needs an entry point. Skip building until after Step 3; this step exists only to confirm `dotnet restore` pulls `PdfiumViewer.Net.WPF` and its native `bblanchon.PDFium.Win32` dependency without error:

Run: `cd "D:/claude/DalView" && dotnet restore src/DalView/DalView.csproj`
Expected: `Restored ... DalView.csproj` with no errors, and `bblanchon.PDFium.Win32` listed among resolved packages.

- [ ] **Step 3: Create App.xaml / App.xaml.cs**

`src/DalView/App.xaml`:

```xml
<Application x:Class="DalView.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
</Application>
```

`src/DalView/App.xaml.cs`:

```csharp
using System.Windows;

namespace DalView;

public partial class App : Application
{
}
```

- [ ] **Step 4: Create the document-loader service**

`src/DalView/Services/IPdfDocumentLoader.cs`:

```csharp
using PdfiumViewer.Core;

namespace DalView.Services;

public interface IPdfDocumentLoader
{
    IPdfDocument Load(string path, string? password = null);
}
```

`src/DalView/Services/PdfiumDocumentLoader.cs`:

```csharp
using PdfiumViewer.Core;

namespace DalView.Services;

public class PdfiumDocumentLoader : IPdfDocumentLoader
{
    public IPdfDocument Load(string path, string? password = null)
    {
        return PdfDocument.Load(path, password);
    }
}
```

- [ ] **Step 5: Create MainViewModel with open/page/zoom state**

`src/DalView/ViewModels/MainViewModel.cs`:

```csharp
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DalView.Services;
using Microsoft.Win32;
using PdfiumViewer.Core;
using PdfiumViewer.Enums;

namespace DalView.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPdfDocumentLoader _loader;

    public MainViewModel() : this(new PdfiumDocumentLoader())
    {
    }

    public MainViewModel(IPdfDocumentLoader loader)
    {
        _loader = loader;
    }

    [ObservableProperty]
    private string? pdfPath;

    [ObservableProperty]
    private IPdfDocument? document;

    [ObservableProperty]
    private int page;

    [ObservableProperty]
    private int pageCount;

    [ObservableProperty]
    private double zoom = 1.0;

    [ObservableProperty]
    private double zoomMin = 0.1;

    [ObservableProperty]
    private double zoomMax = 4.0;

    [ObservableProperty]
    private bool fitWidth;

    [ObservableProperty]
    private string? statusMessage;

    [RelayCommand]
    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        OpenPath(dialog.FileName, password: null);
    }

    public void OpenPath(string path, string? password)
    {
        try
        {
            var newDocument = _loader.Load(path, password);
            Document?.Dispose();
            Document = newDocument;
            PdfPath = path;
            Page = 0;
            StatusMessage = $"{Path.GetFileName(path)} ({newDocument.PageCount} pages)";
        }
        catch (PdfException ex) when (ex.Error == PdfError.PasswordProtected)
        {
            StatusMessage = "이 PDF는 암호로 보호되어 있습니다.";
            throw;
        }
        catch (PdfException ex)
        {
            StatusMessage = $"PDF를 열 수 없습니다: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(ZoomMax, Math.Round(Zoom + 0.1, 2));

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(ZoomMin, Math.Round(Zoom - 0.1, 2));
}
```

- [ ] **Step 6: Create MainWindow wiring the PDFViewer control**

`src/DalView/MainWindow.xaml`:

```xml
<Window x:Class="DalView.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:pdf="clr-namespace:PdfiumViewer;assembly=PdfiumViewer"
        xmlns:vm="clr-namespace:DalView.ViewModels"
        Title="달뷰" Height="800" Width="1100" Background="#FAFAF8">
    <Window.DataContext>
        <vm:MainViewModel />
    </Window.DataContext>
    <DockPanel>
        <ToolBar DockPanel.Dock="Top">
            <Button Content="열기" Command="{Binding OpenFileCommand}" Padding="8,2" />
            <Separator />
            <TextBox Width="50" Text="{Binding Page, Mode=TwoWay}" TextAlignment="Center" />
            <TextBlock VerticalAlignment="Center" Margin="4,0">
                <Run Text="/ " /><Run Text="{Binding PageCount, Mode=OneWay}" />
            </TextBlock>
            <Separator />
            <Button Content="－" Command="{Binding ZoomOutCommand}" Width="28" />
            <TextBlock VerticalAlignment="Center" Margin="4,0" Text="{Binding Zoom, StringFormat={}{0:P0}}" />
            <Button Content="＋" Command="{Binding ZoomInCommand}" Width="28" />
            <CheckBox Content="폭 맞춤" IsChecked="{Binding FitWidth}" VerticalAlignment="Center" Margin="8,0" />
        </ToolBar>
        <StatusBar DockPanel.Dock="Bottom">
            <StatusBarItem Content="{Binding StatusMessage}" />
        </StatusBar>
        <pdf:PDFViewer x:Name="Viewer"
                       Document="{Binding Document, Mode=TwoWay}"
                       Page="{Binding Page, Mode=TwoWay}"
                       PageCount="{Binding PageCount, Mode=OneWayToSource}"
                       Zoom="{Binding Zoom, Mode=TwoWay}"
                       ZoomMin="{Binding ZoomMin}"
                       ZoomMax="{Binding ZoomMax}"
                       FitWidth="{Binding FitWidth}"
                       Padding="12" />
    </DockPanel>
</Window>
```

`src/DalView/MainWindow.xaml.cs`:

```csharp
using System.Windows;

namespace DalView;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 7: Build and manually verify**

Run: `cd "D:/claude/DalView" && dotnet build src/DalView/DalView.csproj`
Expected: Build succeeds, 0 errors.

Run: `dotnet run --project src/DalView/DalView.csproj`
Expected: A window titled "달뷰" opens. Click "열기", pick any real PDF file — it renders, page-number box and zoom +/- work, "폭 맞춤" fits the page to the window width. Close the app.

- [ ] **Step 8: Create the test project and fixture builder**

```bash
cd "D:/claude/DalView/tests/DalView.Tests"
dotnet new xunit
```

Replace `tests/DalView.Tests/DalView.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/DalView/DalView.csproj" />
  </ItemGroup>

</Project>
```

```bash
cd "D:/claude/DalView"
dotnet sln add "tests/DalView.Tests/DalView.Tests.csproj"
```

Create `tests/DalView.Tests/TestFixtures/MinimalPdfBuilder.cs` — builds a tiny valid 2-page PDF in memory (no external sample file needed, no hand-computed byte offsets: offsets are tracked programmatically as each object is written):

```csharp
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
```

- [ ] **Step 9: Write and run a smoke test proving the fixture loads via the real PDFium engine**

Create `tests/DalView.Tests/MinimalPdfBuilderTests.cs`:

```csharp
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
```

Run: `cd "D:/claude/DalView" && dotnet test tests/DalView.Tests/DalView.Tests.csproj --filter MinimalPdfBuilderTests`
Expected: PASS. This confirms the fixture builder produces real, PDFium-loadable PDFs, so later tasks can trust it for search/thumbnail tests without needing a checked-in binary sample file.

- [ ] **Step 10: Commit**

Create `.gitignore` at repo root:

```
bin/
obj/
.vs/
```

```bash
cd "D:/claude/DalView"
git add .gitignore DalView.sln src/DalView tests/DalView.Tests docs/superpowers/plans/2026-09-01-dalview-pdf-reader.md
git commit -m "Scaffold DalView: open/page/zoom viewer wired to PdfiumViewer.Net.WPF"
```

---

### Task 2: Text search

**Files:**
- Create: `src/DalView/ViewModels/SearchNavigator.cs`
- Create: `tests/DalView.Tests/SearchNavigatorTests.cs`
- Modify: `src/DalView/ViewModels/MainViewModel.cs`
- Modify: `src/DalView/MainWindow.xaml`

**Interfaces:**
- Consumes: `IPdfDocument.Search(string, bool, bool, int, int) : PdfMatches` (from Task 1's `Document` property); `PdfMatches.Items : IList<PdfMatch>`; `PdfMatch.Page : int`.
- Produces: `SearchNavigator.Next(int currentIndex, int count) : int` and `SearchNavigator.Previous(int currentIndex, int count) : int`, both wrap around and return `-1` when `count <= 0`. `MainViewModel.Matches (PdfMatches?)`, `MatchIndex (int)`, `HighlightAllMatches (bool)`, `SearchText (string)`, commands `SearchCommand`, `NextMatchCommand`, `PreviousMatchCommand`.

- [ ] **Step 1: Write the failing test for match-index wraparound**

Create `tests/DalView.Tests/SearchNavigatorTests.cs`:

```csharp
using DalView.ViewModels;
using Xunit;

namespace DalView.Tests;

public class SearchNavigatorTests
{
    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(2, 3, 0)]
    public void Next_WrapsAround(int current, int count, int expected)
    {
        Assert.Equal(expected, SearchNavigator.Next(current, count));
    }

    [Theory]
    [InlineData(1, 3, 0)]
    [InlineData(0, 3, 2)]
    public void Previous_WrapsAround(int current, int count, int expected)
    {
        Assert.Equal(expected, SearchNavigator.Previous(current, count));
    }

    [Fact]
    public void Next_ReturnsNegativeOne_WhenNoMatches()
    {
        Assert.Equal(-1, SearchNavigator.Next(0, 0));
    }
}
```

Run: `cd "D:/claude/DalView" && dotnet test tests/DalView.Tests/DalView.Tests.csproj --filter SearchNavigatorTests`
Expected: FAIL — `SearchNavigator` does not exist yet.

- [ ] **Step 2: Implement SearchNavigator**

Create `src/DalView/ViewModels/SearchNavigator.cs`:

```csharp
namespace DalView.ViewModels;

public static class SearchNavigator
{
    public static int Next(int currentIndex, int count)
    {
        if (count <= 0) return -1;
        return (currentIndex + 1) % count;
    }

    public static int Previous(int currentIndex, int count)
    {
        if (count <= 0) return -1;
        return currentIndex <= 0 ? count - 1 : currentIndex - 1;
    }
}
```

- [ ] **Step 3: Run test to verify it passes**

Run: `cd "D:/claude/DalView" && dotnet test tests/DalView.Tests/DalView.Tests.csproj --filter SearchNavigatorTests`
Expected: PASS (3 tests).

- [ ] **Step 4: Wire search into MainViewModel**

Add to `src/DalView/ViewModels/MainViewModel.cs` (inside the `MainViewModel` class, alongside the existing members):

```csharp
    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private PdfMatches? matches;

    [ObservableProperty]
    private int matchIndex = -1;

    [ObservableProperty]
    private bool highlightAllMatches = true;

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (Document == null || string.IsNullOrWhiteSpace(SearchText))
        {
            Matches = null;
            MatchIndex = -1;
            return;
        }

        var doc = Document;
        var query = SearchText;
        var result = await Task.Run(() => doc.Search(query, matchCase: false, wholeWord: false, 0, doc.PageCount - 1));

        Matches = result;
        MatchIndex = result.Items.Count > 0 ? 0 : -1;
        StatusMessage = result.Items.Count > 0
            ? $"{result.Items.Count}건 찾음"
            : "찾는 내용이 없습니다.";
    }

    [RelayCommand]
    private void NextMatch()
    {
        if (Matches == null) return;
        MatchIndex = SearchNavigator.Next(MatchIndex, Matches.Items.Count);
    }

    [RelayCommand]
    private void PreviousMatch()
    {
        if (Matches == null) return;
        MatchIndex = SearchNavigator.Previous(MatchIndex, Matches.Items.Count);
    }
```

Add these `using` statements to the top of the file:

```csharp
using System.Threading.Tasks;
```

(`PdfiumViewer.Core` is already imported from Task 1, which covers `PdfMatches`.)

- [ ] **Step 5: Add the search box to MainWindow.xaml**

Add inside the existing `<ToolBar>` in `src/DalView/MainWindow.xaml`, after the `FitWidth` `CheckBox`:

```xml
            <Separator />
            <TextBox Width="160" Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" />
            <Button Content="검색" Command="{Binding SearchCommand}" Padding="6,2" />
            <Button Content="◀" Command="{Binding PreviousMatchCommand}" Width="26" />
            <Button Content="▶" Command="{Binding NextMatchCommand}" Width="26" />
```

Add these attributes to the `<pdf:PDFViewer ...>` element in the same file (alongside the existing bound properties):

```xml
                       Matches="{Binding Matches}"
                       MatchIndex="{Binding MatchIndex, Mode=TwoWay}"
                       HighlightAllMatches="{Binding HighlightAllMatches}"
```

- [ ] **Step 6: Build and manually verify**

Run: `cd "D:/claude/DalView" && dotnet build src/DalView/DalView.csproj`
Expected: 0 errors.

Run: `dotnet run --project src/DalView/DalView.csproj`
Expected: Open a real multi-page PDF with known text. Type a word into the search box, click "검색" — matches highlight yellow across the document, status bar shows the match count, ◀/▶ jump between matches and wrap around at the ends.

- [ ] **Step 7: Commit**

```bash
cd "D:/claude/DalView"
git add src/DalView tests/DalView.Tests
git commit -m "Add text search with wraparound match navigation"
```

---

### Task 3: Bookmarks (outline) sidebar

**Files:**
- Modify: `src/DalView/MainWindow.xaml`

**Interfaces:**
- Consumes: `IPdfDocument.Bookmarks : PdfBookmarkCollection` (a `Collection<PdfBookmark>`); `PdfBookmark.Title (string)`, `PdfBookmark.PageIndex (int)`, `PdfBookmark.Children (PdfBookmarkCollection)`.
- Produces: nothing new consumed by later tasks — this is a leaf UI feature bound directly to `Document.Bookmarks` and `Page` from Task 1.

No unit test for this task — it is pure XAML data-binding against a third-party collection type; correctness is verified by manual inspection with a real PDF that has an outline (most books/manuals do).

- [ ] **Step 1: Add a left sidebar with a Bookmarks tab**

Wrap the existing `<pdf:PDFViewer .../>` element in `src/DalView/MainWindow.xaml` inside a `Grid` with two columns, and add a `TabControl` sidebar in the first column. Replace the `<DockPanel>` body (everything between `<Window.DataContext>...</Window.DataContext>` and `</Window>`) with:

Keep the `<ToolBar>` content exactly as it stands after Task 2 (the file already has it — do not delete or replace it, only the surrounding `<DockPanel>`/`<Grid>` structure changes). For reference, the ToolBar's full content at this point is:

```xml
        <ToolBar DockPanel.Dock="Top">
            <Button Content="열기" Command="{Binding OpenFileCommand}" Padding="8,2" />
            <Separator />
            <TextBox Width="50" Text="{Binding Page, Mode=TwoWay}" TextAlignment="Center" />
            <TextBlock VerticalAlignment="Center" Margin="4,0">
                <Run Text="/ " /><Run Text="{Binding PageCount, Mode=OneWay}" />
            </TextBlock>
            <Separator />
            <Button Content="－" Command="{Binding ZoomOutCommand}" Width="28" />
            <TextBlock VerticalAlignment="Center" Margin="4,0" Text="{Binding Zoom, StringFormat={}{0:P0}}" />
            <Button Content="＋" Command="{Binding ZoomInCommand}" Width="28" />
            <CheckBox Content="폭 맞춤" IsChecked="{Binding FitWidth}" VerticalAlignment="Center" Margin="8,0" />
            <Separator />
            <TextBox Width="160" Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" />
            <Button Content="검색" Command="{Binding SearchCommand}" Padding="6,2" />
            <Button Content="◀" Command="{Binding PreviousMatchCommand}" Width="26" />
            <Button Content="▶" Command="{Binding NextMatchCommand}" Width="26" />
        </ToolBar>
```

The full replacement for the `<DockPanel>` body is:

```xml
    <DockPanel>
        <ToolBar DockPanel.Dock="Top">
            <Button Content="열기" Command="{Binding OpenFileCommand}" Padding="8,2" />
            <Separator />
            <TextBox Width="50" Text="{Binding Page, Mode=TwoWay}" TextAlignment="Center" />
            <TextBlock VerticalAlignment="Center" Margin="4,0">
                <Run Text="/ " /><Run Text="{Binding PageCount, Mode=OneWay}" />
            </TextBlock>
            <Separator />
            <Button Content="－" Command="{Binding ZoomOutCommand}" Width="28" />
            <TextBlock VerticalAlignment="Center" Margin="4,0" Text="{Binding Zoom, StringFormat={}{0:P0}}" />
            <Button Content="＋" Command="{Binding ZoomInCommand}" Width="28" />
            <CheckBox Content="폭 맞춤" IsChecked="{Binding FitWidth}" VerticalAlignment="Center" Margin="8,0" />
            <Separator />
            <TextBox Width="160" Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" />
            <Button Content="검색" Command="{Binding SearchCommand}" Padding="6,2" />
            <Button Content="◀" Command="{Binding PreviousMatchCommand}" Width="26" />
            <Button Content="▶" Command="{Binding NextMatchCommand}" Width="26" />
        </ToolBar>
        <StatusBar DockPanel.Dock="Bottom">
            <StatusBarItem Content="{Binding StatusMessage}" />
        </StatusBar>
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="220" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>

            <TabControl Grid.Column="0">
                <TabItem Header="목차">
                    <TreeView ItemsSource="{Binding Document.Bookmarks}">
                        <TreeView.ItemContainerStyle>
                            <Style TargetType="TreeViewItem">
                                <EventSetter Event="MouseDoubleClick" Handler="BookmarkItem_MouseDoubleClick" />
                            </Style>
                        </TreeView.ItemContainerStyle>
                        <TreeView.Resources>
                            <HierarchicalDataTemplate DataType="{x:Type pdfcore:PdfBookmark}" ItemsSource="{Binding Children}">
                                <TextBlock Text="{Binding Title}" TextTrimming="CharacterEllipsis" />
                            </HierarchicalDataTemplate>
                        </TreeView.Resources>
                    </TreeView>
                </TabItem>
            </TabControl>

            <pdf:PDFViewer x:Name="Viewer" Grid.Column="1"
                           Document="{Binding Document, Mode=TwoWay}"
                           Page="{Binding Page, Mode=TwoWay}"
                           PageCount="{Binding PageCount, Mode=OneWayToSource}"
                           Zoom="{Binding Zoom, Mode=TwoWay}"
                           ZoomMin="{Binding ZoomMin}"
                           ZoomMax="{Binding ZoomMax}"
                           FitWidth="{Binding FitWidth}"
                           Matches="{Binding Matches}"
                           MatchIndex="{Binding MatchIndex, Mode=TwoWay}"
                           HighlightAllMatches="{Binding HighlightAllMatches}"
                           Padding="12" />
        </Grid>
    </DockPanel>
```

Add the `pdfcore` namespace to the `<Window ...>` root element's attribute list (alongside the existing `pdf` and `vm` namespaces):

```xml
        xmlns:pdfcore="clr-namespace:PdfiumViewer.Core;assembly=PdfiumViewer"
```

- [ ] **Step 2: Handle bookmark double-click navigation**

Add to `src/DalView/MainWindow.xaml.cs`:

```csharp
using System.Windows.Controls;
using DalView.ViewModels;
using PdfiumViewer.Core;

namespace DalView;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BookmarkItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: PdfBookmark bookmark } && DataContext is MainViewModel vm)
        {
            vm.Page = bookmark.PageIndex;
        }
        e.Handled = true;
    }
}
```

- [ ] **Step 3: Build and manually verify**

Run: `cd "D:/claude/DalView" && dotnet build src/DalView/DalView.csproj`
Expected: 0 errors.

Run: `dotnet run --project src/DalView/DalView.csproj`
Expected: Open a PDF that has a table of contents / outline (most technical PDFs and e-books do) — the "목차" tab shows a nested tree; double-clicking an entry jumps the viewer to that page. Open a PDF with no outline — the tree is simply empty (no crash).

- [ ] **Step 4: Commit**

```bash
cd "D:/claude/DalView"
git add src/DalView
git commit -m "Add bookmarks/outline sidebar with double-click navigation"
```

---

### Task 4: Thumbnail sidebar (lazy-rendered)

**Files:**
- Create: `src/DalView/Services/ThumbnailRenderer.cs`
- Create: `src/DalView/ViewModels/ThumbnailItem.cs`
- Create: `tests/DalView.Tests/ThumbnailRendererTests.cs`
- Modify: `src/DalView/ViewModels/MainViewModel.cs`
- Modify: `src/DalView/MainWindow.xaml`
- Modify: `src/DalView/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `IPdfDocument.Pages : IReadOnlyList<PdfPage>` (Task 1); `PdfPage.Render(int width, int height, float dpiX, float dpiY, PdfRotation rotate, PdfRenderFlags flags) : System.Drawing.Image`; `PdfPage.Size : System.Windows.Size`.
- Produces: `ThumbnailRenderer.RenderThumbnail(IPdfDocument document, int pageIndex) : BitmapImage`, used only inside `ThumbnailItem`. `MainViewModel.Thumbnails : ObservableCollection<ThumbnailItem>`.

- [ ] **Step 1: Write the failing test for thumbnail rendering**

Create `tests/DalView.Tests/ThumbnailRendererTests.cs`:

```csharp
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
}
```

Run: `cd "D:/claude/DalView" && dotnet test tests/DalView.Tests/DalView.Tests.csproj --filter ThumbnailRendererTests`
Expected: FAIL — `DalView.Services.ThumbnailRenderer` does not exist yet.

- [ ] **Step 2: Implement ThumbnailRenderer**

Create `src/DalView/Services/ThumbnailRenderer.cs`:

```csharp
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
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
```

- [ ] **Step 3: Run test to verify it passes**

Run: `cd "D:/claude/DalView" && dotnet test tests/DalView.Tests/DalView.Tests.csproj --filter ThumbnailRendererTests`
Expected: PASS.

- [ ] **Step 4: Create the lazy-loading ThumbnailItem view model**

Create `src/DalView/ViewModels/ThumbnailItem.cs`:

```csharp
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using DalView.Services;
using PdfiumViewer.Core;

namespace DalView.ViewModels;

public partial class ThumbnailItem : ObservableObject
{
    private readonly IPdfDocument _document;
    private bool _loadStarted;

    public ThumbnailItem(IPdfDocument document, int pageIndex)
    {
        _document = document;
        PageIndex = pageIndex;
    }

    public int PageIndex { get; }

    public int DisplayNumber => PageIndex + 1;

    [ObservableProperty]
    private BitmapImage? thumbnail;

    public void EnsureLoaded()
    {
        if (_loadStarted) return;
        _loadStarted = true;

        var document = _document;
        var pageIndex = PageIndex;

        Task.Run(() => ThumbnailRenderer.RenderThumbnail(document, pageIndex))
            .ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    Application.Current.Dispatcher.Invoke(() => Thumbnail = t.Result);
                }
            }, TaskScheduler.Default);
    }
}
```

- [ ] **Step 5: Populate Thumbnails when a document opens**

In `src/DalView/ViewModels/MainViewModel.cs`, add the property:

```csharp
    [ObservableProperty]
    private ObservableCollection<ThumbnailItem> thumbnails = new();
```

and add `using System.Collections.ObjectModel;` to the top of the file.

In `OpenPath`, right after the line `StatusMessage = $"{Path.GetFileName(path)} ({newDocument.PageCount} pages)";`, add:

```csharp
            Thumbnails = new ObservableCollection<ThumbnailItem>(
                Enumerable.Range(0, newDocument.PageCount).Select(i => new ThumbnailItem(newDocument, i)));
```

and add `using System.Linq;` to the top of the file.

- [ ] **Step 6: Add the thumbnail tab to MainWindow.xaml**

Add a second `<TabItem>` inside the `<TabControl>` from Task 3, before or after the "목차" tab:

```xml
                <TabItem Header="썸네일">
                    <ListBox ItemsSource="{Binding Thumbnails}"
                             SelectedItem="{Binding SelectedThumbnail, Mode=OneWayToSource}"
                             ScrollViewer.CanContentScroll="True">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal" Margin="2">
                                    <Image Width="80" Loaded="ThumbnailImage_Loaded" Source="{Binding Thumbnail}" />
                                    <TextBlock Text="{Binding DisplayNumber}" VerticalAlignment="Center" Margin="6,0" />
                                </StackPanel>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </TabItem>
```

- [ ] **Step 7: Trigger lazy loading and click-to-navigate in code-behind**

Add to `src/DalView/MainWindow.xaml.cs` (inside the `MainWindow` class):

```csharp
    private void ThumbnailImage_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThumbnailItem item })
        {
            item.EnsureLoaded();
        }
    }
```

Add `using System.Windows;` (already present) — `RoutedEventArgs` and `FrameworkElement` are both in `System.Windows`.

Add to `MainViewModel`, a settable property so clicking a thumbnail navigates (bound via `SelectedItem` two-way in a follow-up, but for v1 clicking the row is enough — add a click handler instead of relying on `SelectedThumbnail`). Replace the `SelectedItem` binding added in Step 6 with a `MouseDown` handler on the `StackPanel`:

```xml
                                <StackPanel Orientation="Horizontal" Margin="2" MouseDown="ThumbnailRow_MouseDown">
```

(remove the `ListBox.SelectedItem` binding line added in Step 6 — it isn't needed once the row itself handles the click)

Add to `src/DalView/MainWindow.xaml.cs`:

```csharp
    private void ThumbnailRow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThumbnailItem item } && DataContext is MainViewModel vm)
        {
            vm.Page = item.PageIndex;
        }
    }
```

- [ ] **Step 8: Build and manually verify**

Run: `cd "D:/claude/DalView" && dotnet build src/DalView/DalView.csproj`
Expected: 0 errors.

Run: `dotnet run --project src/DalView/DalView.csproj`
Expected: Open a real multi-page (50+) PDF. Switch to the "썸네일" tab — visible thumbnails render within a second; scrolling further down renders more as they come into view (not all 50+ up front — check Task Manager or just observe it doesn't stall on open). Click a thumbnail — main view jumps to that page.

- [ ] **Step 9: Commit**

```bash
cd "D:/claude/DalView"
git add src/DalView tests/DalView.Tests
git commit -m "Add lazy-loaded thumbnail sidebar"
```

---

### Task 5: Error handling — corrupted files and password-protected PDFs

**Files:**
- Create: `src/DalView/PasswordDialog.xaml`
- Create: `src/DalView/PasswordDialog.xaml.cs`
- Create: `tests/DalView.Tests/MainViewModelTests.cs`
- Modify: `src/DalView/ViewModels/MainViewModel.cs`
- Modify: `src/DalView/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `IPdfDocumentLoader` (Task 1) — tests substitute a fake implementation instead of the real PDFium-backed one.
- Produces: `MainViewModel.PasswordRequired` event (`EventHandler<string>`, argument is the file path) that the view subscribes to in order to show `PasswordDialog` and retry `OpenPath` with the entered password.

- [ ] **Step 1: Write the failing tests for error branches**

Create `tests/DalView.Tests/MainViewModelTests.cs`:

```csharp
using DalView.Services;
using DalView.ViewModels;
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
```

Run: `cd "D:/claude/DalView" && dotnet test tests/DalView.Tests/DalView.Tests.csproj --filter MainViewModelTests`
Expected: FAIL — `OpenPath_CorruptedFile...` fails because `OpenPath` currently rethrows on `PasswordProtected` (fine, second test expects that path to raise an event, not throw) and there is no `PasswordRequired` event yet, so the second test fails to compile/run.

- [ ] **Step 2: Add the PasswordRequired event and stop rethrowing**

In `src/DalView/ViewModels/MainViewModel.cs`, replace the `OpenPath` method's `catch (PdfException ex) when (ex.Error == PdfError.PasswordProtected)` block:

```csharp
    public event EventHandler<string>? PasswordRequired;

    public void OpenPath(string path, string? password)
    {
        try
        {
            var newDocument = _loader.Load(path, password);
            Document?.Dispose();
            Document = newDocument;
            PdfPath = path;
            Page = 0;
            StatusMessage = $"{Path.GetFileName(path)} ({newDocument.PageCount} pages)";
            Thumbnails = new ObservableCollection<ThumbnailItem>(
                Enumerable.Range(0, newDocument.PageCount).Select(i => new ThumbnailItem(newDocument, i)));
        }
        catch (PdfException ex) when (ex.Error == PdfError.PasswordProtected)
        {
            StatusMessage = "이 PDF는 암호로 보호되어 있습니다.";
            PasswordRequired?.Invoke(this, path);
        }
        catch (PdfException ex)
        {
            StatusMessage = $"PDF를 열 수 없습니다: {ex.Message}";
        }
    }
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `cd "D:/claude/DalView" && dotnet test tests/DalView.Tests/DalView.Tests.csproj --filter MainViewModelTests`
Expected: PASS (2 tests).

Run the full suite to confirm nothing else broke:
Run: `cd "D:/claude/DalView" && dotnet test tests/DalView.Tests/DalView.Tests.csproj`
Expected: All tests PASS.

- [ ] **Step 4: Create the password prompt dialog**

`src/DalView/PasswordDialog.xaml`:

```xml
<Window x:Class="DalView.PasswordDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="암호 입력" Height="150" Width="320"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <StackPanel Margin="16">
        <TextBlock Text="이 PDF는 암호로 보호되어 있습니다." Margin="0,0,0,8" />
        <PasswordBox x:Name="PasswordBox" Margin="0,0,0,12" />
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="취소" Width="70" Margin="0,0,8,0" IsCancel="True" />
            <Button Content="확인" Width="70" IsDefault="True" Click="Ok_Click" />
        </StackPanel>
    </StackPanel>
</Window>
```

`src/DalView/PasswordDialog.xaml.cs`:

```csharp
using System.Windows;

namespace DalView;

public partial class PasswordDialog : Window
{
    public string Password { get; private set; } = string.Empty;

    public PasswordDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Password = PasswordBox.Password;
        DialogResult = true;
    }
}
```

- [ ] **Step 5: Wire the dialog into MainWindow**

Add to `src/DalView/MainWindow.xaml.cs` constructor, after `InitializeComponent();`:

```csharp
        if (DataContext is MainViewModel vm)
        {
            vm.PasswordRequired += OnPasswordRequired;
        }
```

Add the handler method:

```csharp
    private void OnPasswordRequired(object? sender, string path)
    {
        var dialog = new PasswordDialog { Owner = this };
        if (dialog.ShowDialog() == true && sender is MainViewModel vm)
        {
            vm.OpenPath(path, dialog.Password);
        }
    }
```

(`MainViewModel` needs a `using DalView.ViewModels;` already present from Task 3.)

- [ ] **Step 6: Build and manually verify**

Run: `cd "D:/claude/DalView" && dotnet build src/DalView/DalView.csproj`
Expected: 0 errors.

Run: `dotnet run --project src/DalView/DalView.csproj`
Expected: Opening a corrupted/non-PDF file renamed to `.pdf` shows a status-bar error message and the app stays open. Opening a password-protected PDF pops the password dialog; entering the correct password opens it; entering the wrong one re-prompts (status bar shows the "암호로 보호되어 있습니다" message again since the retried `Load` throws the same `PdfException`).

- [ ] **Step 7: Commit**

```bash
cd "D:/claude/DalView"
git add src/DalView tests/DalView.Tests
git commit -m "Add error handling for corrupted and password-protected PDFs"
```

---

### Task 6: Polish and publish

**Files:**
- Modify: `src/DalView/DalView.csproj`
- Modify: `src/DalView/MainWindow.xaml`

**Interfaces:**
- Consumes: nothing new.
- Produces: a publishable executable; no new code interfaces for later tasks (this is the last task).

- [ ] **Step 1: Set window/taskbar identity**

In `src/DalView/MainWindow.xaml`, confirm `Title="달뷰"` is set (done since Task 1). No icon file is required for v1 — skip adding one rather than sourcing/guessing binary icon content (YAGNI; can be added later by dropping an `.ico` file in and setting `<ApplicationIcon>` in the csproj).

- [ ] **Step 2: Configure a framework-dependent, single-file publish profile**

Add to `src/DalView/DalView.csproj`, inside the existing `<PropertyGroup>`:

```xml
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
```

Framework-dependent (`SelfContained=false`) keeps the published output small — it relies on the .NET 8 desktop runtime already being installed, which is the standard case on an up-to-date Windows machine and keeps DalView from bundling a full private runtime copy (that would defeat the "가볍다" goal).

- [ ] **Step 3: Publish and verify**

Run: `cd "D:/claude/DalView" && dotnet publish src/DalView/DalView.csproj -c Release`
Expected: Build succeeds; output appears under `src/DalView/bin/Release/net8.0-windows/win-x64/publish/DalView.exe`.

Run: `& "D:/claude/DalView/src/DalView/bin/Release/net8.0-windows/win-x64/publish/DalView.exe"`
Expected: App launches standalone from the publish folder (not `dotnet run`). Open a large (200+ page) real-world PDF and confirm: scrolling is smooth, thumbnails populate lazily as you scroll the sidebar, search and bookmarks both work, and the process's working set stays reasonable (spot-check via Task Manager — no requirement to automate this, just confirm it isn't pathologically bloated).

- [ ] **Step 4: Commit**

```bash
cd "D:/claude/DalView"
git add src/DalView/DalView.csproj
git commit -m "Configure framework-dependent single-file publish"
```

---

## Self-Review Notes

- **Spec coverage:** page nav/zoom/scroll → Task 1; text search → Task 2; sidebar thumbnails/bookmarks → Tasks 3–4; error handling (corrupted/password PDFs) → Task 5; no network/editing/printing anywhere → never added. Packaging isn't in the original spec's explicit feature list but is required for the app to be usable as a replacement for the heavy reader, so Task 6 covers it.
- **Deviation from spec's architecture section, and why it's still faithful:** the spec called for a hand-rolled `VirtualizingStackPanel`-based renderer. Verified against the actual `PdfiumViewer.Net.WPF` 3.0.4 source (fetched directly from `github.com/vrjure/PdfiumViewer`) that its `PDFViewer` control already does scroll-range virtualized rendering (`PDFViewer.cs`'s `RenderRange`/`_scroll_ScrollChanged` logic renders only pages near the viewport). Building a second, redundant virtualization layer on top would add complexity without adding capability, so the plan uses the control's built-in behavior directly. The spec's actual requirement — fast response regardless of page count — is preserved.
- **Type consistency check:** `IPdfDocumentLoader.Load` (Task 1) is used unchanged by `MainViewModel` through Tasks 1–5; `SearchNavigator.Next/Previous` (Task 2) signatures match their call sites in `MainViewModel.NextMatch/PreviousMatch`; `ThumbnailRenderer.RenderThumbnail` (Task 4) signature matches its use in `ThumbnailItem.EnsureLoaded`; `MainViewModel.PasswordRequired` (Task 5) matches the subscription in `MainWindow.xaml.cs`.
- **Placeholder scan:** no TBD/TODO markers; every step has literal code or an exact shell command.
