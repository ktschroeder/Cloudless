using System.IO;
using System.Reflection;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cloudless.PluginBase;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class HotkeyActionUiTests
{
    [Fact]
    public void BookmarkHotkeyAddsAndShiftBookmarkRemovesCurrentMedia()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var directory = Path.Combine(Path.GetTempPath(), "cloudless-bookmark-hotkey-" + Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                Directory.CreateDirectory(directory);
                var manager = new BookmarkManager(Path.Combine(directory, "bookmarks.json"));
                var mediaPath = Path.Combine(directory, "image.jpg");
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                SetField(window, "bookmarkManager", manager);

                window.SimulateKeyEvent(Key.B, shift: false, control: false, alt: false).GetAwaiter().GetResult();
                Assert.Equal("No image is displayed.", window.LastMessage);

                SetField(window, "currentlyDisplayedImagePath", mediaPath);
                window.SimulateKeyEvent(Key.B, shift: false, control: false, alt: false).GetAwaiter().GetResult();
                Assert.True(manager.IsBookmarked(mediaPath));
                Assert.Equal("Bookmark added.", window.LastMessage);

                window.SimulateKeyEvent(Key.B, shift: false, control: false, alt: false).GetAwaiter().GetResult();
                Assert.Equal("Cannot bookmark: Image is already bookmarked.", window.LastMessage);

                window.SimulateKeyEvent(Key.B, shift: true, control: false, alt: false).GetAwaiter().GetResult();
                Assert.False(manager.IsBookmarked(mediaPath));
                Assert.Equal("Bookmark removed.", window.LastMessage);

                window.SimulateKeyEvent(Key.B, shift: true, control: false, alt: false).GetAwaiter().GetResult();
                Assert.Equal("Cannot un-bookmark: Image isn't bookmarked.", window.LastMessage);
            }
            finally
            {
                CloseTestWindows();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        });
    }

    [Fact]
    public void VideoStatusCommandsReportPlayerValuesAndClearConfiguredLoopBounds()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            MainWindow? window = null;
            try
            {
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                var player = new FakeVideoPlayer();
                GetField<ContentControl>(window, "VideoHost").Content = player;
                SetField(window, "currentlyDisplayedImagePath", @"C:\test-video.mp4");

                window.ExecuteCommand("volume").GetAwaiter().GetResult();
                Assert.Equal("Current volume: 100/100", window.LastMessage);
                player.SetVolume(55);
                window.ExecuteCommand("volume").GetAwaiter().GetResult();
                Assert.Equal("Current volume: 55/100", window.LastMessage);

                window.ExecuteCommand("speed").GetAwaiter().GetResult();
                Assert.Equal("Current playback speed: 1×", window.LastMessage);
                player.SetPlaybackSpeed(1.5);
                window.ExecuteCommand("speed").GetAwaiter().GetResult();
                Assert.Equal("Current playback speed: 1.5×", window.LastMessage);

                player.SetAudioDelay(120_000);
                player.SetSubtitleDelay(-250_000);
                window.ExecuteCommand("audio sync").GetAwaiter().GetResult();
                Assert.Equal("Current audio/video sync offset: +0.12 s (+120 ms)", window.LastMessage);
                window.ExecuteCommand("subtitle sync").GetAwaiter().GetResult();
                Assert.Equal("Current subtitle/video sync offset: -0.25 s (-250 ms)", window.LastMessage);

                player.SeekTo(TimeSpan.FromSeconds(12));
                window.ExecuteCommand("set start").GetAwaiter().GetResult();
                player.SeekTo(TimeSpan.FromSeconds(48));
                window.ExecuteCommand("set end").GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(12), window.VideoLoopStart);
                Assert.Equal(TimeSpan.FromSeconds(48), window.VideoLoopEnd);
                window.ExecuteCommand("clear s").GetAwaiter().GetResult();
                window.ExecuteCommand("clear e").GetAwaiter().GetResult();
                Assert.Null(window.VideoLoopStart);
                Assert.Null(window.VideoLoopEnd);
                Assert.Null(player.LoopStart);
                Assert.Null(player.LoopEnd);

                GetField<ContentControl>(window, "VideoHost").Content = null;
                window.ExecuteCommand("speed").GetAwaiter().GetResult();
                Assert.Equal("No video is loaded", window.LastMessage);
                window.ExecuteCommand("audio sync").GetAwaiter().GetResult();
                Assert.Equal("The audio delay command requires a loaded video.", window.LastMessage);
            }
            finally
            {
                CloseTestWindows();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
            }
        });
    }

    [Fact]
    public void CustomCommandHotkeysMapAllTwentyFourSlotsAndHandleAnEmptySlot()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCommands = settings.UserCommands?.Cast<string>().ToArray() ?? Array.Empty<string>();
            var originalHistory = settings.CommandHistory?.Cast<string>().ToArray() ?? Array.Empty<string>();
            try
            {
                var commands = Enumerable.Range(1, 24).Select(index => $"echo shortcut-slot-{index}").ToArray();
                settings.UserCommands = ToStringCollection(commands);
                var window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();

                for (var slot = 0; slot < commands.Length; slot++)
                {
                    var key = (Key)((int)Key.D1 + slot % 8);
                    var usesControlAlt = slot >= 8;
                    var usesShift = slot >= 16;
                    window.SimulateKeyEvent(key, usesShift, control: true, alt: usesControlAlt).GetAwaiter().GetResult();
                    Assert.Equal($"echo: shortcut-slot-{slot + 1}", window.LastMessage);
                }

                commands[0] = string.Empty;
                settings.UserCommands = ToStringCollection(commands);
                window.SimulateKeyEvent(Key.D1, shift: false, control: true, alt: false).GetAwaiter().GetResult();
                Assert.Equal("No command found at index 1.", window.LastMessage);
            }
            finally
            {
                CloseTestWindows();
                settings.UserCommands = ToStringCollection(originalCommands);
                settings.CommandHistory = ToStringCollection(originalHistory);
                settings.Save();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
            }
        });
    }

    [Fact]
    public void VideoHotkeysCoverMuteRestartAndNormalBroadAndFineSeeking()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            MainWindow? window = null;
            try
            {
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                var player = new FakeVideoPlayer
                {
                    Position = TimeSpan.FromSeconds(120),
                    Duration = TimeSpan.FromMinutes(10)
                };
                GetField<ContentControl>(window, "VideoHost").Content = player;
                SetField(window, "currentlyDisplayedImagePath", @"C:\test-video.mp4");

                window.SimulateKeyEvent(Key.M, shift: false, control: true, alt: false).GetAwaiter().GetResult();
                Assert.True(player.IsMuted());
                window.SimulateKeyEvent(Key.M, shift: false, control: true, alt: false).GetAwaiter().GetResult();
                Assert.False(player.IsMuted());

                window.SimulateKeyEvent(Key.Space, shift: false, control: false, alt: false).GetAwaiter().GetResult();
                Assert.False(player.IsPaused());
                window.SimulateKeyEvent(Key.Space, shift: false, control: true, alt: false).GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.Zero, player.Position);

                player.Position = TimeSpan.FromSeconds(120);
                window.SimulateKeyEvent(Key.Left, shift: false, control: true, alt: false).GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(115), player.Position);
                window.SimulateKeyEvent(Key.Right, shift: false, control: true, alt: false).GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(120), player.Position);
                window.SimulateKeyEvent(Key.Left, shift: false, control: true, alt: true).GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(60), player.Position);
                window.SimulateKeyEvent(Key.Right, shift: false, control: true, alt: true).GetAwaiter().GetResult();
                Assert.Equal(TimeSpan.FromSeconds(120), player.Position);

                window.SimulateKeyEvent(Key.Left, shift: true, control: true, alt: false).GetAwaiter().GetResult();
                window.SimulateKeyEvent(Key.Right, shift: true, control: true, alt: false).GetAwaiter().GetResult();
                Assert.Equal(1, player.FineSeekBackwardCount);
                Assert.Equal(1, player.FineSeekForwardCount);
                Assert.Equal(TimeSpan.FromSeconds(120), player.Position);
            }
            finally
            {
                CloseTestWindows();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
            }
        });
    }

    [Fact]
    public void FullscreenEscapeAndDebugHotkeysToggleTheirWindowStates()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            MainWindow? window = null;
            try
            {
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                var debugPanel = GetField<System.Windows.Controls.Border>(window, "DebugTextBlockBorder");
                Assert.NotEqual(Visibility.Visible, debugPanel.Visibility);

                window.SimulateKeyEvent(Key.F11, shift: false, control: false, alt: false).GetAwaiter().GetResult();
                Assert.Equal(WindowState.Maximized, window.WindowState);
                window.SimulateKeyEvent(Key.Escape, shift: false, control: false, alt: false).GetAwaiter().GetResult();
                Assert.Equal(WindowState.Normal, window.WindowState);

                window.SimulateKeyEvent(Key.D, shift: false, control: true, alt: true).GetAwaiter().GetResult();
                Assert.Equal(Visibility.Visible, debugPanel.Visibility);
                window.SimulateKeyEvent(Key.D, shift: false, control: true, alt: true).GetAwaiter().GetResult();
                Assert.Equal(Visibility.Collapsed, debugPanel.Visibility);

                window.SimulateKeyEvent(Key.M, shift: false, control: false, alt: false).GetAwaiter().GetResult();
                Assert.Equal(WindowState.Minimized, window.WindowState);
            }
            finally
            {
                CloseTestWindows();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
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

    private static T GetField<T>(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Field '{name}' was not found.");
        return Assert.IsAssignableFrom<T>(field.GetValue(instance));
    }

    private static void SetField<T>(object instance, string name, T value)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Field '{name}' was not found.");
        field.SetValue(instance, value);
    }

    private static StringCollection ToStringCollection(IEnumerable<string> values)
    {
        var collection = new StringCollection();
        collection.AddRange(values.ToArray());
        return collection;
    }

    private static void CloseTestWindows()
    {
        if (Application.Current == null)
            return;
        foreach (var window in Application.Current.Windows.OfType<Window>().ToArray())
            window.Close();
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
            throw new Xunit.Sdk.XunitException($"Hotkey action UI test failed: {failure}");
    }
}
