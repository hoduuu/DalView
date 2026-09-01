using System;
using System.IO;
using DalView.Services;
using DalView.Tests.TestFixtures;
using DalView.ViewModels;
using Xunit;

namespace DalView.Tests;

/// <summary>
/// End-to-end integration tests for search functionality.
/// These tests exercise real PDFium document loading and search against actual multi-page PDFs.
/// </summary>
public class MainViewModelSearchTests
{
    [Fact]
    public async Task Search_FindsMultipleMatches_AcrossPages()
    {
        // Arrange
        var pdfBytes = MinimalPdfBuilder.Build(
            "Test Page One with searchable content",
            "Test Page Two with more content"
        );
        var pdfPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        try
        {
            var viewModel = new MainViewModel();
            viewModel.OpenPath(pdfPath, null);

            // Verify PDF loaded (MinimalPdfBuilder creates valid PDF structure)
            Assert.NotNull(viewModel.Document);

            // Act: Perform search for term that appears on both pages
            viewModel.SearchText = "Test";
            await viewModel.SearchCommand.ExecuteAsync(null);

            // Assert: Search should find 2 matches (one per page) - strict assertions to catch broken search
            Assert.NotNull(viewModel.Matches);
            Assert.Equal(2, viewModel.Matches.Items.Count);
            Assert.Equal(0, viewModel.MatchIndex);
            Assert.NotNull(viewModel.StatusMessage);
            Assert.Contains("2건 찾음", viewModel.StatusMessage);

            // Cleanup
            viewModel.Document?.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        finally
        {
            if (File.Exists(pdfPath))
            {
                try
                {
                    File.Delete(pdfPath);
                }
                catch (IOException)
                {
                    // File may still be locked by PDFium, it will be cleaned up by temp folder
                }
            }
        }
    }

    [Fact]
    public async Task NextMatchCommand_NavigatesBetweenMatches()
    {
        // Arrange
        var pdfBytes = MinimalPdfBuilder.Build(
            "Test First Match on Page One",
            "Test Second Match on Page Two"
        );
        var pdfPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        try
        {
            var viewModel = new MainViewModel();
            viewModel.OpenPath(pdfPath, null);

            // Act: Search and navigate
            viewModel.SearchText = "Test";
            await viewModel.SearchCommand.ExecuteAsync(null);

            Assert.Equal(0, viewModel.MatchIndex);
            var firstMatchPage = viewModel.Matches!.Items[0].Page;

            // Act: Click next button
            viewModel.NextMatchCommand.Execute(null);

            // Assert: Match index advanced
            Assert.Equal(1, viewModel.MatchIndex);
            var secondMatchPage = viewModel.Matches!.Items[1].Page;
            Assert.NotEqual(firstMatchPage, secondMatchPage); // Matches on different pages

            // Cleanup
            viewModel.Document?.Dispose();
        }
        finally
        {
            if (File.Exists(pdfPath))
            {
                try
                {
                    File.Delete(pdfPath);
                }
                catch (IOException)
                {
                    // File may still be locked by PDFium, it will be cleaned up by temp folder
                }
            }
        }
    }

    [Fact]
    public async Task NextMatchCommand_WrapsAroundToFirstMatch()
    {
        // Arrange
        var pdfBytes = MinimalPdfBuilder.Build(
            "Test Word on Page One",
            "Test Word on Page Two"
        );
        var pdfPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        try
        {
            var viewModel = new MainViewModel();
            viewModel.OpenPath(pdfPath, null);

            // Act: Search
            viewModel.SearchText = "Test";
            await viewModel.SearchCommand.ExecuteAsync(null);

            var matchCount = viewModel.Matches!.Items.Count;
            Assert.True(matchCount >= 2);

            // Navigate through all matches
            for (int i = 0; i < matchCount; i++)
            {
                viewModel.NextMatchCommand.Execute(null);
            }

            // Assert: Should wrap back to first match
            Assert.Equal(0, viewModel.MatchIndex);

            // Cleanup
            viewModel.Document?.Dispose();
        }
        finally
        {
            if (File.Exists(pdfPath))
            {
                try
                {
                    File.Delete(pdfPath);
                }
                catch (IOException)
                {
                    // File may still be locked by PDFium, it will be cleaned up by temp folder
                }
            }
        }
    }

    [Fact]
    public async Task PreviousMatchCommand_NavigatesBackward()
    {
        // Arrange
        var pdfBytes = MinimalPdfBuilder.Build(
            "Test Word on Page One",
            "Test Word on Page Two"
        );
        var pdfPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        try
        {
            var viewModel = new MainViewModel();
            viewModel.OpenPath(pdfPath, null);

            // Act: Search and navigate to second match
            viewModel.SearchText = "Test";
            await viewModel.SearchCommand.ExecuteAsync(null);

            viewModel.NextMatchCommand.Execute(null); // Go to second match
            Assert.Equal(1, viewModel.MatchIndex);

            // Act: Go previous
            viewModel.PreviousMatchCommand.Execute(null);

            // Assert: Should go back to first match
            Assert.Equal(0, viewModel.MatchIndex);

            // Cleanup
            viewModel.Document?.Dispose();
        }
        finally
        {
            if (File.Exists(pdfPath))
            {
                try
                {
                    File.Delete(pdfPath);
                }
                catch (IOException)
                {
                    // File may still be locked by PDFium, it will be cleaned up by temp folder
                }
            }
        }
    }

    [Fact]
    public async Task PreviousMatchCommand_WrapsAroundToLastMatch()
    {
        // Arrange
        var pdfBytes = MinimalPdfBuilder.Build(
            "Test Word on Page One",
            "Test Word on Page Two"
        );
        var pdfPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        try
        {
            var viewModel = new MainViewModel();
            viewModel.OpenPath(pdfPath, null);

            // Act: Search
            viewModel.SearchText = "Test";
            await viewModel.SearchCommand.ExecuteAsync(null);

            var matchCount = viewModel.Matches!.Items.Count;
            Assert.True(matchCount >= 2);

            // Verify we're at first match
            Assert.Equal(0, viewModel.MatchIndex);

            // Act: Click previous from first position (should wrap to last)
            viewModel.PreviousMatchCommand.Execute(null);

            // Assert: Should wrap to last match
            Assert.Equal(matchCount - 1, viewModel.MatchIndex);

            // Cleanup
            viewModel.Document?.Dispose();
        }
        finally
        {
            if (File.Exists(pdfPath))
            {
                try
                {
                    File.Delete(pdfPath);
                }
                catch (IOException)
                {
                    // File may still be locked by PDFium, it will be cleaned up by temp folder
                }
            }
        }
    }

    [Fact]
    public async Task Search_ShowsNoResultsMessage_WhenNotFound()
    {
        // Arrange
        var pdfBytes = MinimalPdfBuilder.Build(
            "Page One Content",
            "Page Two Content"
        );
        var pdfPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        try
        {
            var viewModel = new MainViewModel();
            viewModel.OpenPath(pdfPath, null);

            // Act: Search for text that doesn't exist
            viewModel.SearchText = "NONEXISTENT_XYZ_SEARCH_TERM";
            await viewModel.SearchCommand.ExecuteAsync(null);

            // Assert
            // When no matches found, Matches may be empty collection rather than null
            if (viewModel.Matches != null)
            {
                Assert.Empty(viewModel.Matches.Items);
            }
            Assert.Equal(-1, viewModel.MatchIndex);
            Assert.NotNull(viewModel.StatusMessage);
            Assert.Contains("없습니다", viewModel.StatusMessage);

            // Cleanup
            viewModel.Document?.Dispose();
        }
        finally
        {
            if (File.Exists(pdfPath))
            {
                try
                {
                    File.Delete(pdfPath);
                }
                catch (IOException)
                {
                    // File may still be locked by PDFium, it will be cleaned up by temp folder
                }
            }
        }
    }

    [Fact]
    public async Task OpeningDifferentDocument_ResetsSearchState_AndRebuildsThumbnails()
    {
        // Arrange: two distinct PDFs
        var pdfABytes = MinimalPdfBuilder.Build(
            "Alpha searchable content here",
            "Alpha second page content"
        );
        var pdfBBytes = MinimalPdfBuilder.Build(
            "Beta page one text",
            "Beta page two text"
        );
        var pdfAPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        var pdfBPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        await File.WriteAllBytesAsync(pdfAPath, pdfABytes);
        await File.WriteAllBytesAsync(pdfBPath, pdfBBytes);

        try
        {
            var viewModel = new MainViewModel();

            // Act: open PDF A and run a search that finds matches
            viewModel.OpenPath(pdfAPath, null);
            viewModel.SearchText = "Alpha";
            await viewModel.SearchCommand.ExecuteAsync(null);

            Assert.NotNull(viewModel.Matches);
            Assert.True(viewModel.Matches.Items.Count > 0);
            Assert.Equal(0, viewModel.MatchIndex);

            // Act: open a different PDF (PDF B) without touching search state directly
            viewModel.OpenPath(pdfBPath, null);

            // Assert: search state from PDF A no longer applies, thumbnails match PDF B's real page count
            Assert.Null(viewModel.Matches);
            Assert.Equal(-1, viewModel.MatchIndex);
            Assert.Equal(2, viewModel.Thumbnails.Count);
            Assert.Equal(viewModel.Document!.PageCount, viewModel.Thumbnails.Count);

            // Cleanup
            viewModel.Document?.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        finally
        {
            foreach (var path in new[] { pdfAPath, pdfBPath })
            {
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                        // File may still be locked by PDFium, it will be cleaned up by temp folder
                    }
                }
            }
        }
    }

    [Fact]
    public async Task Search_ClearsResults_WhenSearchTextIsCleared()
    {
        // Arrange
        var pdfBytes = MinimalPdfBuilder.Build(
            "Test Word on Page One",
            "Test Word on Page Two"
        );
        var pdfPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        try
        {
            var viewModel = new MainViewModel();
            viewModel.OpenPath(pdfPath, null);

            // Act: Search first
            viewModel.SearchText = "Test";
            await viewModel.SearchCommand.ExecuteAsync(null);
            Assert.NotNull(viewModel.Matches);
            Assert.True(viewModel.Matches.Items.Count > 0);

            // Act: Clear search
            viewModel.SearchText = string.Empty;
            await viewModel.SearchCommand.ExecuteAsync(null);

            // Assert: Results should be cleared
            Assert.Null(viewModel.Matches);
            Assert.Equal(-1, viewModel.MatchIndex);

            // Cleanup
            viewModel.Document?.Dispose();
        }
        finally
        {
            if (File.Exists(pdfPath))
            {
                try
                {
                    File.Delete(pdfPath);
                }
                catch (IOException)
                {
                    // File may still be locked by PDFium, it will be cleaned up by temp folder
                }
            }
        }
    }
}
