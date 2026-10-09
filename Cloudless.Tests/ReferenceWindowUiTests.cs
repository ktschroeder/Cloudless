using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Cloudless.ReferenceData;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class ReferenceWindowUiTests
{
    [Fact]
    public void ReferenceWindows_RenderTheirTabsAndSupportKeyboardAndWheelNavigation()
    {
        RunOnStaThread(() =>
        {
            ResetWpfApplicationSingletonForTestIsolation();
            App.SuppressStartupForUiTests = true;
            var app = new App();
            app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                var hotkeyWindow = new HotkeyRefWindow();
                hotkeyWindow.Show();
                hotkeyWindow.UpdateLayout();
                var hotkeyTabs = GetTabControl(hotkeyWindow);
                Assert.Equal(HotkeyReferenceData.GetTabs().Select(tab => tab.Header),
                    hotkeyTabs.Items.OfType<TabItem>().Select(tab => tab.Header?.ToString()));
                Assert.All(hotkeyTabs.Items.OfType<TabItem>(), tab => Assert.IsType<ScrollViewer>(tab.Content));

                hotkeyTabs.SelectedIndex = 0;
                var left = CreateKeyEvent(hotkeyWindow, Key.Left);
                WindowHelper.HandleKeyDown(hotkeyWindow, left);
                Assert.Equal(hotkeyTabs.Items.Count - 1, hotkeyTabs.SelectedIndex);
                Assert.True(left.Handled);

                var right = CreateKeyEvent(hotkeyWindow, Key.Right);
                WindowHelper.HandleKeyDown(hotkeyWindow, right);
                Assert.Equal(0, hotkeyTabs.SelectedIndex);
                Assert.True(right.Handled);

                var previousTabWheel = CreateMouseWheelEvent(120);
                WindowHelper.HandleMouseWheel(hotkeyWindow, previousTabWheel);
                Assert.Equal(hotkeyTabs.Items.Count - 1, hotkeyTabs.SelectedIndex);
                Assert.True(previousTabWheel.Handled);

                var nextTabWheel = CreateMouseWheelEvent(-120);
                WindowHelper.HandleMouseWheel(hotkeyWindow, nextTabWheel);
                Assert.Equal(0, hotkeyTabs.SelectedIndex);
                Assert.True(nextTabWheel.Handled);

                var unrelatedKey = CreateKeyEvent(hotkeyWindow, Key.F1);
                WindowHelper.HandleKeyDown(hotkeyWindow, unrelatedKey);
                Assert.False(unrelatedKey.Handled);
                Assert.True(hotkeyWindow.IsVisible);
                hotkeyWindow.Close();

                var commandWindow = new CommandRefWindow();
                commandWindow.Show();
                commandWindow.UpdateLayout();
                var commandTabs = GetTabControl(commandWindow);
                Assert.Equal(CommandReferenceData.GetTabs().Select(tab => tab.Header),
                    commandTabs.Items.OfType<TabItem>().Select(tab => tab.Header?.ToString()));
                Assert.All(commandTabs.Items.OfType<TabItem>(), tab => Assert.IsType<ScrollViewer>(tab.Content));

                commandTabs.SelectedIndex = commandTabs.Items.Count - 1;
                var aKey = CreateKeyEvent(commandWindow, Key.A);
                WindowHelper.HandleKeyDown(commandWindow, aKey);
                Assert.Equal(commandTabs.Items.Count - 2, commandTabs.SelectedIndex);
                Assert.True(aKey.Handled);

                var escape = CreateKeyEvent(commandWindow, Key.Escape);
                WindowHelper.HandleKeyDown(commandWindow, escape);
                Assert.True(escape.Handled);
                Assert.False(commandWindow.IsVisible);

                var plainWindow = new Window { Content = new Grid() };
                plainWindow.Show();
                var unhandledWheel = CreateMouseWheelEvent(120);
                WindowHelper.HandleMouseWheel(plainWindow, unhandledWheel);
                Assert.False(unhandledWheel.Handled);
                plainWindow.Close();
            }
            finally
            {
                foreach (var window in Application.Current.Windows.OfType<Window>().ToArray())
                    window.Close();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
            }
        });
    }

    private static TabControl GetTabControl(Window window)
    {
        var field = window.GetType().GetField("TabControlReference", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(field);
        return Assert.IsType<TabControl>(field!.GetValue(window));
    }

    private static KeyEventArgs CreateKeyEvent(Window window, Key key)
    {
        var source = System.Windows.PresentationSource.FromVisual(window);
        Assert.NotNull(source);
        return new KeyEventArgs(Keyboard.PrimaryDevice, source!, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
    }

    private static MouseWheelEventArgs CreateMouseWheelEvent(int delta) =>
        new(Mouse.PrimaryDevice, Environment.TickCount, delta)
        {
            RoutedEvent = Mouse.MouseWheelEvent
        };

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
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            throw new Xunit.Sdk.XunitException($"STA reference-window UI test failed: {failure}");
    }
}
