using Xunit;

namespace Cloudless.Tests;

public class PreloadManagerIndexTests
{
    [Theory]
    [InlineData(10, 20, 5, 2, "10,11,12,13,14,15,9,8")]
    [InlineData(0, 20, 5, 2, "0,1,2,3,4,5")]
    [InlineData(18, 20, 5, 2, "18,19,17,16")]
    [InlineData(0, 1, 5, 2, "0")]
    [InlineData(5, 10, 2, 1, "5,6,7,4")]
    [InlineData(5, 10, 0, 0, "5")]
    public void GetPreloadIndices_ClampsLookAheadAndLookBehindToListBounds(
        int currentIndex,
        int itemCount,
        int preloadNext,
        int preloadPrev,
        string expectedIndices)
    {
        var expected = expectedIndices.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);

        var actual = PreloadManager.GetPreloadIndices(currentIndex, itemCount, preloadNext, preloadPrev);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(10, 10)]
    public void GetPreloadIndices_ReturnsNoIndicesForInvalidCurrentIndex(int currentIndex, int itemCount)
    {
        Assert.Empty(PreloadManager.GetPreloadIndices(currentIndex, itemCount));
    }

    [Theory]
    [InlineData(-1, 2, "5,4,3")]
    [InlineData(2, -1, "5,6,7")]
    [InlineData(-1, -1, "5")]
    public void GetPreloadIndices_NegativeLookAroundCountsDisableOnlyTheirDirection(
        int preloadNext,
        int preloadPrev,
        string expectedIndices)
    {
        var expected = expectedIndices.Split(',').Select(int.Parse);
        Assert.Equal(expected, PreloadManager.GetPreloadIndices(5, 10, preloadNext, preloadPrev));
    }

    [Theory]
    [InlineData(15, 40, 10, "5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25")]
    [InlineData(0, 5, 10, "0,1,2,3,4")]
    [InlineData(19, 20, 10, "9,10,11,12,13,14,15,16,17,18,19")]
    [InlineData(4, 10, 0, "4")]
    public void GetCacheRetentionIndices_ClampsRetentionWindowToListBounds(
        int currentIndex,
        int itemCount,
        int radius,
        string expectedIndices)
    {
        var expected = expectedIndices.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);

        var actual = PreloadManager.GetCacheRetentionIndices(currentIndex, itemCount, radius);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(-1, 10, 10)]
    [InlineData(10, 10, 10)]
    [InlineData(0, 0, 10)]
    [InlineData(5, 10, -1)]
    public void GetCacheRetentionIndices_ReturnsNoIndicesForInvalidWindow(
        int currentIndex,
        int itemCount,
        int radius)
    {
        Assert.Empty(PreloadManager.GetCacheRetentionIndices(currentIndex, itemCount, radius));
    }
}
