using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cloudless.PluginBase;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class WorkspaceUiRoundTripTests
{
    [Fact]
    public void RichWorkspace_RendersSavesReloadsAndAdvancesSlideshowAcrossPages()
    {
        RunOnStaThread(() =>
        {
            ResetWpfApplicationSingletonForTestIsolation();
            App.SuppressStartupForUiTests = true;
            var app = new App();
            app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ThemeManager.ApplyThemeGlobalResources("Dark");

            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            var originalDisplayMode = settings.DisplayMode;
            var originalRecentWorkspaces = settings.RecentWorkspaces?.Cast<string>().ToArray() ?? Array.Empty<string>();
            var recentFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudless", "recent_files.json");
            var originalRecentFiles = File.Exists(recentFilesPath) ? File.ReadAllBytes(recentFilesPath) : null;
            var videoPositionsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudless", "video_positions.json");
            var originalVideoPositions = File.Exists(videoPositionsPath) ? File.ReadAllBytes(videoPositionsPath) : null;
            var workspaceName = "ui-round-trip-" + Guid.NewGuid().ToString("N");
            var workspacePath = Path.Combine(MainWindow.workspaceFilesPath, workspaceName + ".cloudless");
            var mediaWindows = new List<MainWindow>();

            try
            {
                settings.CurrentPage = 1;
                settings.DisplayMode = "BestFit";
                var images = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "TestAssets", "img1-landscape.jpg"),
                    Path.Combine(AppContext.BaseDirectory, "TestAssets", "img2-portrait.jpg"),
                    Path.Combine(AppContext.BaseDirectory, "TestAssets", "img3-portrait.jpg")
                };
                var videos = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "TestAssets", "vid1-landscape.mp4"),
                    Path.Combine(AppContext.BaseDirectory, "TestAssets", "vid2-landscape.mp4"),
                    Path.Combine(AppContext.BaseDirectory, "TestAssets", "vid3-portrait.mp4")
                };

                for (var i = 0; i < 3; i++)
                {
                    OpenMedia(images[i]);
                    OpenMedia(videos[i]);
                }

                var imageWindows = mediaWindows.Where(w => !IsVideo(w)).ToArray();
                var videoWindows = mediaWindows.Where(IsVideo).ToArray();
                CropImage(imageWindows[0]);
                ApplyImageView(imageWindows[0], 1.35, 18, -11);
                ApplyImageView(imageWindows[1], 1.6, -23, 15);
                ApplyImageView(imageWindows[2], 1.2, 9, 21);
                for (var i = 0; i < videoWindows.Length; i++)
                    ConfigureVideo(videoWindows[i], i);

                WaitForDispatcherCondition(() => videoWindows.All(w =>
                {
                    var dimensions = Assert.IsAssignableFrom<IVideoPlayer>(GetVideoHost(w).Content).GetDimensions();
                    return dimensions.IsCompletedSuccessfully && dimensions.Result.HasValue;
                }), mediaWindows[0].Dispatcher, "video dimensions to finish loading");
                for (var i = 0; i < mediaWindows.Count; i++)
                {
                    if (i != 0)
                    {
                        mediaWindows[i].Width = 520;
                        mediaWindows[i].Height = 380;
                    }
                    mediaWindows[i].Left = 80 + (i % 2) * 100;
                    mediaWindows[i].Top = 70 + (i % 2) * 80;
                }
                mediaWindows[2].SendWindowToPage(2);
                mediaWindows[3].SendWindowToPage(2);
                mediaWindows[4].SendWindowToPage(3);
                mediaWindows[5].SendWindowToPage(3);
                videoWindows[0].ExecuteCommand("set trigger 1").GetAwaiter().GetResult();
                videoWindows[1].ExecuteCommand("set trigger 2").GetAwaiter().GetResult();
                videoWindows[2].ExecuteCommand("set trigger 3").GetAwaiter().GetResult();

                AssertImageRenders(imageWindows[0]);
                var saveResult = mediaWindows[0].SaveWorkspace(workspaceName, allowOverwrite: true);
                Assert.Equal(6, saveResult.Item1);
                Assert.Equal(3, saveResult.Item2);
                Assert.Null(saveResult.Item3);

                var saved = Assert.IsType<CloudlessWorkspace>(WorkspacePersistence.Load(workspacePath));
                Assert.Equal(1, saved.CurrentPageIndex);
                Assert.Equal(6, saved.CloudlessWindows.Count);
                Assert.Equal(new[] { 1, 2, 3 }, saved.CloudlessWindows.Select(w => w.PageIndex).Distinct().OrderBy(p => p));
                Assert.All(saved.CloudlessWindows.GroupBy(w => w.PageIndex), page =>
                    Assert.Equal(2, page.Select(w => w.ZOrder).Distinct().Count()));
                var savedStateSummary = string.Join(Environment.NewLine, saved.CloudlessWindows.Select(w => $"page={w.PageIndex}, path={Path.GetFileName(w.ImagePath)}, render={w.RenderWidth}x{w.RenderHeight}, zoom={w.Zoom}, start={w.LoopStartMs}, end={w.LoopEndMs}, flag={w.FlagMs}, trigger={w.SlideshowTriggerCount}, sync={w.IsSynced}"));
                Assert.True(saved.CloudlessWindows.Any(w => w.PageIndex == 1 && Path.GetFileName(w.ImagePath) == "img1-landscape.jpg" && w.Width < 800 && w.Zoom > 1), savedStateSummary);
                Assert.True(saved.CloudlessWindows.Any(w => w.PageIndex == 2 && w.LoopStartMs.HasValue && w.LoopEndMs.HasValue && w.FlagMs.HasValue && w.SlideshowTriggerCount == 2 && w.IsSynced == true), savedStateSummary);
                Assert.True(saved.CloudlessWindows.Any(w => w.PageIndex == 3 && w.LoopStartMs.HasValue && w.LoopEndMs.HasValue && w.FlagMs.HasValue && w.SlideshowTriggerCount == 3 && w.IsSynced == true),
                    savedStateSummary);
                Assert.All(saved.CloudlessWindows, state =>
                {
                    Assert.InRange(state.Left, 0, 500);
                    Assert.InRange(state.Top, 0, 500);
                    Assert.InRange(state.Width, 1, 800);
                    Assert.InRange(state.Height, 1, 800);
                });

                foreach (var window in mediaWindows)
                    window.Close();
                mediaWindows.Clear();

                var loader = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                loader.Show();
                var priorLoadContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(loader.Dispatcher));
                try
                {
                    saved.WorkspaceName = workspaceName;
                    WaitForDispatcherTask(loader.CreateWindowsForWorkspace(saved), loader.Dispatcher, "recreate the saved workspace windows");
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(priorLoadContext);
                }
                loader.Close();

                var restored = Application.Current.Windows.OfType<MainWindow>().ToArray();
                Assert.Equal(6, restored.Length);
                foreach (var path in images.Concat(videos))
                {
                    var window = Assert.Single(restored, w => PathEquals(GetImagePath(w), path));
                    var expectedPage = Array.IndexOf(images, path) >= 0 ? Array.IndexOf(images, path) + 1 : Array.IndexOf(videos, path) + 1;
                    window.SwapViewToPage(expectedPage);
                    Assert.Equal(expectedPage, GetPrivateField<int>(window, "windowPageIndex"));
                    Assert.True(window.IsVisible);
                    var savedState = Assert.Single(saved.CloudlessWindows, state => PathEquals(state.ImagePath, path));
                    Assert.True(Math.Abs(savedState.Width - window.Width) < 0.01, $"Width mismatch for {Path.GetFileName(path)}: saved {savedState.Width}, restored {window.Width}.");
                    Assert.True(Math.Abs(savedState.Height - window.Height) < 0.01, $"Height mismatch for {Path.GetFileName(path)}: saved {savedState.Height}, restored {window.Height}.");
                    Assert.InRange(window.Left, 0, 500);
                    Assert.InRange(window.Top, 0, 500);
                }

                var restoredImage = Assert.Single(restored, w => PathEquals(GetImagePath(w), images[0]));
                var scale = GetPrivateField<ScaleTransform>(restoredImage, "imageScaleTransform");
                var pan = GetPrivateField<TranslateTransform>(restoredImage, "imageTranslateTransform");
                Assert.Equal(1.35, scale.ScaleX, 2);
                Assert.NotEqual(0, pan.X);
                Assert.NotEqual(0, pan.Y);
                AssertImageRenders(restoredImage);

                foreach (var (path, triggerCount) in new[] { (videos[0], 1), (videos[1], 2), (videos[2], 3) })
                {
                    var window = Assert.Single(restored, w => PathEquals(GetImagePath(w), path));
                    Assert.Equal(triggerCount, window.SlideshowTriggerCount);
                    Assert.True(window.VideoIsSynced);
                    Assert.NotNull(window.VideoLoopStart);
                    Assert.NotNull(window.VideoLoopEnd);
                    Assert.NotNull(window.VideoFlag);
                    var player = Assert.IsAssignableFrom<IVideoPlayer>(GetVideoHost(window).Content);
                    Assert.True(player.GetVideoZoom() > 1);
                    Assert.NotEqual((0d, 0d), player.GetVideoPan());
                }

                var pageThreeWindow = Assert.Single(restored, w => PathEquals(GetImagePath(w), videos[2]));
                pageThreeWindow.SwapViewToPage(3);
                pageThreeWindow.ExecuteCommand("ss 60").GetAwaiter().GetResult();
                Assert.True(SlideshowManager.IsRunning);
                pageThreeWindow.ExecuteCommand("ss next").GetAwaiter().GetResult();
                Assert.Equal(1, SlideshowManager.SelectedPage);
                pageThreeWindow.ExecuteCommand("ss stop").GetAwaiter().GetResult();
                Assert.False(SlideshowManager.IsRunning);
            }
            finally
            {
                SlideshowManager.Stop();
                SlideshowManager.ClearTriggers();
                foreach (var window in Application.Current.Windows.OfType<MainWindow>().ToArray())
                    window.Close();
                if (File.Exists(workspacePath))
                    File.Delete(workspacePath);
                settings.CurrentPage = originalCurrentPage;
                settings.DisplayMode = originalDisplayMode;
                var recent = new StringCollection();
                recent.AddRange(originalRecentWorkspaces);
                settings.RecentWorkspaces = recent;
                settings.Save();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
                RestoreFile(recentFilesPath, originalRecentFiles);
                RestoreFile(videoPositionsPath, originalVideoPositions);
            }

            void OpenMedia(string path)
            {
                var window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                mediaWindows.Add(window);
                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                try
                {
                    WaitForDispatcherTask(window.ExecuteCommand("o " + path), window.Dispatcher, "open " + Path.GetFileName(path));
                    if (FileTypeManager.IsVideoFile(Path.GetExtension(path)))
                        WaitForDispatcherCondition(() => GetVideoHost(window).Content is IVideoPlayer, window.Dispatcher, "load the video player for " + Path.GetFileName(path));
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
                Assert.True(PathEquals(GetImagePath(window), path));
            }
        });
    }

    private static void ApplyImageView(MainWindow window, double zoom, double panX, double panY)
    {
        var scale = GetPrivateField<ScaleTransform>(window, "imageScaleTransform");
        scale.ScaleX = zoom;
        scale.ScaleY = zoom;
        var pan = GetPrivateField<TranslateTransform>(window, "imageTranslateTransform");
        pan.X = panX;
        pan.Y = panY;
    }

    private static void CropImage(MainWindow window)
    {
        var originalWidth = window.Width;
        var image = GetImageDisplay(window);
        window.EnterSelectionMode().GetAwaiter().GetResult();
        window.UpdateLayout();
        image.UpdateLayout();
        var bounds = image.TransformToAncestor(window).TransformBounds(new Rect(0, 0, image.ActualWidth, image.ActualHeight));
        var start = new Point(bounds.Left + bounds.Width * 0.2, bounds.Top + bounds.Height * 0.2);
        var end = new Point(bounds.Left + bounds.Width * 0.75, bounds.Top + bounds.Height * 0.75);
        window.SelectionMode_MouseDown(start);
        window.SelectionMode_MouseMove(end);
        window.SelectionMode_MouseUp(end).GetAwaiter().GetResult();
        Assert.True(window.Width < originalWidth);
    }

    private static void ConfigureVideo(MainWindow window, int index)
    {
        var player = Assert.IsAssignableFrom<IVideoPlayer>(GetVideoHost(window).Content);
        player.TogglePause(true);
        player.SetVideoZoom(1.25 + index * 0.1, 200, 150, constrainPan: false);
        player.PanVideoBy(12 + index, -8 - index, constrainToBounds: false);
        player.SeekTo(TimeSpan.FromSeconds(1));
        window.ExecuteCommand("set start").GetAwaiter().GetResult();
        player.SeekTo(TimeSpan.FromSeconds(2));
        window.ExecuteCommand("set end").GetAwaiter().GetResult();
        player.SeekTo(TimeSpan.FromSeconds(1.5));
        window.ExecuteCommand("set flag").GetAwaiter().GetResult();
        window.ExecuteCommand("sync").GetAwaiter().GetResult();
    }

    private static void AssertImageRenders(MainWindow window)
    {
        var image = GetImageDisplay(window);
        window.UpdateLayout();
        image.UpdateLayout();
        Assert.IsAssignableFrom<BitmapSource>(image.Source);
        Assert.True(image.ActualWidth > 0);
        Assert.True(image.ActualHeight > 0);
        var rendered = new RenderTargetBitmap((int)Math.Ceiling(image.ActualWidth), (int)Math.Ceiling(image.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        rendered.Render(image);
        var pixels = new byte[rendered.PixelWidth * rendered.PixelHeight * 4];
        rendered.CopyPixels(pixels, rendered.PixelWidth * 4, 0);
        Assert.Contains(pixels.Where((_, index) => index % 4 == 3), alpha => alpha > 0);
    }

    private static bool IsVideo(MainWindow window) => FileTypeManager.IsVideoFile(Path.GetExtension(GetImagePath(window)));
    private static string GetImagePath(MainWindow window) => GetPrivateField<string>(window, "currentlyDisplayedImagePath");
    private static Image GetImageDisplay(MainWindow window) => GetPrivateField<Image>(window, "ImageDisplay");
    private static ContentControl GetVideoHost(MainWindow window) => GetPrivateField<ContentControl>(window, "VideoHost");

    private static T GetPrivateField<T>(MainWindow window, string fieldName)
    {
        var field = typeof(MainWindow).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<T>(field!.GetValue(window));
    }

    private static bool PathEquals(string actual, string expected) => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool WaitForDispatcherTask(Task<bool> task, Dispatcher dispatcher, string operation)
    {
        WaitForDispatcherTask((Task)task, dispatcher, operation);
        return task.Result;
    }

    private static void WaitForDispatcherTask(Task task, Dispatcher dispatcher, string operation)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(45) };
            timeout.Tick += (_, _) => frame.Continue = false;
            task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            timeout.Start();
            Dispatcher.PushFrame(frame);
            timeout.Stop();
        }

        if (!task.IsCompleted)
            throw new TimeoutException($"Timed out waiting for {operation}.");
        task.GetAwaiter().GetResult();
    }

    private static void WaitForDispatcherCondition(Func<bool> condition, Dispatcher dispatcher, string operation)
    {
        if (condition())
            return;

        var frame = new DispatcherFrame();
        var completed = false;
        var check = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(50) };
        var timeout = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(30) };
        check.Tick += (_, _) =>
        {
            if (condition())
            {
                completed = true;
                frame.Continue = false;
            }
        };
        timeout.Tick += (_, _) => frame.Continue = false;
        check.Start();
        timeout.Start();
        Dispatcher.PushFrame(frame);
        check.Stop();
        timeout.Stop();
        Assert.True(completed, $"Timed out waiting for {operation}.");
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
            throw new Xunit.Sdk.XunitException($"STA workspace UI test failed: {failure}");
    }

    private static void RestoreFile(string path, byte[]? contents)
    {
        if (contents == null)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, contents);
    }

    private static void ResetWpfApplicationSingletonForTestIsolation()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(Application).GetField("_appInstance", flags)!.SetValue(null, null);
        typeof(Application).GetField("_appCreatedInThisAppDomain", flags)!.SetValue(null, false);
        typeof(Application).GetField("_isShuttingDown", flags)!.SetValue(null, false);
    }

}
