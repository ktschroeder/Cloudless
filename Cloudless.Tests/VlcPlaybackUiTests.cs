using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Cloudless.PluginBase;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "WindowsIntegration")]
public sealed class VlcPlaybackUiTests
{
    [Fact]
    public void RealVlcVideoOpensPlaysSeeksAndRespondsToSpace()
    {
        RunOnStaThread(() =>
        {
            ResetWpfApplicationSingletonForTestIsolation();
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ThemeManager.ApplyThemeGlobalResources("Dark");
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            var recentFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudless", "recent_files.json");
            var originalRecentFiles = File.Exists(recentFilesPath) ? File.ReadAllBytes(recentFilesPath) : null;
            var videoPositionsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudless", "video_positions.json");
            var originalVideoPositions = File.Exists(videoPositionsPath) ? File.ReadAllBytes(videoPositionsPath) : null;
            MainWindow? window = null;

            try
            {
                settings.CurrentPage = 1;
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                var videoHost = GetPrivateField<ContentControl>(window, "VideoHost");
                var imagePathField = typeof(MainWindow).GetField("currentlyDisplayedImagePath", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("The current media path field was not found.");
                var videoPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "vid1-landscape.mp4");

                var previousSynchronizationContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                try
                {
                    WaitForDispatcherTask(
                        window.ExecuteCommand("o " + videoPath),
                        window.Dispatcher,
                        "the checked-in MP4 to load through VLC",
                        timeout: TimeSpan.FromMinutes(2));
                    Assert.Equal(videoPath, imagePathField.GetValue(window)?.ToString(), StringComparer.OrdinalIgnoreCase);

                    var videoPlayer = Assert.IsAssignableFrom<IVideoPlayer>(videoHost.Content);
                    var playbackTime = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
                    EventHandler<VideoTimeChangedEventArgs> timeChangedHandler = (_, args) =>
                    {
                        if (args.TimeMilliseconds > 0)
                            playbackTime.TrySetResult(args.TimeMilliseconds);
                    };
                    videoPlayer.TimeChanged += timeChangedHandler;
                    try
                    {
                        WaitForDispatcherTask(playbackTime.Task, window.Dispatcher, "VLC to report advancing playback time");
                        Assert.True(playbackTime.Task.Result > 0);
                        WaitForDispatcherCondition(
                            () => videoPlayer.GetPosition() > TimeSpan.Zero,
                            window.Dispatcher,
                            "the VLC playback position to advance");

                        window.ExecuteCommand("pause").GetAwaiter().GetResult();
                        WaitForDispatcherCondition(() => videoPlayer.IsPaused(), window.Dispatcher, "VLC to pause before seeking");
                        Assert.True(videoPlayer.GetPosition() > TimeSpan.Zero);
                        window.ExecuteCommand("seek 0").GetAwaiter().GetResult();
                        WaitForDispatcherCondition(
                            () => videoPlayer.GetPosition() <= TimeSpan.FromMilliseconds(100),
                            window.Dispatcher,
                            "VLC to seek to the beginning");
                        window.ExecuteCommand("play").GetAwaiter().GetResult();
                        WaitForDispatcherCondition(() => !videoPlayer.IsPaused(), window.Dispatcher, "VLC to begin playback");

                        window.ExecuteCommand("pause").GetAwaiter().GetResult();
                        WaitForDispatcherCondition(() => videoPlayer.IsPaused(), window.Dispatcher, "VLC to pause");
                        window.ExecuteCommand("play").GetAwaiter().GetResult();
                        WaitForDispatcherCondition(() => !videoPlayer.IsPaused(), window.Dispatcher, "VLC to resume playback");

                        var videoView = Assert.IsAssignableFrom<UIElement>(videoHost.Content);
                        var presentationSource = PresentationSource.FromVisual(videoView);
                        Assert.NotNull(presentationSource);
                        RaiseSpaceKey(videoView, presentationSource!, window.Dispatcher, paused: true);
                        RaiseSpaceKey(videoView, presentationSource!, window.Dispatcher, paused: false);
                    }
                    finally
                    {
                        videoPlayer.TimeChanged -= timeChangedHandler;
                    }
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousSynchronizationContext);
                }
            }
            finally
            {
                if (Application.Current != null)
                {
                    foreach (var openWindow in Application.Current.Windows.OfType<Window>().ToArray())
                        openWindow.Close();
                }
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                app.Shutdown();
                RestoreFile(recentFilesPath, originalRecentFiles);
                RestoreFile(videoPositionsPath, originalVideoPositions);
                ResetWpfApplicationSingletonForTestIsolation();
            }
        });
    }

    private static T GetPrivateField<T>(MainWindow window, string fieldName)
    {
        var field = typeof(MainWindow).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        return Assert.IsAssignableFrom<T>(field.GetValue(window));
    }

    private static void RaiseSpaceKey(UIElement videoView, PresentationSource source, Dispatcher dispatcher, bool paused)
    {
        var keyEvent = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Space)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        videoView.RaiseEvent(keyEvent);
        Assert.True(keyEvent.Handled);
        WaitForDispatcherCondition(
            () => (videoView is IVideoPlayer player) && player.IsPaused() == paused,
            dispatcher,
            paused ? "the video view's Space key to pause playback" : "the video view's Space key to resume playback");
    }

    private static void WaitForDispatcherTask(Task task, Dispatcher dispatcher, string operation, TimeSpan? timeout = null)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var timedOut = false;
            var timeoutTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = timeout ?? TimeSpan.FromSeconds(30)
            };
            timeoutTimer.Tick += (_, _) =>
            {
                timedOut = true;
                frame.Continue = false;
            };
            task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            timeoutTimer.Start();
            Dispatcher.PushFrame(frame);
            timeoutTimer.Stop();
            if (timedOut && !task.IsCompleted)
                throw new TimeoutException($"Timed out waiting for {operation}.");
        }
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
            throw new Xunit.Sdk.XunitException($"VLC playback UI test failed: {failure}");
    }
}
