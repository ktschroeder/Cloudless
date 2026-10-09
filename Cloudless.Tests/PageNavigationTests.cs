using Xunit;

namespace Cloudless.Tests;

public class PageNavigationTests
{
    [Theory]
    [InlineData("na", 4, "7,2,7", 7)]
    [InlineData("na", 7, "7,2", 2)]
    [InlineData("na", 1, "7,2", 2)]
    [InlineData("pa", 4, "7,2,7", 2)]
    [InlineData("pa", 2, "2,7", 7)]
    [InlineData("pa", 8, "2,7", 7)]
    [InlineData("ni", 3, "5,1,4", 4)]
    [InlineData("ni", 5, "5,1", 1)]
    [InlineData("pi", 4, "5,1,3", 3)]
    [InlineData("pi", 1, "5,3", 5)]
    [InlineData("na", 5, "0,10", 10)]
    [InlineData("na", 10, "0,5", 0)]
    [InlineData("pa", 1, "0,5", 0)]
    [InlineData("unknown", 1, "1,2", null)]
    [InlineData("na", 1, "", null)]
    public void ResolveSpecialPageToken_SelectsNextOrPreviousCandidateWithWraparound(
        string token,
        int currentPage,
        string candidates,
        int? expectedPage)
    {
        var candidatePages = candidates.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);

        var resolvedPage = MainWindow.ResolveSpecialPageToken(token, currentPage, candidatePages);

        Assert.Equal(expectedPage, resolvedPage);
    }

    [Theory]
    [InlineData("2,4,4,21,0", 5, "1,3,5")]
    [InlineData("", 3, "1,2,3")]
    [InlineData("1,2,3", 3, "")]
    [InlineData("1,4", 5, "2,3,5")]
    public void GetInactivePages_ReturnsUnoccupiedPagesWithinConfiguredRange(
        string activePages,
        int maxPageIndex,
        string expectedPages)
    {
        var active = activePages.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);
        var expected = expectedPages.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);

        var inactive = MainWindow.GetInactivePages(active, maxPageIndex);

        Assert.Equal(expected, inactive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetInactivePages_ReturnsEmptyForNonPositiveMaximum(int maxPageIndex)
    {
        Assert.Empty(MainWindow.GetInactivePages(new[] { 1, 2 }, maxPageIndex));
    }
}
