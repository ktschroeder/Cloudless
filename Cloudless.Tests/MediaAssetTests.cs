using System.IO;
using System.Windows.Media.Imaging;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
public sealed class MediaAssetTests
{
    [Theory]
    [InlineData("img1-landscape.jpg", 640, 427)]
    [InlineData("img2-portrait.jpg", 640, 963)]
    [InlineData("img3-portrait.jpg", 640, 960)]
    [InlineData("img4-portrait.jpg", 640, 963)]
    public void CheckedInJpegAssets_DecodeWithExpectedPixelDimensions(
        string assetName,
        int expectedWidth,
        int expectedHeight)
    {
        RunOnStaThread(() =>
        {
            using var stream = File.OpenRead(GetAssetPath(assetName));
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            Assert.IsType<JpegBitmapDecoder>(decoder);
            var frame = Assert.Single(decoder.Frames);
            Assert.Equal(expectedWidth, frame.PixelWidth);
            Assert.Equal(expectedHeight, frame.PixelHeight);
        });
    }

    [Theory]
    [InlineData("img1-landscape.jpg")]
    [InlineData("img2-portrait.jpg")]
    [InlineData("img3-portrait.jpg")]
    [InlineData("img4-portrait.jpg")]
    [InlineData("vid1-landscape.mp4")]
    [InlineData("vid2-landscape.mp4")]
    [InlineData("vid3-portrait.mp4")]
    [InlineData("vid4-portrait.mp4")]
    [InlineData("vid5-portrait.mp4")]
    [InlineData("vid6-portrait.mp4")]
    [Trait("Category", "WindowsIntegration")]
    public void ShellThumbnailExtractor_CreatesADecodableThumbnail(string assetName)
    {
        RunOnStaThread(() =>
        {
            var outputDirectory = Path.Combine(Path.GetTempPath(), "Cloudless.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDirectory);
            var thumbnailPath = Path.Combine(outputDirectory, "thumbnail.png");

            try
            {
                Assert.True(ShellThumbnailExtractor.TrySaveShellThumbnail(
                    GetAssetPath(assetName),
                    thumbnailPath,
                    160,
                    160));

                using var stream = File.OpenRead(thumbnailPath);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = Assert.Single(decoder.Frames);
                Assert.InRange(frame.PixelWidth, 1, 160);
                Assert.InRange(frame.PixelHeight, 1, 160);
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                    Directory.Delete(outputDirectory, recursive: true);
            }
        });
    }

    private static string GetAssetPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", name);

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            throw new Xunit.Sdk.XunitException($"Media asset test failed: {failure}");
    }
}
