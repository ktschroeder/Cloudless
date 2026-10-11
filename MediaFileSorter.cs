using System.IO;
using System.Runtime.InteropServices;

namespace Cloudless;

internal enum MediaSortOrder
{
    Default,
    DefaultDescending,
    Filename,
    FilenameDescending,
    Filetype,
    FiletypeDescending,
    CreatedOn,
    CreatedOnDescending,
    ModifiedOn,
    ModifiedOnDescending,
    Size,
    SizeDescending
}

internal static class MediaFileSorter
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string x, string y);

    public static T[] Sort<T>(IEnumerable<T> defaultItems, Func<T, string?> getPath, MediaSortOrder sortOrder)
    {
        T[] items = defaultItems.ToArray();
        if (sortOrder == MediaSortOrder.Default)
            return items;
        if (sortOrder == MediaSortOrder.DefaultDescending)
            return items.Reverse().ToArray();

        return sortOrder switch
        {
            MediaSortOrder.Filename => items.OrderBy(item => Path.GetFileName(getPath(item)), NaturalFileNameComparer.Instance).ToArray(),
            MediaSortOrder.FilenameDescending => items.OrderByDescending(item => Path.GetFileName(getPath(item)), NaturalFileNameComparer.Instance).ToArray(),
            MediaSortOrder.Filetype => items.OrderBy(item => Path.GetExtension(getPath(item)), StringComparer.OrdinalIgnoreCase).ToArray(),
            MediaSortOrder.FiletypeDescending => items.OrderByDescending(item => Path.GetExtension(getPath(item)), StringComparer.OrdinalIgnoreCase).ToArray(),
            MediaSortOrder.CreatedOn => items.OrderBy(item => new FileInfo(getPath(item)!).CreationTimeUtc).ToArray(),
            MediaSortOrder.CreatedOnDescending => items.OrderByDescending(item => new FileInfo(getPath(item)!).CreationTimeUtc).ToArray(),
            MediaSortOrder.ModifiedOn => items.OrderBy(item => new FileInfo(getPath(item)!).LastWriteTimeUtc).ToArray(),
            MediaSortOrder.ModifiedOnDescending => items.OrderByDescending(item => new FileInfo(getPath(item)!).LastWriteTimeUtc).ToArray(),
            MediaSortOrder.Size => items.OrderBy(item => new FileInfo(getPath(item)!).Length).ToArray(),
            MediaSortOrder.SizeDescending => items.OrderByDescending(item => new FileInfo(getPath(item)!).Length).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, "Unknown media sort order.")
        };
    }

    private sealed class NaturalFileNameComparer : IComparer<string?>
    {
        public static NaturalFileNameComparer Instance { get; } = new();

        public int Compare(string? x, string? y) => StrCmpLogicalW(x ?? string.Empty, y ?? string.Empty);
    }
}
