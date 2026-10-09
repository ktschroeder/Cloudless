using System.Reflection;
using System.Windows;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class PageCommandUiTests
{
    [Fact]
    public void BringPageCommandMovesCurrentWindowAndSwitchesView()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            try
            {
                settings.CurrentPage = 1;
                var movedWindow = CreateMediaWindow(1, "moved.jpg");
                var remainingWindow = CreateMediaWindow(1, "remaining.jpg");
                var targetWindow = CreateMediaWindow(2, "target.jpg");

                movedWindow.ExecuteCommand("p 2 bring").GetAwaiter().GetResult();

                Assert.Equal(2, GetPageIndex(movedWindow));
                Assert.Equal(2, settings.CurrentPage);
                Assert.True(movedWindow.IsVisible);
                Assert.True(targetWindow.IsVisible);
                Assert.False(remainingWindow.IsVisible);
                Assert.Equal(1, GetPageIndex(remainingWindow));
            }
            finally
            {
                CloseTestWindows();
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                ShutdownApplication(app);
            }
        });
    }

    [Fact]
    public void SendPageCommandMovesEveryMediaWindowWithoutChangingTheCurrentView()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            try
            {
                settings.CurrentPage = 1;
                var first = CreateMediaWindow(1, "first.jpg");
                var second = CreateMediaWindow(1, "second.jpg");
                var target = CreateMediaWindow(2, "target.jpg");

                first.ExecuteCommand("p 2 send page").GetAwaiter().GetResult();

                Assert.Equal(1, settings.CurrentPage);
                Assert.Equal(2, GetPageIndex(first));
                Assert.Equal(2, GetPageIndex(second));
                Assert.Equal(2, GetPageIndex(target));
                Assert.False(first.IsVisible);
                Assert.False(second.IsVisible);
                Assert.False(target.IsVisible);
                Assert.Contains(Application.Current.Windows.OfType<MainWindow>(), window =>
                    GetPageIndex(window) == 1 && string.IsNullOrEmpty(GetImagePath(window)));
            }
            finally
            {
                CloseTestWindows();
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                ShutdownApplication(app);
            }
        });
    }

    [Fact]
    public void SwapAndClearPageCommandsExchangeAndRemoveOnlyTheirTargetPage()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            try
            {
                settings.CurrentPage = 1;
                var originalPageOne = CreateMediaWindow(1, "page-one.jpg");
                var originalPageTwo = CreateMediaWindow(2, "page-two.jpg");

                originalPageOne.ExecuteCommand("p 1 swap 2").GetAwaiter().GetResult();

                Assert.Equal(2, GetPageIndex(originalPageOne));
                Assert.Equal(1, GetPageIndex(originalPageTwo));
                Assert.False(originalPageOne.IsVisible);
                Assert.True(originalPageTwo.IsVisible);
                Assert.Equal(1, settings.CurrentPage);

                originalPageTwo.ExecuteCommand("p 2 clear").GetAwaiter().GetResult();

                Assert.False(originalPageOne.IsVisible);
                Assert.DoesNotContain(originalPageOne, Application.Current.Windows.OfType<MainWindow>());
                Assert.Contains(originalPageTwo, Application.Current.Windows.OfType<MainWindow>());
                Assert.Equal(1, GetPageIndex(originalPageTwo));
            }
            finally
            {
                CloseTestWindows();
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                ShutdownApplication(app);
            }
        });
    }

    [Fact]
    public void RelativePageTargetsNavigateAcrossActiveAndInactivePagesWithWraparound()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            try
            {
                settings.CurrentPage = 2;
                var currentWindow = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                currentWindow.Show();
                CreateMediaWindow(1, "active-one.jpg");
                CreateMediaWindow(4, "active-four.jpg");

                currentWindow.ExecuteCommand("p na").GetAwaiter().GetResult();
                Assert.Equal(4, settings.CurrentPage);

                currentWindow.ExecuteCommand("p pa").GetAwaiter().GetResult();
                Assert.Equal(1, settings.CurrentPage);

                currentWindow.ExecuteCommand("p ni").GetAwaiter().GetResult();
                Assert.Equal(2, settings.CurrentPage);

                currentWindow.ExecuteCommand("p pi").GetAwaiter().GetResult();
                Assert.Equal(20, settings.CurrentPage);

                for (var page = 1; page <= 8; page++)
                {
                    currentWindow.SimulateKeyEvent((System.Windows.Input.Key)((int)System.Windows.Input.Key.D1 + page - 1), false, false, false).GetAwaiter().GetResult();
                    Assert.Equal(page, settings.CurrentPage);
                }

                currentWindow.SimulateKeyEvent(System.Windows.Input.Key.Left, false, false, true).GetAwaiter().GetResult();
                Assert.Equal(7, settings.CurrentPage);
                currentWindow.SimulateKeyEvent(System.Windows.Input.Key.Right, false, false, true).GetAwaiter().GetResult();
                Assert.Equal(8, settings.CurrentPage);

                currentWindow.SwapViewToPage(20);
                currentWindow.SimulateKeyEvent(System.Windows.Input.Key.Right, false, false, true).GetAwaiter().GetResult();
                Assert.Equal(1, settings.CurrentPage);
                currentWindow.SimulateKeyEvent(System.Windows.Input.Key.Left, false, false, true).GetAwaiter().GetResult();
                Assert.Equal(20, settings.CurrentPage);

                for (var page = 9; page <= 20; page++)
                {
                    var pageDigit = page > 10 ? page - 10 : page;
                    var key = pageDigit == 10
                        ? System.Windows.Input.Key.D0
                        : (System.Windows.Input.Key)((int)System.Windows.Input.Key.D1 + pageDigit - 1);
                    currentWindow.SimulateKeyEvent(key, shift: false, control: false, alt: page > 10).GetAwaiter().GetResult();
                    Assert.Equal(page, settings.CurrentPage);
                }
            }
            finally
            {
                CloseTestWindows();
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                ShutdownApplication(app);
            }
        });
    }

    [Fact]
    public void LayoutLockHotkeyTogglesGlobalResizeProtection()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var originalLayoutLocked = MainWindow.LayoutLocked;
            MainWindow? window = null;
            try
            {
                MainWindow.LayoutLocked = false;
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();

                window.SimulateKeyEvent(System.Windows.Input.Key.F, shift: false, control: true, alt: true).GetAwaiter().GetResult();

                Assert.True(MainWindow.LayoutLocked);
                Assert.Equal(ResizeMode.NoResize, window.ResizeMode);

                window.SimulateKeyEvent(System.Windows.Input.Key.F, shift: false, control: true, alt: true).GetAwaiter().GetResult();

                Assert.False(MainWindow.LayoutLocked);
                Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
            }
            finally
            {
                CloseTestWindows();
                MainWindow.LayoutLocked = originalLayoutLocked;
                ShutdownApplication(app);
            }
        });
    }

    private static MainWindow CreateMediaWindow(int pageIndex, string path)
    {
        var window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
        SetField(window, "windowPageIndex", pageIndex);
        SetField(window, "currentlyDisplayedImagePath", path);
        window.Show();
        if (pageIndex != Cloudless.Properties.Settings.Default.CurrentPage)
            window.Hide();
        return window;
    }

    private static int GetPageIndex(MainWindow window) => GetField<int>(window, "windowPageIndex");

    private static string GetImagePath(MainWindow window) => GetField<string>(window, "currentlyDisplayedImagePath");

    private static T GetField<T>(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Field '{name}' was not found.");
        return (T)field.GetValue(instance)!;
    }

    private static void SetField<T>(object instance, string name, T value)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Field '{name}' was not found.");
        field.SetValue(instance, value);
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

    private static void CloseTestWindows()
    {
        if (Application.Current == null)
            return;
        SlideshowManager.Stop();
        SlideshowManager.ClearTriggers();
        foreach (var window in Application.Current.Windows.OfType<Window>().ToArray())
            window.Close();
    }

    private static void ShutdownApplication(App app)
    {
        app.Shutdown();
        App.SuppressStartupForUiTests = false;
        ResetWpfApplicationSingletonForTestIsolation();
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
            throw new Xunit.Sdk.XunitException($"Page command UI test failed: {failure}");
    }
}
