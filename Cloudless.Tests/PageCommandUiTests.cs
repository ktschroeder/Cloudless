using System.Reflection;
using System.Windows;
using System.Windows.Input;
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
    public void StandaloneMinimizeCommandsOnlyAffectTheCurrentWindow()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            try
            {
                settings.CurrentPage = 1;
                var current = CreateMediaWindow(1, "current.jpg");
                var other = CreateMediaWindow(1, "other.jpg");

                current.ExecuteCommand("m").GetAwaiter().GetResult();

                Assert.Equal(WindowState.Minimized, current.WindowState);
                Assert.Equal(WindowState.Normal, other.WindowState);

                current.ExecuteCommand("um").GetAwaiter().GetResult();

                Assert.Equal(WindowState.Normal, current.WindowState);
                Assert.Equal(WindowState.Normal, other.WindowState);
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
    public void SelectedScopeMinimizesAndUnminimizesOnlySelectedWindows()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            try
            {
                settings.CurrentPage = 1;
                var selectedWindow = CreateMediaWindow(1, "selected.jpg");
                var otherWindow = CreateMediaWindow(1, "other.jpg");
                GlobalMouseModeManager.ClearSelection();
                GlobalMouseModeManager.SelectWindow(selectedWindow);

                otherWindow.ExecuteCommand("m sel").GetAwaiter().GetResult();

                Assert.Equal(WindowState.Minimized, selectedWindow.WindowState);
                Assert.Equal(WindowState.Normal, otherWindow.WindowState);

                otherWindow.ExecuteCommand("um sel").GetAwaiter().GetResult();

                Assert.Equal(WindowState.Normal, selectedWindow.WindowState);
                Assert.Equal(WindowState.Normal, otherWindow.WindowState);

                selectedWindow.WindowState = WindowState.Minimized;
                otherWindow.WindowState = WindowState.Minimized;
                selectedWindow.ExecuteCommand("um others").GetAwaiter().GetResult();

                Assert.Equal(WindowState.Minimized, selectedWindow.WindowState);
                Assert.Equal(WindowState.Normal, otherWindow.WindowState);
            }
            finally
            {
                CloseTestWindows();
                GlobalMouseModeManager.ClearSelection();
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                ShutdownApplication(app);
            }
        });
    }

    [Fact]
    public void OriginWrapperExecutesCommandsOnlyForWindowsWithTheSameOrigin()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            try
            {
                settings.CurrentPage = 1;
                var first = CreateMediaWindow(1, "origin-first.jpg");
                var second = CreateMediaWindow(1, "origin-second.jpg");
                var unrelated = CreateMediaWindow(1, "origin-unrelated.jpg");
                SetField(first, "imageOriginalWorkspaceName", "shared-origin");
                SetField(second, "imageOriginalWorkspaceName", "shared-origin");
                SetField(unrelated, "imageOriginalWorkspaceName", "different-origin");
                string? unrelatedInitialMessage = unrelated.LastMessage;

                first.ExecuteCommand("origin echo origin-wrapper-test").GetAwaiter().GetResult();

                Assert.Equal("echo: origin-wrapper-test", first.LastMessage);
                Assert.Equal("echo: origin-wrapper-test", second.LastMessage);
                Assert.Equal(unrelatedInitialMessage, unrelated.LastMessage);
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
    public void SelectionModeWheelCyclesOverlappingWindowsAndCtrlWheelChangesPages()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            var originalMouseMode = GlobalMouseModeManager.CurrentMode;
            try
            {
                settings.CurrentPage = 1;
                var first = new MainWindow(string.Empty, 800, 600, workspaceLoad: true)
                {
                    Left = 120,
                    Top = 120
                };
                var second = new MainWindow(string.Empty, 800, 600, workspaceLoad: true)
                {
                    Left = 120,
                    Top = 120
                };
                first.Show();
                second.Show();
                GlobalMouseModeManager.SetMode(CloudlessMouseMode.Selection);
                first.SetWindowCropMode(true);
                first.SetWindowCropMode(false);
                var previousOverlay = GetField<MultifunctionalOverlayWindow>(first, "_multifunctionalOverlayWindow");
                GlobalMouseModeManager.SelectWindow(first);
                var selectedOverlay = GetField<MultifunctionalOverlayWindow>(first, "_multifunctionalOverlayWindow");
                Assert.NotSame(previousOverlay, selectedOverlay);
                Assert.Equal(Visibility.Collapsed, GetField<System.Windows.Shapes.Rectangle>(selectedOverlay, "_cropBorderVisual").Visibility);

                first.SetWindowCropMode(true);
                var cropOverlay = GetField<MultifunctionalOverlayWindow>(first, "_multifunctionalOverlayWindow");
                Assert.NotSame(selectedOverlay, cropOverlay);
                Assert.Equal(Visibility.Visible, GetField<System.Windows.Shapes.Rectangle>(cropOverlay, "_cropBorderVisual").Visibility);

                MainWindow topmost = GetTopmostWindow(first, second);
                Point cursorPosition = topmost.PointToScreen(new Point(100, 100));
                Assert.True(first.HandleSelectionModeMouseWheel(-120, ModifierKeys.None, cursorPosition).GetAwaiter().GetResult());
                Assert.Same(first, Assert.Single(GlobalMouseModeManager.GetSelectedWindows()));
                Assert.NotSame(topmost, GetTopmostWindow(first, second));
                Assert.True(first.HandleSelectionModeMouseWheel(-120, ModifierKeys.None, cursorPosition).GetAwaiter().GetResult());
                Assert.Same(first, Assert.Single(GlobalMouseModeManager.GetSelectedWindows()));
                Assert.Same(topmost, GetTopmostWindow(first, second));

                second.Hide();
                GlobalMouseModeManager.ResetOverlapWindowCycle();
                Assert.True(first.HandleSelectionModeMouseWheel(-120, ModifierKeys.None, cursorPosition).GetAwaiter().GetResult());
                Assert.Same(first, Assert.Single(GlobalMouseModeManager.GetSelectedWindows()));

                Assert.True(first.HandleSelectionModeMouseWheel(-120, ModifierKeys.Control, cursorPosition).GetAwaiter().GetResult());
                Assert.Equal(2, settings.CurrentPage);
                Assert.True(first.HandleSelectionModeMouseWheel(120, ModifierKeys.Control, cursorPosition).GetAwaiter().GetResult());
                Assert.Equal(1, settings.CurrentPage);
            }
            finally
            {
                CloseTestWindows();
                GlobalMouseModeManager.SetMode(originalMouseMode);
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                ShutdownApplication(app);
            }
        });
    }

    [Fact]
    public void DraggingAnUnselectedWindowInSelectionModeMovesItWithoutSelectingIt()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var originalMouseMode = GlobalMouseModeManager.CurrentMode;
            MainWindow? window = null;
            try
            {
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true)
                {
                    Left = 120,
                    Top = 120
                };
                window.Show();
                GlobalMouseModeManager.ClearSelection();
                GlobalMouseModeManager.SetMode(CloudlessMouseMode.Selection);
                SetField(window, "selectionDragInitialWindowPosition", new Point(window.Left, window.Top));

                var moveMethod = typeof(MainWindow).GetMethod("MoveUnselectedWindow", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(moveMethod);
                moveMethod!.Invoke(window, new object[] { new Vector(80, 60) });

                Assert.Equal(200, window.Left);
                Assert.Equal(180, window.Top);
                Assert.False(GlobalMouseModeManager.IsSelected(window));

                SetField(window, "isSelectionDragPending", true);
                SetField(window, "selectionDragThresholdCrossed", true);
                var mouseUp = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Mouse.MouseUpEvent
                };
                var mouseUpMethod = typeof(MainWindow).GetMethod("Window_MouseUp", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(mouseUpMethod);
                mouseUpMethod!.Invoke(window, new object[] { window, mouseUp });
                Assert.False(GlobalMouseModeManager.IsSelected(window));
            }
            finally
            {
                CloseTestWindows();
                GlobalMouseModeManager.ClearSelection();
                GlobalMouseModeManager.SetMode(originalMouseMode);
                ShutdownApplication(app);
            }
        });
    }

    private static MainWindow GetTopmostWindow(params MainWindow[] windows)
    {
        var byHandle = windows.ToDictionary(window => new System.Windows.Interop.WindowInteropHelper(window).Handle);
        IntPtr handle = NativeMethods.GetTopWindow(IntPtr.Zero);
        while (handle != IntPtr.Zero)
        {
            if (byHandle.TryGetValue(handle, out MainWindow? window))
                return window;
            handle = NativeMethods.GetWindow(handle, NativeMethods.GW_HWNDNEXT);
        }

        throw new InvalidOperationException("The test windows were not found in the native z-order.");
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
