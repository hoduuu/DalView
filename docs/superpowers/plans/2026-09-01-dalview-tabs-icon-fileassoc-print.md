# DalView Tabs / Icon / File Association / Print Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add multi-document tabs, an app icon, Windows file-association support (single-instance + open-as-new-tab), and printing to the already-shipped 달뷰 (DalView) PDF reader.

**Architecture:** Split the existing single-document `MainViewModel` into a new `DocumentTabViewModel` (one per open PDF — owns everything that's currently per-document state) and a thin `MainViewModel` that owns the collection of open tabs. `MainWindow.xaml` gains a `TabControl` bound to that collection; the entire existing toolbar/sidebar/viewer layout becomes the `TabControl.ContentTemplate`, so almost none of the existing per-document XAML bindings need to change — only the two commands that must reach the tab-container level (`OpenFileCommand`, `CloseTabCommand`) need an explicit `RelativeSource` hop. File association reuses VideoPlayer's proven named-mutex + named-pipe single-instance pattern verbatim. Printing is a thin wrapper around `IPdfDocument.CreatePrintDocument()`, which the PdfiumViewer.Net.WPF library already provides.

**Tech Stack:** Same as v1 (C# / .NET 8.0-windows, WPF, `PdfiumViewer.Net.WPF` 3.0.4, `CommunityToolkit.Mvvm` 8.4.2, xUnit) plus `System.Windows.Forms`'s `PrintDialog` (fully-qualified, not a project-wide `using`, to avoid `UseWindowsForms`'s implicit global usings colliding with existing WPF type names — verified empirically, see Task 3).

**Spec:** [docs/superpowers/specs/2026-09-01-dalview-tabs-icon-fileassoc-print-design.md](../specs/2026-09-01-dalview-tabs-icon-fileassoc-print-design.md)

## Global Constraints

- Target framework: `net8.0-windows` everywhere (unchanged from v1).
- No network calls anywhere in the app (unchanged from v1) — the named-pipe IPC in Task 4 is local-machine-only inter-process communication, not network I/O.
- No editing/annotation/signing features (unchanged from v1). Printing (Task 3) is newly in scope per this plan.
- App root namespace: `DalView` (unchanged).
- Registry-based file-association auto-registration is explicitly OUT of scope — the app never writes to the Windows Registry. Users associate `.pdf` with DalView manually via Explorer's "Open with" (same as VideoPlayer's actual behavior — verified by reading VideoPlayer's source, which has single-instance/pipe code but no registry-writing code at all).
- Closing the last open tab quits the application (no empty-shell state).

---

### Task 1: App icon

**Files:**
- Create: `src/DalView/Assets/icon.ico`
- Modify: `src/DalView/DalView.csproj`
- Modify: `src/DalView/MainWindow.xaml`

**Interfaces:**
- Produces: nothing consumed by later tasks — purely cosmetic, independent of every other task in this plan.

- [ ] **Step 1: Generate a multi-resolution .ico from the source PNG**

The source image is at `D:\claude\DalView\icon.png` (1245×1245, transparent background). Generate a real 4-size (16/32/48/256px) `.ico` using the "PNG-in-ICO" container format (supported since Windows Vista — each size entry embeds a re-encoded PNG rather than raw BMP data, which keeps this script self-contained with no external tools). Run this PowerShell script:

```powershell
Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Image]::FromFile("D:\claude\DalView\icon.png")
$sizes = @(16, 32, 48, 256)
$pngBytesList = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($src, 0, 0, $s, $s)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytesList += ,$ms.ToArray()
    $bmp.Dispose()
    $ms.Dispose()
}
$src.Dispose()

$outPath = "D:\claude\DalView\src\DalView\Assets\icon.ico"
New-Item -ItemType Directory -Force -Path (Split-Path $outPath) | Out-Null

$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([uint16]0)              # ICONDIR.reserved
$bw.Write([uint16]1)              # ICONDIR.type (1 = icon)
$bw.Write([uint16]$sizes.Count)   # ICONDIR.count

$headerSize = 6 + (16 * $sizes.Count)
$offset = $headerSize
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $byteSize = $pngBytesList[$i].Length
    $wByte = if ($s -ge 256) { 0 } else { $s }
    $hByte = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$wByte)
    $bw.Write([byte]$hByte)
    $bw.Write([byte]0)            # color count
    $bw.Write([byte]0)            # reserved
    $bw.Write([uint16]1)          # planes
    $bw.Write([uint16]32)         # bit count
    $bw.Write([uint32]$byteSize)
    $bw.Write([uint32]$offset)
    $offset += $byteSize
}
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $bw.Write($pngBytesList[$i])
}
$bw.Flush()
$bw.Close()
$fs.Close()

Write-Output "Wrote $outPath"
```

- [ ] **Step 2: Verify the .ico file was written and looks valid**

Run: `Get-Item "D:\claude\DalView\src\DalView\Assets\icon.ico" | Select-Object Length`
Expected: file exists, length is a few tens of KB (4 embedded PNGs, not 0 bytes).

- [ ] **Step 3: Wire the icon into the csproj and window**

Add to `src/DalView/DalView.csproj`, inside the existing `<PropertyGroup>`:

```xml
    <ApplicationIcon>Assets\icon.ico</ApplicationIcon>
```

Add a new `<ItemGroup>` (so WPF's pack-URI resolution for `Window.Icon` can find the file as an embedded resource):

```xml
  <ItemGroup>
    <Resource Include="Assets\icon.ico" />
  </ItemGroup>
```

In `src/DalView/MainWindow.xaml`, add `Icon="Assets/icon.ico"` to the `<Window ...>` root element's attributes (alongside the existing `Title="달뷰"` etc.).

- [ ] **Step 4: Build and verify**

Run: `cd "D:/claude/DalView" && "/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" build DalView.sln`
Expected: 0 errors.

Run: `"/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" run --project src/DalView/DalView.csproj`
Expected: the app window's title-bar icon (top-left) shows the moon+PDF icon, not the default WPF icon. Check the taskbar icon too while the app is running.

- [ ] **Step 5: Commit**

```bash
cd "D:/claude/DalView"
git add src/DalView/Assets/icon.ico src/DalView/DalView.csproj src/DalView/MainWindow.xaml
git commit -m "Add app icon"
```

---

### Task 2: Multi-document tabs

**Files:**
- Create: `src/DalView/ViewModels/DocumentTabViewModel.cs`
- Modify: `src/DalView/ViewModels/MainViewModel.cs` (full rewrite — becomes a thin tab container)
- Modify: `src/DalView/MainWindow.xaml` (full rewrite of the body — wraps existing layout in a `TabControl`)
- Modify: `src/DalView/MainWindow.xaml.cs`
- Create: `tests/DalView.Tests/MainViewModelTabTests.cs`

**Interfaces:**
- Produces: `DocumentTabViewModel` — carries everything v1's `MainViewModel` had (PdfPath, Document, Page, DisplayPage, PageCount, Zoom/ZoomMin/ZoomMax, FitWidth, StatusMessage, SearchText/Matches/MatchIndex/HighlightAllMatches, Thumbnails, `OpenPath(string, string?)`, `PasswordRequired` event, ZoomIn/ZoomOut/Search/NextMatch/PreviousMatch commands), plus a new read-only `Title` property (filename, or "새 탭" before a file is loaded).
- Produces: `MainViewModel.Tabs : ObservableCollection<DocumentTabViewModel>`, `MainViewModel.SelectedTab : DocumentTabViewModel?`, `MainViewModel.OpenFileCommand`, `MainViewModel.CloseTabCommand` (takes a `DocumentTabViewModel` parameter), `MainViewModel.OpenPathAsNewTab(string path) : void` — this last one is the shared entry point Task 4's pipe-receiver will call directly (bypassing the file dialog), so its exact name and signature is load-bearing for that later task.
- Consumes: `IPdfDocumentLoader` (unchanged from v1, injected into `MainViewModel`, which passes it to each `DocumentTabViewModel` it creates).

- [ ] **Step 1: Create DocumentTabViewModel — the v1 MainViewModel's content, renamed and extended**

Create `src/DalView/ViewModels/DocumentTabViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DalView.Services;
using PdfiumViewer;
using PdfiumViewer.Core;
using PdfiumViewer.Enums;

namespace DalView.ViewModels;

public partial class DocumentTabViewModel : ObservableObject
{
    private readonly IPdfDocumentLoader _loader;

    public event EventHandler<string>? PasswordRequired;

    public DocumentTabViewModel(IPdfDocumentLoader loader)
    {
        _loader = loader;
    }

    public string Title => string.IsNullOrEmpty(PdfPath) ? "새 탭" : Path.GetFileName(PdfPath);

    [ObservableProperty]
    private string? pdfPath;

    partial void OnPdfPathChanged(string? value) => OnPropertyChanged(nameof(Title));

    [ObservableProperty]
    private IPdfDocument? document;

    [ObservableProperty]
    private int page;

    /// <summary>1-based page number for display in the toolbar. <see cref="Page"/> itself stays 0-based.</summary>
    public int DisplayPage
    {
        get => Page + 1;
        set => Page = value - 1;
    }

    partial void OnPageChanged(int value) => OnPropertyChanged(nameof(DisplayPage));

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

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private PdfMatches? matches;

    [ObservableProperty]
    private int matchIndex = -1;

    [ObservableProperty]
    private bool highlightAllMatches = true;

    [ObservableProperty]
    private ObservableCollection<ThumbnailItem> thumbnails = new();

    public void OpenPath(string path, string? password)
    {
        try
        {
            var newDocument = _loader.Load(path, password);
            // PDFViewer's DocumentChanged handler disposes the previous Document synchronously
            // when this property changes — do not dispose it here (would be a redundant no-op at best).
            Document = newDocument;
            PdfPath = path;
            Page = 0;
            Matches = null;
            MatchIndex = -1;
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
        catch (Exception ex)
        {
            StatusMessage = $"PDF를 열 수 없습니다: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(ZoomMax, Math.Round(Zoom + 0.1, 2));

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(ZoomMin, Math.Round(Zoom - 0.1, 2));

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

        if (Document != doc) return;

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
}
```

This is byte-for-byte the current `MainViewModel.cs`'s content (open/zoom/search logic), with the class renamed, the constructor's parameterless overload removed (every `DocumentTabViewModel` is now created by `MainViewModel`, which already owns the loader — no more standalone/design-time construction need), and `Title`/`OnPdfPathChanged` added. Printing is intentionally NOT included here — Task 3 adds it separately as a focused, independently-reviewable change.

- [ ] **Step 2: Rewrite MainViewModel as a thin tab container**

Replace the entire contents of `src/DalView/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DalView.Services;
using Microsoft.Win32;

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
    private ObservableCollection<DocumentTabViewModel> tabs = new();

    [ObservableProperty]
    private DocumentTabViewModel? selectedTab;

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

        OpenPathAsNewTab(dialog.FileName);
    }

    public void OpenPathAsNewTab(string path)
    {
        var tab = new DocumentTabViewModel(_loader);
        tab.OpenPath(path, password: null);
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    [RelayCommand]
    private void CloseTab(DocumentTabViewModel tab)
    {
        Tabs.Remove(tab);
        if (Tabs.Count == 0)
        {
            Application.Current?.Shutdown();
        }
    }
}
```

Note `Application.Current?.Shutdown()` — the null-conditional matters: in a plain unit-test host there is no running `Application`, so `Application.Current` is `null` there, and this must not throw.

- [ ] **Step 3: Rewrite MainWindow.xaml with a TabControl**

Replace the entire contents of `src/DalView/MainWindow.xaml`:

```xml
<Window x:Class="DalView.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:pdf="clr-namespace:PdfiumViewer;assembly=PdfiumViewer"
        xmlns:vm="clr-namespace:DalView.ViewModels"
        xmlns:pdfcore="clr-namespace:PdfiumViewer.Core;assembly=PdfiumViewer"
        Icon="Assets/icon.ico"
        Title="달뷰" Height="800" Width="1100" Background="#FAFAF8">
    <Window.DataContext>
        <vm:MainViewModel />
    </Window.DataContext>
    <TabControl ItemsSource="{Binding Tabs}" SelectedItem="{Binding SelectedTab, Mode=TwoWay}">
        <TabControl.ItemTemplate>
            <DataTemplate DataType="{x:Type vm:DocumentTabViewModel}">
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="{Binding Title}" VerticalAlignment="Center" MaxWidth="160"
                               TextTrimming="CharacterEllipsis" Margin="0,0,6,0" />
                    <Button Content="×" Width="18" Height="18" Padding="0" FontSize="11"
                            Command="{Binding DataContext.CloseTabCommand, RelativeSource={RelativeSource AncestorType=TabControl}}"
                            CommandParameter="{Binding}" />
                </StackPanel>
            </DataTemplate>
        </TabControl.ItemTemplate>
        <TabControl.ContentTemplate>
            <DataTemplate DataType="{x:Type vm:DocumentTabViewModel}">
                <DockPanel>
                    <ToolBar DockPanel.Dock="Top">
                        <Button Content="열기"
                                Command="{Binding DataContext.OpenFileCommand, RelativeSource={RelativeSource AncestorType=TabControl}}"
                                Padding="8,2" />
                        <Separator />
                        <TextBox Width="50" Text="{Binding DisplayPage, Mode=TwoWay}" TextAlignment="Center" />
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
                            <TabItem Header="썸네일">
                                <ListBox ItemsSource="{Binding Thumbnails}"
                                         ScrollViewer.CanContentScroll="True">
                                    <ListBox.ItemTemplate>
                                        <DataTemplate>
                                            <StackPanel Orientation="Horizontal" Margin="2" MouseDown="ThumbnailRow_MouseDown">
                                                <Image Width="80" Loaded="ThumbnailImage_Loaded" Source="{Binding Thumbnail}" />
                                                <TextBlock Text="{Binding DisplayNumber}" VerticalAlignment="Center" Margin="6,0" />
                                            </StackPanel>
                                        </DataTemplate>
                                    </ListBox.ItemTemplate>
                                </ListBox>
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
            </DataTemplate>
        </TabControl.ContentTemplate>
    </TabControl>
</Window>
```

Note the two `RelativeSource={RelativeSource AncestorType=TabControl}` bindings ("열기" button, tab-close "×" button) — these are the ONLY two places that need to reach up from per-tab content to the tab-container `MainViewModel`; everything else in the toolbar/sidebar/viewer binds directly to the active `DocumentTabViewModel` exactly as in v1, unchanged. The inner `<TabControl Grid.Column="0">` (bookmarks/thumbnails sidebar) is unrelated to the OUTER new tab-strip `TabControl` — same control type, different purpose, already existed in v1.

- [ ] **Step 4: Update MainWindow.xaml.cs for per-tab PasswordRequired subscription and tab-scoped bookmark/thumbnail navigation**

Replace the entire contents of `src/DalView/MainWindow.xaml.cs`:

```csharp
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DalView.ViewModels;
using PdfiumViewer.Core;

namespace DalView;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel vm)
        {
            vm.Tabs.CollectionChanged += OnTabsCollectionChanged;
        }
    }

    public void OpenFileFromExternalRequest(string path)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenPathAsNewTab(path);
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    private void OnTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (DocumentTabViewModel tab in e.NewItems)
            {
                tab.PasswordRequired += OnPasswordRequired;
            }
        }
    }

    private void BookmarkItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: PdfBookmark bookmark } item
            && FindTabViewModel(item) is DocumentTabViewModel vm)
        {
            vm.Page = bookmark.PageIndex;
        }
        e.Handled = true;
    }

    private void ThumbnailImage_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThumbnailItem item })
        {
            item.EnsureLoaded();
        }
    }

    private void ThumbnailRow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThumbnailItem item } row
            && FindTabViewModel(row) is DocumentTabViewModel vm)
        {
            vm.Page = item.PageIndex;
        }
    }

    private void OnPasswordRequired(object? sender, string path)
    {
        var dialog = new PasswordDialog { Owner = this };
        if (dialog.ShowDialog() == true && sender is DocumentTabViewModel vm)
        {
            vm.OpenPath(path, dialog.Password);
        }
    }

    /// <summary>
    /// Walks up the visual tree from <paramref name="element"/> to find the nearest ancestor
    /// whose DataContext is a DocumentTabViewModel. Needed because, with tabs, a Window's own
    /// DataContext is the tab-container MainViewModel, not the active tab — event handlers that
    /// need the active tab's state (e.g. to set Page) must resolve it via the visual tree instead.
    /// </summary>
    private static DocumentTabViewModel? FindTabViewModel(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is FrameworkElement { DataContext: DocumentTabViewModel vm })
            {
                return vm;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
```

- [ ] **Step 5: Write a test proving tabs are independent**

Create `tests/DalView.Tests/MainViewModelTabTests.cs`:

```csharp
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
            Assert.Equal(2, tabA.PageCount);
            Assert.Equal(2, tabB.PageCount);

            // Changing tab A's page must not affect tab B.
            tabA.Page = 1;
            Assert.Equal(0, tabB.Page);
        }
        finally
        {
            File.Delete(pathA);
            File.Delete(pathB);
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
        }
        finally
        {
            File.Delete(pathA);
            File.Delete(pathB);
        }
    }
}
```

`CloseTabCommand.Execute(tabA)` deliberately does NOT test closing the LAST tab — that path calls `Application.Current?.Shutdown()`, which is a real app-lifetime side effect not meaningful to exercise from a unit test; the null-conditional in Step 2 is what keeps it safe to call in a test host at all (it becomes a no-op there).

- [ ] **Step 6: Run the full test suite**

Run: `cd "D:/claude/DalView" && "/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" test DalView.sln`
Expected: all tests pass (19 existing + 2 new = 21), 0 build warnings/errors.

- [ ] **Step 7: Build and manually verify tabs work**

Run: `"/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" build DalView.sln`
Expected: 0 errors.

Run: `"/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" run --project src/DalView/DalView.csproj`
Expected: open a PDF via "열기" — it opens in a tab. Click "열기" again and open a second, different PDF — a second tab appears and becomes active; the toolbar/sidebar/viewer now show the second document. Click back to the first tab — it shows the first document's own page/zoom/bookmarks/thumbnails, unaffected by anything done in the second tab. Click a tab's "×" — that tab closes; if it was the last tab, the app exits.

- [ ] **Step 8: Commit**

```bash
cd "D:/claude/DalView"
git add src/DalView/ViewModels/DocumentTabViewModel.cs src/DalView/ViewModels/MainViewModel.cs src/DalView/MainWindow.xaml src/DalView/MainWindow.xaml.cs tests/DalView.Tests/MainViewModelTabTests.cs
git commit -m "Split MainViewModel into per-tab DocumentTabViewModel; add tabbed UI"
```

---

### Task 3: Printing

**Files:**
- Modify: `src/DalView/ViewModels/DocumentTabViewModel.cs`
- Modify: `src/DalView/MainWindow.xaml`
- Modify: `src/DalView/DalView.csproj`

**Interfaces:**
- Consumes: `IPdfDocument.CreatePrintDocument() : System.Drawing.Printing.PrintDocument` (already part of the `PdfiumViewer.Net.WPF` 3.0.4 API surface used by v1, just not called yet).
- Produces: `DocumentTabViewModel.PrintCommand` — nothing later depends on this; Task 3 and Task 4 are independent of each other (both depend only on Task 2).

- [ ] **Step 1: Enable Windows Forms in the project, without letting it change any existing type resolution**

`System.Windows.Forms.PrintDialog` is the simplest way to get a native printer/page-range picker backed by a `System.Drawing.Printing.PrintDocument` (which is what `CreatePrintDocument()` returns) — WPF has no equivalent dialog of its own for a `PrintDocument`. Add to `src/DalView/DalView.csproj`, inside the existing `<PropertyGroup>`:

```xml
    <UseWindowsForms>true</UseWindowsForms>
```

**Important — verified empirically before writing this plan:** enabling `UseWindowsForms` alongside the existing `UseWPF` causes the SDK to add `global using global::System.Windows.Forms;` (and `global using global::System.Drawing;`) as *implicit* global usings across the whole project. Left in place, this creates ambiguous-reference compile errors wherever existing code uses a type name that exists in both `System.Windows`/`System.Drawing.Common` and `System.Windows.Forms` unqualified (e.g. `MessageBox` in `App.xaml.cs`, `Application` in `MainViewModel.cs`). To keep every other file's compilation behavior completely unchanged, strip both implicit usings back out — add a second `<ItemGroup>` to the same csproj:

```xml
  <ItemGroup>
    <Using Remove="System.Windows.Forms" />
    <Using Remove="System.Drawing" />
  </ItemGroup>
```

(Verified: with this `Using Remove` in place, `System.Windows.Forms` and `System.Drawing` types are only available where explicitly/fully qualified — which is exactly what Step 2 does — and no other file's implicit type resolution changes at all.)

- [ ] **Step 2: Add PrintCommand to DocumentTabViewModel**

Add to `src/DalView/ViewModels/DocumentTabViewModel.cs`, inside the `DocumentTabViewModel` class (alongside the other `[RelayCommand]` methods):

```csharp
    [RelayCommand]
    private void Print()
    {
        if (Document == null) return;

        using var printDocument = Document.CreatePrintDocument();
        using var dialog = new System.Windows.Forms.PrintDialog { Document = printDocument };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            printDocument.Print();
        }
    }
```

Fully-qualifying `System.Windows.Forms.PrintDialog`/`System.Windows.Forms.DialogResult` here (rather than adding a file-level `using System.Windows.Forms;`) keeps this the only place in the codebase that references WinForms types at all, consistent with Step 1's `Using Remove`.

- [ ] **Step 3: Add a "인쇄" button to the toolbar**

In `src/DalView/MainWindow.xaml`, inside the `ToolBar` (within `TabControl.ContentTemplate`, from Task 2 Step 3), add after the last `Button Content="▶"` element:

```xml
                        <Separator />
                        <Button Content="인쇄" Command="{Binding PrintCommand}" Padding="8,2" />
```

`PrintCommand` binds directly to the active `DocumentTabViewModel` (no `RelativeSource` hop needed — it lives on the per-tab view model, unlike `OpenFileCommand`).

- [ ] **Step 4: Build and verify**

Run: `cd "D:/claude/DalView" && "/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" build DalView.sln`
Expected: 0 errors, 0 warnings. If any ambiguous-reference errors appear (CS0104), they mean Step 1's `Using Remove` block is missing or misplaced — re-check it's actually in the csproj.

Run: `"/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" test DalView.sln`
Expected: all 21 tests still pass (printing has no automated test — it opens a real native dialog, not something to script).

Run: `"/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" run --project src/DalView/DalView.csproj`
Expected: open a real PDF, click "인쇄" — the Windows print dialog appears with a printer/page-range picker, showing the correct total page count. Clicking Cancel does not crash the app. (Actually printing to a physical/virtual printer, e.g. "Microsoft Print to PDF", is worth doing once if a printer is available, to confirm `printDocument.Print()` doesn't throw — but confirming the dialog opens correctly with the right page count is the main thing this step needs to prove.)

- [ ] **Step 5: Commit**

```bash
cd "D:/claude/DalView"
git add src/DalView/ViewModels/DocumentTabViewModel.cs src/DalView/MainWindow.xaml src/DalView/DalView.csproj
git commit -m "Add printing via Windows print dialog"
```

---

### Task 4: File association (single instance + open-as-new-tab)

**Files:**
- Modify: `src/DalView/App.xaml`
- Modify: `src/DalView/App.xaml.cs`
- Modify: `src/DalView/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `MainViewModel.OpenPathAsNewTab(string) : void` (Task 2) — the pipe-receiving instance calls this via `MainWindow.OpenFileFromExternalRequest`.
- Produces: `MainWindow.OpenFileFromExternalRequest(string path) : void` — already added in Task 2 Step 4 for use by the tab-opening flow in general; this task is what actually calls it from an external-process handoff.

This task reuses VideoPlayer's (`D:\claude\VideoPlayer\App.xaml.cs`) proven named-mutex + named-pipe single-instance pattern, adapted to DalView's names and to open a new tab instead of loading a playlist.

- [ ] **Step 1: Remove StartupUri so App has full manual control over window creation**

In `src/DalView/App.xaml`, remove the `StartupUri="MainWindow.xaml"` attribute from the `<Application>` root element (keep everything else). This matters because `OnStartup` (Step 2) must be able to decide NOT to create/show a window at all when a second instance immediately hands off and exits — `StartupUri` would create one unconditionally.

`src/DalView/App.xaml` after this change:

```xml
<Application x:Class="DalView.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</Application>
```

- [ ] **Step 2: Add single-instance mutex + named-pipe server/client to App.xaml.cs**

Replace the entire contents of `src/DalView/App.xaml.cs`:

```csharp
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DalView;

public partial class App : Application
{
    private const string MutexName = "DalView_SingleInstance_Mutex";
    private const string PipeName = "DalView_OpenFile_Pipe";

    private Mutex? _mutex;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var filePath = e.Args.Length > 0 ? e.Args[0] : null;

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            if (filePath != null)
            {
                SendFileToRunningInstance(filePath);
            }
            Shutdown();
            return;
        }

        StartPipeServer();

        _mainWindow = new MainWindow();
        _mainWindow.Show();

        if (filePath != null)
        {
            _mainWindow.OpenFileFromExternalRequest(filePath);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"예상치 못한 오류가 발생했습니다:\n{e.Exception.Message}", "달뷰", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void StartPipeServer()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var path = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(path))
                    {
                        Dispatcher.Invoke(() => _mainWindow?.OpenFileFromExternalRequest(path));
                    }
                }
                catch
                {
                    // Pipe faulted or was torn down mid-connection; loop and open a fresh one.
                }
            }
        });
    }

    private static void SendFileToRunningInstance(string filePath)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(filePath);
        }
        catch
        {
            // Best-effort handoff: if the running instance can't be reached, give up quietly
            // rather than surfacing an error for what the user experiences as "opening a file".
        }
    }
}
```

This mirrors `D:\claude\VideoPlayer\App.xaml.cs`'s `OnStartup`/`OnExit`/`StartPipeServer`/`SendFileToRunningInstance` structure with DalView's own mutex/pipe names, `MainWindow()` construction (no constructor argument, unlike VideoPlayer's `MainWindow(filePath)` — DalView's `MainWindow` has a parameterless constructor; the file-open-on-launch call happens via `OpenFileFromExternalRequest` right after `Show()` instead), and calls `OpenFileFromExternalRequest` (Task 2's method) instead of VideoPlayer's `LoadFolderAndPlay`.

`src/DalView/MainWindow.xaml.cs`'s `OpenFileFromExternalRequest` (added in Task 2 Step 4) already does the right thing here unmodified — no changes needed to that file in this task.

- [ ] **Step 3: Build and verify**

Run: `cd "D:/claude/DalView" && "/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" build DalView.sln`
Expected: 0 errors.

Run: `"/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" test DalView.sln`
Expected: all 21 tests still pass (this task adds no new unit tests — cross-process named-pipe IPC needs two real running processes to exercise meaningfully, which is a manual-verification concern below, not something to unit-test).

- [ ] **Step 4: Manually verify single-instance + new-tab handoff with two real processes**

Build first: `"/c/Users/hyun/AppData/Local/Microsoft/dotnet/dotnet.exe" build DalView.sln -c Debug`

Find the built exe (e.g. `src/DalView/bin/Debug/net8.0-windows/win-x64/DalView.exe`) and two different real PDF files on this machine (or build fresh ones via a quick script using `MinimalPdfBuilder`-equivalent content, or just reuse whichever real PDFs earlier verification steps in this project found, e.g. under `C:\Users\hyun\Downloads\...`).

1. Launch the exe directly with the first PDF's path as an argument (simulating a file-association double-click): `& "<path-to>\DalView.exe" "<path-to-pdf-A>"`. Expected: the app opens with PDF A already loaded in the first tab.
2. While that's still running, launch the exe again with a DIFFERENT PDF's path: `& "<path-to>\DalView.exe" "<path-to-pdf-B>"`. Expected: this second process exits almost immediately (check via `Get-Process DalView` — should still show only ONE `DalView` process after a moment, not two) and the FIRST instance's window comes to the foreground with a NEW SECOND TAB open showing PDF B, while the first tab (PDF A) is untouched.
3. Close the app (close both tabs, or close the window) and confirm no lingering `DalView.exe` process remains (`Get-Process DalView -ErrorAction SilentlyContinue` returns nothing) — confirms `OnExit`'s mutex release isn't leaving anything stuck.

Report exactly what was observed at each numbered step — this is the same kind of "must actually run two real processes and read real process state" verification this project's earlier work required, not something to infer from reading the code.

- [ ] **Step 5: Commit**

```bash
cd "D:/claude/DalView"
git add src/DalView/App.xaml src/DalView/App.xaml.cs
git commit -m "Add single-instance file association (named pipe forwards to a new tab)"
```

---

## Self-Review Notes

- **Spec coverage:** icon → Task 1; tabs with independent per-tab state → Task 2; printing → Task 3; single-instance + new-tab file handoff → Task 4; registry auto-registration explicitly and correctly left out per the spec's stated scope.
- **Type consistency check:** `DocumentTabViewModel` (Task 2) is referenced identically by its exact name/members across Tasks 2, 3, and the `FindTabViewModel` helper; `MainViewModel.OpenPathAsNewTab(string)` (Task 2) is called unchanged by `MainWindow.OpenFileFromExternalRequest` (also Task 2) and exercised end-to-end by Task 4's pipe receiver without modification; `PrintCommand` (Task 3) matches its XAML binding.
- **Verified, not assumed:** the `UseWindowsForms`+`UseWPF` implicit-global-using collision risk in Task 3 was empirically tested on this machine before writing the plan (a scratch csproj build showed `global using global::System.Windows.Forms;` and `global using global::System.Drawing;` get added, and confirmed `<Using Remove>` cleanly removes them) — this was not guessed.
- **Placeholder scan:** no TBD/TODO markers; every step has literal code, exact file paths, or exact commands.
