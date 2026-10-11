using System.IO;
using Xunit;

namespace Cloudless.Tests;

public sealed class MediaFileSorterTests
{
    [Fact]
    public void Sort_AppliesDefaultFilenameDateAndSizeOrdersInBothDirections()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"Cloudless-sort-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string img10 = Path.Combine(directory, "image10.gif");
            string img2 = Path.Combine(directory, "image2.png");
            string img1 = Path.Combine(directory, "image1.jpg");
            File.WriteAllBytes(img10, new byte[30]);
            File.WriteAllBytes(img2, new byte[10]);
            File.WriteAllBytes(img1, new byte[20]);

            File.SetCreationTimeUtc(img10, new DateTime(2020, 1, 3, 0, 0, 0, DateTimeKind.Utc));
            File.SetCreationTimeUtc(img2, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetCreationTimeUtc(img1, new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(img10, new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(img2, new DateTime(2020, 2, 3, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(img1, new DateTime(2020, 2, 2, 0, 0, 0, DateTimeKind.Utc));

            string[] defaultOrder = { img10, img2, img1 };
            Assert.Equal(defaultOrder, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.Default));
            Assert.Equal(new[] { img1, img2, img10 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.DefaultDescending));
            Assert.Equal(new[] { img1, img2, img10 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.Filename));
            Assert.Equal(new[] { img10, img2, img1 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.FilenameDescending));
            Assert.Equal(new[] { img10, img1, img2 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.Filetype));
            Assert.Equal(new[] { img2, img1, img10 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.FiletypeDescending));
            Assert.Equal(new[] { img2, img1, img10 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.CreatedOn));
            Assert.Equal(new[] { img10, img1, img2 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.CreatedOnDescending));
            Assert.Equal(new[] { img10, img1, img2 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.ModifiedOn));
            Assert.Equal(new[] { img2, img1, img10 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.ModifiedOnDescending));
            Assert.Equal(new[] { img2, img1, img10 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.Size));
            Assert.Equal(new[] { img10, img1, img2 }, MediaFileSorter.Sort(defaultOrder, path => path, MediaSortOrder.SizeDescending));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
