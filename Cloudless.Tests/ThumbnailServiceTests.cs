using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
public sealed class ThumbnailServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetThumbnailAsync_ReturnsNullForBlankOrMissingPath(string? path)
    {
        var result = await ThumbnailService.GetThumbnailAsync(path!, 80, 60);

        Assert.Null(result);
    }

    [Fact]
    public void GetThumbnailAsync_ReturnsFrozenThumbnailWithinRequestedBounds()
    {
        RunOnStaThread(() =>
        {
            var testDirectory = Path.Combine(Path.GetTempPath(), "Cloudless.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            var sourcePath = Path.Combine(testDirectory, "source.jpg");
            var assetPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "img1-landscape.jpg");
            File.Copy(assetPath, sourcePath);
            const int maxWidth = 80;
            const int maxHeight = 60;
            var cachePath = GetCachePath(sourcePath, maxWidth, maxHeight);

            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                try
                {
                    var thumbnail = WaitForDispatcherTask(
                        ThumbnailService.GetThumbnailAsync(sourcePath, maxWidth, maxHeight),
                        dispatcher,
                        "create a shell thumbnail");

                    Assert.NotNull(thumbnail);
                    Assert.True(thumbnail!.IsFrozen);
                    Assert.InRange(thumbnail.PixelWidth, 1, maxWidth);
                    Assert.InRange(thumbnail.PixelHeight, 1, maxHeight);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
            finally
            {
                if (File.Exists(cachePath))
                    File.Delete(cachePath);
                if (Directory.Exists(testDirectory))
                    Directory.Delete(testDirectory, recursive: true);
            }
        });
    }

    private static string GetCachePath(string sourcePath, int width, int height)
    {
        var cacheDirectoryField = typeof(ThumbnailService).GetField(
            "cacheDir",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(cacheDirectoryField);
        var cacheDirectory = Assert.IsType<string>(cacheDirectoryField!.GetValue(null));
        var cacheKey = $"{sourcePath.ToLowerInvariant()}|{File.GetLastWriteTimeUtc(sourcePath).Ticks}|{width}x{height}";
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(cacheKey))).ToLowerInvariant();
        return Path.Combine(cacheDirectory, hash + ".png");
    }

    private static BitmapSource? WaitForDispatcherTask(Task<BitmapSource?> task, Dispatcher dispatcher, string operation)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            timeout.Tick += (_, _) => frame.Continue = false;
            task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            timeout.Start();
            Dispatcher.PushFrame(frame);
            timeout.Stop();
        }

        if (!task.IsCompleted)
            throw new TimeoutException($"Timed out waiting to {operation}.");
        return task.GetAwaiter().GetResult();
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            throw new Xunit.Sdk.XunitException($"Thumbnail service test failed: {failure}");
    }
}
