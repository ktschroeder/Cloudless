using System.Windows;

namespace Cloudless;

public partial class MainWindow
{
    internal bool IsFileBookmarked(string? filePath) =>
        !string.IsNullOrWhiteSpace(filePath) && bookmarkManager?.IsBookmarked(filePath) == true;

    internal void EditBookmarksForFiles(IReadOnlyList<string> filePaths, bool add)
    {
        if (bookmarkManager == null)
            return;

        string[] selectedFiles = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        int changedFiles = 0;

        foreach (string filePath in selectedFiles)
        {
            bool isBookmarked = bookmarkManager.IsBookmarked(filePath);
            if (add && !isBookmarked)
            {
                bookmarkManager.AddBookmark(filePath);
                changedFiles++;
            }
            else if (!add && isBookmarked)
            {
                bookmarkManager.RemoveBookmark(filePath);
                changedFiles++;
            }
        }

        if (changedFiles == 0)
            Message(add ? "No bookmarks added; all selected items are already bookmarked." : "No bookmarks removed; none of the selected items are bookmarked.");
        else
            Message($"{(add ? "Bookmarked" : "Removed bookmarks from")} {changedFiles} of {selectedFiles.Length} selected item(s).");

        RefreshAllBookmarkIndicators();
        _ = UpdateBookmarksMenu();
    }

    private void RefreshAllBookmarkIndicators()
    {
        if (Application.Current == null)
            return;

        foreach (MainWindow mainWindow in Application.Current.Windows.OfType<MainWindow>())
        {
            if (mainWindow._filmStripWindow != null)
                mainWindow._filmStripWindow.RefreshBookmarkIndicators(mainWindow.IsFileBookmarked);

            foreach (GalleryWindow gallery in Application.Current.Windows.OfType<GalleryWindow>()
                         .Where(window => ReferenceEquals(window.Owner, mainWindow)))
                gallery.RefreshBookmarkIndicators(mainWindow.IsFileBookmarked);
        }
    }
}
