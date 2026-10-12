using Xunit;

namespace Cloudless.Tests;

public sealed class FilmStripNavigationTests
{
    [Theory]
    [InlineData("a.jpg", -1, "c.jpg")]
    [InlineData("a.jpg", 1, "b.jpg")]
    [InlineData("c.jpg", 1, "a.jpg")]
    [InlineData("c.jpg", -1, "b.jpg")]
    [InlineData("not-listed.jpg", -1, "c.jpg")]
    [InlineData("not-listed.jpg", 1, "a.jpg")]
    public void GetAdjacentPath_NavigatesAndWrapsAcrossFilmstripItems(string currentPath, int direction, string expected)
    {
        string? result = FilmStripNavigation.GetAdjacentPath(
            new[] { "a.jpg", "b.jpg", "c.jpg" },
            currentPath,
            direction);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetAdjacentPath_IsCaseInsensitiveAndHandlesEmptyOrInvalidDirection()
    {
        Assert.Equal("b.jpg", FilmStripNavigation.GetAdjacentPath(new[] { "a.jpg", "b.jpg" }, "A.JPG", 1));
        Assert.Null(FilmStripNavigation.GetAdjacentPath(Array.Empty<string>(), "a.jpg", 1));
        Assert.Null(FilmStripNavigation.GetAdjacentPath(new[] { "a.jpg" }, "a.jpg", 0));
    }
}
