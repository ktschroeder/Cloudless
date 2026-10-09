using Xunit;

namespace Cloudless.Tests;

public sealed class ThumbnailOverlayTests
{
    [Theory]
    [InlineData("clip.mp4", true)]
    [InlineData("C:\\media\\clip.MKV", true)]
    [InlineData("clip.WEBM", true)]
    [InlineData("clip.avi", false)]
    [InlineData("clip.mp4.bak", false)]
    [InlineData("C:\\media.mp4\\clip.jpg", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsVlcVideoPath_ClassifiesOnlySupportedVideoExtensions(string? path, bool expected)
    {
        Assert.Equal(expected, VideoThumbnailOverlay.IsVlcVideoPath(path));
    }
}
