using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Cloudless
{
    public class BookmarkManager
    {
        private static readonly Mutex bookmarksMutex = new(false, "CloudlessBookmarksMutex");
        private readonly string bookmarksPath;
        private List<string> bookmarks = new();
        private int bookmarksHash = -1;

        public BookmarkManager()
        {
            bookmarksPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Cloudless",
                "bookmarks.json");
        }

        public List<string> GetBookmarks()
        {
            LoadBookmarks();
            return new List<string>(bookmarks);
        }

        public bool IsBookmarked(string filePath)
        {
            LoadBookmarks();
            return bookmarks.Contains(filePath);
        }

        public void AddBookmark(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            LoadBookmarks();

            // Avoid duplicates - move to the end if already exists
            bookmarks.Remove(filePath);
            bookmarks.Add(filePath);

            SaveBookmarks();
        }

        public void RemoveBookmark(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            LoadBookmarks();
            bookmarks.Remove(filePath);
            SaveBookmarks();
        }

        public void ClearBookmarks()
        {
            bookmarks.Clear();
            SaveBookmarks();
        }

        private bool LoadBookmarks()
        {
            if (bookmarksMutex.WaitOne(2000))
            {
                try
                {
                    if (File.Exists(bookmarksPath))
                    {
                        string json = File.ReadAllText(bookmarksPath);
                        int newHash = json.GetHashCode();
                        if (newHash == bookmarksHash)
                        {
                            return false;
                        }

                        bookmarks = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                        bookmarksHash = newHash;
                    }
                    else
                    {
                        if (bookmarksHash == 0)
                            return false;
                        bookmarks = new List<string>();
                        bookmarksHash = 0;
                    }

                    return true;
                }
                finally
                {
                    bookmarksMutex.ReleaseMutex();
                }
            }
            else
            {
                return false;
            }
        }

        private void SaveBookmarks()
        {
            if (bookmarksMutex.WaitOne(2000))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(bookmarksPath) ?? "");
                    File.WriteAllText(bookmarksPath, JsonSerializer.Serialize(bookmarks));
                }
                finally
                {
                    bookmarksMutex.ReleaseMutex();
                }
            }
        }
    }
}
