using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class ContextMenuUiTests
{
    [Fact]
    public void ZoomContextMenu_IsPopulatedButDisabledWithoutMedia_AndWorksAfterLoadingAnImage()
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
            var recentFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cloudless", "recent_files.json");
            var originalRecentFiles = File.Exists(recentFilesPath) ? File.ReadAllBytes(recentFilesPath) : null;
            MainWindow? emptyWindow = null;
            MainWindow? imageWindow = null;

            try
            {
                settings.CurrentPage = 1;
                settings.DisplayMode = "BestFit";

                emptyWindow = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                emptyWindow.Show();
                var emptyContextMenu = GetContextMenu(emptyWindow);
                emptyWindow.SimulateKeyEvent(System.Windows.Input.Key.X, false, false, false).GetAwaiter().GetResult();
                WaitForDispatcherCondition(() => emptyContextMenu.IsOpen, emptyWindow.Dispatcher, "the empty-window context menu to open");
                var emptyZoomMenu = GetZoomMenu(emptyContextMenu);
                Assert.Equal(9, emptyZoomMenu.Items.Count);
                Assert.False(emptyZoomMenu.IsEnabled);
                emptyContextMenu.IsOpen = false;
                emptyWindow.Close();
                emptyWindow = null;

                var imagePath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "img1-landscape.jpg");
                imageWindow = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                imageWindow.Show();
                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(imageWindow.Dispatcher));
                try
                {
                    WaitForDispatcherTask(imageWindow.ExecuteCommand("o " + imagePath), imageWindow.Dispatcher, "open the test image");
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }

                var imageDisplay = GetPrivateField<Image>(imageWindow, "ImageDisplay");
                WaitForDispatcherCondition(() => imageDisplay.Source != null, imageWindow.Dispatcher, "the test image to be rendered");
                var contextMenu = GetContextMenu(imageWindow);
                imageWindow.SimulateKeyEvent(System.Windows.Input.Key.X, false, false, false).GetAwaiter().GetResult();
                WaitForDispatcherCondition(() => contextMenu.IsOpen, imageWindow.Dispatcher, "the image context menu to open");
                var zoomMenu = GetZoomMenu(contextMenu);
                Assert.Equal(9, zoomMenu.Items.Count);
                Assert.True(zoomMenu.IsEnabled);

                var zoomPreset = Assert.Single(zoomMenu.Items.OfType<MenuItem>(), item => item.Header?.ToString() == "150%");
                zoomPreset.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, zoomPreset));
                WaitForDispatcherCondition(
                    () => imageWindow.imageScaleTransform!.ScaleX > 1,
                    imageWindow.Dispatcher,
                    "the 150% zoom preset to apply");
                contextMenu.IsOpen = false;
            }
            finally
            {
                foreach (var window in Application.Current.Windows.OfType<MainWindow>().ToArray())
                    window.Close();
                settings.CurrentPage = originalCurrentPage;
                settings.DisplayMode = originalDisplayMode;
                settings.Save();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
                RestoreFile(recentFilesPath, originalRecentFiles);
            }
        });
    }

    private static ContextMenu GetContextMenu(MainWindow window) => GetPrivateField<ContextMenu>(window, "ImageContextMenu");

    private static MenuItem GetZoomMenu(ContextMenu contextMenu) => Assert.Single(
        contextMenu.Items.OfType<MenuItem>(),
        item => item.Header?.ToString()?.StartsWith("Zoom", StringComparison.Ordinal) == true);

    private static T GetPrivateField<T>(MainWindow window, string fieldName)
    {
        var field = typeof(MainWindow).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<T>(field!.GetValue(window));
    }

    private static void WaitForDispatcherTask(Task task, Dispatcher dispatcher, string operation)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(30) };
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
            throw new Xunit.Sdk.XunitException($"STA context-menu UI test failed: {failure}");
    }

    private static void ResetWpfApplicationSingletonForTestIsolation()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(Application).GetField("_appInstance", flags)!.SetValue(null, null);
        typeof(Application).GetField("_appCreatedInThisAppDomain", flags)!.SetValue(null, false);
        typeof(Application).GetField("_isShuttingDown", flags)!.SetValue(null, false);
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
}
