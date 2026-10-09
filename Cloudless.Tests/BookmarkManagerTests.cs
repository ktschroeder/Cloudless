using System.IO;
using System.Text.Json;
using Xunit;

namespace Cloudless.Tests;

public sealed class BookmarkManagerTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Cloudless.Tests", Guid.NewGuid().ToString("N"));
    private string BookmarksPath => Path.Combine(_testDirectory, "bookmarks.json");

    [Fact]
    public void AddBookmark_PersistsOrderAndMovesDuplicatesToTheEnd()
    {
        var manager = new BookmarkManager(BookmarksPath);
        manager.AddBookmark("first.jpg");
        manager.AddBookmark("second.jpg");
        manager.AddBookmark("first.jpg");

        var reloadedManager = new BookmarkManager(BookmarksPath);

        Assert.Equal(new[] { "second.jpg", "first.jpg" }, reloadedManager.GetBookmarks());
        Assert.True(reloadedManager.IsBookmarked("first.jpg"));
        Assert.False(reloadedManager.IsBookmarked("missing.jpg"));
    }

    [Fact]
    public void RemoveBookmark_UpdatesPersistedBookmarks()
    {
        var manager = new BookmarkManager(BookmarksPath);
        manager.AddBookmark("first.jpg");
        manager.AddBookmark("second.jpg");
        manager.RemoveBookmark("first.jpg");
        manager.RemoveBookmark("missing.jpg");

        var reloadedManager = new BookmarkManager(BookmarksPath);

        Assert.Equal(new[] { "second.jpg" }, reloadedManager.GetBookmarks());
    }

    [Fact]
    public void ClearBookmarks_PersistsAnEmptyList()
    {
        var manager = new BookmarkManager(BookmarksPath);
        manager.AddBookmark("first.jpg");
        manager.AddBookmark("second.jpg");
        manager.ClearBookmarks();

        var reloadedManager = new BookmarkManager(BookmarksPath);

        Assert.Empty(reloadedManager.GetBookmarks());
    }

    [Fact]
    public void GetBookmarks_ReloadsChangesWrittenByAnotherManager()
    {
        var firstManager = new BookmarkManager(BookmarksPath);
        var secondManager = new BookmarkManager(BookmarksPath);

        firstManager.AddBookmark("first.jpg");
        secondManager.AddBookmark("second.jpg");

        Assert.Equal(new[] { "first.jpg", "second.jpg" }, firstManager.GetBookmarks());
    }

    [Fact]
    public void AddAndRemoveBookmark_IgnoreBlankPaths()
    {
        var manager = new BookmarkManager(BookmarksPath);
        manager.AddBookmark(null!);
        manager.AddBookmark(string.Empty);
        manager.RemoveBookmark(" ");

        Assert.Empty(manager.GetBookmarks());
        Assert.False(File.Exists(BookmarksPath));
    }

    [Fact]
    public void GetBookmarks_RejectsMalformedJsonInsteadOfReturningPartialData()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(BookmarksPath, "{\"bookmarks\":[");
        var manager = new BookmarkManager(BookmarksPath);

        Assert.Throws<JsonException>(() => manager.GetBookmarks());
    }

    [Fact]
    public void GetBookmarks_TreatsJsonNullAsAnEmptyCollection()
    {
        Directory.CreateDirectory(_testDirectory);
        File.WriteAllText(BookmarksPath, "null");
        var manager = new BookmarkManager(BookmarksPath);

        Assert.Empty(manager.GetBookmarks());
    }

    [Fact]
    public void GetBookmarks_ReturnsAnIndependentListSnapshot()
    {
        var manager = new BookmarkManager(BookmarksPath);
        manager.AddBookmark("first.jpg");
        var snapshot = manager.GetBookmarks();

        snapshot.Clear();

        Assert.Equal(new[] { "first.jpg" }, manager.GetBookmarks());
    }

    [Fact]
    public void IsBookmarked_ReloadsChangesWrittenByAnotherManager()
    {
        var firstManager = new BookmarkManager(BookmarksPath);
        var secondManager = new BookmarkManager(BookmarksPath);
        secondManager.AddBookmark("shared.jpg");

        Assert.True(firstManager.IsBookmarked("shared.jpg"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }
}
