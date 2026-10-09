using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class VideoCoordinationUiTests
{
    [Fact]
    public void SynchronizedVideosRestartTogetherOnlyAfterEverySyncedVideoReachesItsEnd()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var windows = new List<MainWindow>();
            try
            {
                var first = CreateVideoWindow(windows, pageIndex: 1);
                var second = CreateVideoWindow(windows, pageIndex: 1);
                var firstPlayer = Assert.IsType<FakeVideoPlayer>(GetVideoHost(first).Content);
                var secondPlayer = Assert.IsType<FakeVideoPlayer>(GetVideoHost(second).Content);
                var nearEnd = (long)firstPlayer.Duration.TotalMilliseconds - 400;
                firstPlayer.TogglePause(false);
                secondPlayer.TogglePause(false);
                first.ExecuteCommand("sync").GetAwaiter().GetResult();
                second.ExecuteCommand("sync").GetAwaiter().GetResult();
                Assert.True(first.VideoIsSynced);
                Assert.True(second.VideoIsSynced);

                firstPlayer.RaiseTimeChanged(nearEnd);

                Assert.True(first.VideoSyncWaiting);
                Assert.True(firstPlayer.IsPaused());
                Assert.False(secondPlayer.IsPaused());

                secondPlayer.RaiseTimeChanged(nearEnd);

                WaitForDispatcherCondition(
                    () => !first.VideoSyncWaiting && !second.VideoSyncWaiting && !firstPlayer.IsPaused() && !secondPlayer.IsPaused(),
                    first.Dispatcher,
                    "the synchronized videos to restart together");

                Assert.Equal(TimeSpan.Zero, firstPlayer.Position);
                Assert.Equal(TimeSpan.Zero, secondPlayer.Position);
            }
            finally
            {
                foreach (var window in windows)
                {
                    if (window.VideoIsSynced)
                        window.ExecuteCommand("unsync").GetAwaiter().GetResult();
                    window.Close();
                }
                SlideshowManager.Stop();
                SlideshowManager.ClearTriggers();
                app.Shutdown();
            }
        });
    }

    [Fact]
    public void VideoOutsideSyncGroupDoesNotDelayOrJoinSynchronizedRestart()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var windows = new List<MainWindow>();
            try
            {
                var synced = CreateVideoWindow(windows, pageIndex: 1);
                var independent = CreateVideoWindow(windows, pageIndex: 1);
                var syncedPlayer = Assert.IsType<FakeVideoPlayer>(GetVideoHost(synced).Content);
                var independentPlayer = Assert.IsType<FakeVideoPlayer>(GetVideoHost(independent).Content);
                var nearEnd = (long)syncedPlayer.Duration.TotalMilliseconds - 400;
                syncedPlayer.TogglePause(false);
                independentPlayer.TogglePause(false);
                synced.ExecuteCommand("sync").GetAwaiter().GetResult();

                syncedPlayer.RaiseTimeChanged(nearEnd);

                WaitForDispatcherCondition(
                    () => !synced.VideoSyncWaiting && !syncedPlayer.IsPaused(),
                    synced.Dispatcher,
                    "the single synchronized video to restart");

                Assert.False(independentPlayer.IsPaused());
                Assert.False(independent.VideoSyncWaiting);
            }
            finally
            {
                foreach (var window in windows)
                {
                    if (window.VideoIsSynced)
                        window.ExecuteCommand("unsync").GetAwaiter().GetResult();
                    window.Close();
                }
                SlideshowManager.Stop();
                SlideshowManager.ClearTriggers();
                app.Shutdown();
            }
        });
    }

    [Fact]
    public void SlideshowTriggerRequiresConfiguredNumberOfVideoEndHits()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var windows = new List<MainWindow>();
            try
            {
                var triggerWindow = CreateVideoWindow(windows, pageIndex: 1);
                var player = Assert.IsType<FakeVideoPlayer>(GetVideoHost(triggerWindow).Content);
                triggerWindow.ExecuteCommand("set trigger 2").GetAwaiter().GetResult();
                SlideshowManager.Initialize(0, new List<int> { 1, 2 }, 0, triggerWindow.Dispatcher, () => { }, useTriggers: true);

                player.RaiseTimeChanged((long)player.Duration.TotalMilliseconds);

                Assert.Equal(1, triggerWindow.GetSlideshowTriggerHitCount());
                Assert.Equal(1, SlideshowManager.SelectedPage);

                Thread.Sleep(350);
                player.RaiseTimeChanged((long)player.Duration.TotalMilliseconds);

                Assert.Equal(0, triggerWindow.GetSlideshowTriggerHitCount());
                Assert.Equal(2, SlideshowManager.SelectedPage);
            }
            finally
            {
                foreach (var window in windows)
                    window.Close();
                SlideshowManager.Stop();
                SlideshowManager.ClearTriggers();
                app.Shutdown();
            }
        });
    }

    private static App CreateApplication()
    {
        ResetWpfApplicationSingletonForTestIsolation();
        App.SuppressStartupForUiTests = true;
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ThemeManager.ApplyThemeGlobalResources("Dark");
        return app;
    }

    private static MainWindow CreateVideoWindow(List<MainWindow> windows, int pageIndex)
    {
        var window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
        SetField(window, "windowPageIndex", pageIndex);
        SetField(window, "currentlyDisplayedImagePath", $@"C:\video-{Guid.NewGuid():N}.mp4");
        GetVideoHost(window).Content = new FakeVideoPlayer();
        window.Show();
        windows.Add(window);
        return window;
    }

    private static ContentControl GetVideoHost(MainWindow window)
    {
        return (ContentControl)(typeof(MainWindow).GetField("VideoHost", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(window)
            ?? throw new InvalidOperationException("VideoHost was not found."));
    }

    private static void SetField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        field.SetValue(instance, value);
    }

    private static void WaitForDispatcherCondition(Func<bool> condition, Dispatcher dispatcher, string operation)
    {
        if (condition())
            return;

        var frame = new DispatcherFrame();
        var completed = false;
        var checkTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(25) };
        var timeoutTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(5) };
        checkTimer.Tick += (_, _) =>
        {
            if (condition())
            {
                completed = true;
                frame.Continue = false;
            }
        };
        timeoutTimer.Tick += (_, _) => frame.Continue = false;
        checkTimer.Start();
        timeoutTimer.Start();
        Dispatcher.PushFrame(frame);
        checkTimer.Stop();
        timeoutTimer.Stop();
        Assert.True(completed, $"Timed out waiting for {operation}.");
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
            throw new Xunit.Sdk.XunitException($"Video coordination UI test failed: {failure}");
    }

    private static void ResetWpfApplicationSingletonForTestIsolation()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(Application).GetField("_appInstance", flags)!.SetValue(null, null);
        typeof(Application).GetField("_appCreatedInThisAppDomain", flags)!.SetValue(null, false);
        typeof(Application).GetField("_isShuttingDown", flags)!.SetValue(null, false);
    }
}
