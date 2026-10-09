using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class CommandPaletteInteractionUiTests
{
    [Fact]
    public void PaletteHistoryEnterAndEscapeBehaveThroughTextBoxKeyboardEvents()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalHistory = settings.CommandHistory?.Cast<string>().ToArray() ?? Array.Empty<string>();
            var originalCurrentPage = settings.CurrentPage;
            MainWindow? window = null;
            try
            {
                settings.CurrentPage = 1;
                settings.CommandHistory = ToStringCollection("echo older", "echo newer");
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                OpenPalette(window);
                var textBox = GetCommandTextBox(window);

                RaiseKey(textBox, Key.Up, preview: true);
                Assert.Equal("echo newer", textBox.Text);
                RaiseKey(textBox, Key.Up, preview: true);
                Assert.Equal("echo older", textBox.Text);
                RaiseKey(textBox, Key.Up, preview: true);
                Assert.Equal("echo older", textBox.Text);
                RaiseKey(textBox, Key.Down, preview: true);
                Assert.Equal("echo newer", textBox.Text);
                RaiseKey(textBox, Key.Down, preview: true);
                Assert.Equal(string.Empty, textBox.Text);

                textBox.Text = "echo submitted through palette";
                RaiseKey(textBox, Key.Enter);
                WaitForDispatcherCondition(
                    () => window.LastMessage == "echo: submitted through palette" && !GetPalette(window).IsVisible,
                    window.Dispatcher,
                    "the Enter key to execute and close the command palette");
                Assert.Equal("echo submitted through palette", settings.CommandHistory!.Cast<string>().Last());

                OpenPalette(window);
                textBox = GetCommandTextBox(window);
                textBox.Text = "echo must not execute";
                RaiseKey(textBox, Key.Escape);
                Assert.False(GetPalette(window).IsVisible);
                Assert.DoesNotContain("echo must not execute", settings.CommandHistory!.Cast<string>());

                window.SimulateKeyEvent(Key.OemSemicolon, shift: false, control: true, alt: false).GetAwaiter().GetResult();
                Assert.Equal("echo: submitted through palette", window.LastMessage);
            }
            finally
            {
                CloseTestWindows();
                settings.CommandHistory = ToStringCollection(originalHistory);
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                ShutdownApplication(app);
            }
        });
    }

    [Fact]
    public void WorkspaceAutocompleteCyclesSubstringMatchesAndRecencyInBothDirections()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            var originalRecentWorkspaces = settings.RecentWorkspaces?.Cast<string>().ToArray() ?? Array.Empty<string>();
            var prefix = "coverage-autocomplete-" + Guid.NewGuid().ToString("N");
            var firstName = prefix + "-a";
            var secondName = prefix + "-b";
            var firstPath = Path.Combine(MainWindow.workspaceFilesPath, firstName + ".cloudless");
            var secondPath = Path.Combine(MainWindow.workspaceFilesPath, secondName + ".cloudless");
            MainWindow? window = null;
            try
            {
                settings.CurrentPage = 1;
                WorkspacePersistence.Save(firstPath, new CloudlessWorkspace { WorkspaceName = firstName });
                WorkspacePersistence.Save(secondPath, new CloudlessWorkspace { WorkspaceName = secondName });
                settings.RecentWorkspaces = ToStringCollection(firstName, secondName);
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                OpenPalette(window);
                var textBox = GetCommandTextBox(window);
                textBox.Text = "ws load " + prefix;

                RaiseKey(textBox, Key.Tab);
                Assert.Equal("ws load " + firstName, textBox.Text);
                RaiseKey(textBox, Key.Tab);
                Assert.Equal("ws load " + secondName, textBox.Text);
                InvokeTabPressed(window, reverse: true, recency: false);
                Assert.Equal("ws load " + firstName, textBox.Text);

                textBox.Text = "ws load " + prefix;
                InvokeTabPressed(window, reverse: false, recency: true);
                Assert.Equal("ws load " + secondName, textBox.Text);
                InvokeTabPressed(window, reverse: true, recency: true);
                Assert.Equal("ws load " + firstName, textBox.Text);
            }
            finally
            {
                CloseTestWindows();
                if (File.Exists(firstPath))
                    File.Delete(firstPath);
                if (File.Exists(secondPath))
                    File.Delete(secondPath);
                settings.RecentWorkspaces = ToStringCollection(originalRecentWorkspaces);
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                ShutdownApplication(app);
            }
        });
    }

    [Fact]
    public void TagAutocompleteCyclesForwardAndBackwardThroughMatchingTags()
    {
        RunOnStaThread(() =>
        {
            var app = CreateApplication();
            var settings = Cloudless.Properties.Settings.Default;
            var originalCurrentPage = settings.CurrentPage;
            var uniquePrefix = "coverage-" + Guid.NewGuid().ToString("N");
            var firstTag = uniquePrefix + "-a";
            var secondTag = uniquePrefix + "-b";
            var tagSourcePath = Path.Combine(Path.GetTempPath(), uniquePrefix + ".jpg");
            MainWindow? window = null;
            try
            {
                settings.CurrentPage = 1;
                TagManager.Instance.AddTags(tagSourcePath, firstTag, secondTag);
                window = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                window.Show();
                OpenPalette(window);
                var textBox = GetCommandTextBox(window);
                textBox.Text = "fs tag " + uniquePrefix;

                var firstTab = RaiseKey(textBox, Key.Tab);
                Assert.True(firstTab.Handled);
                Assert.Equal("fs tag " + firstTag, textBox.Text);

                RaiseKey(textBox, Key.Tab);
                Assert.Equal("fs tag " + secondTag, textBox.Text);

                var tabPressed = typeof(MainWindow).GetMethod("TabPressed", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(tabPressed);
                tabPressed!.Invoke(window, new object[] { true, false });
                Assert.Equal("fs tag " + firstTag, textBox.Text);
            }
            finally
            {
                CloseTestWindows();
                TagManager.Instance.DestroyTag(firstTag);
                TagManager.Instance.DestroyTag(secondTag);
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                ShutdownApplication(app);
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

    private static void OpenPalette(MainWindow window) =>
        window.SimulateKeyEvent(Key.OemSemicolon, shift: false, control: false, alt: false).GetAwaiter().GetResult();

    private static TextBox GetCommandTextBox(MainWindow window) => GetPalette(window).Control!.CommandTextBoxControl;

    private static CommandPaletteWindow GetPalette(MainWindow window)
    {
        var field = typeof(MainWindow).GetField("_commandPaletteWindow", BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<CommandPaletteWindow>(field!.GetValue(window));
    }

    private static KeyEventArgs RaiseKey(TextBox textBox, Key key, bool preview = false)
    {
        var source = PresentationSource.FromVisual(textBox)
            ?? throw new InvalidOperationException("The command text box has no presentation source.");
        var keyEvent = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = preview ? Keyboard.PreviewKeyDownEvent : Keyboard.KeyDownEvent
        };
        textBox.RaiseEvent(keyEvent);
        return keyEvent;
    }

    private static StringCollection ToStringCollection(params string[] values)
    {
        var collection = new StringCollection();
        collection.AddRange(values);
        return collection;
    }

    private static void InvokeTabPressed(MainWindow window, bool reverse, bool recency)
    {
        var method = typeof(MainWindow).GetMethod("TabPressed", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(window, new object[] { reverse, recency });
    }

    private static void WaitForDispatcherCondition(Func<bool> condition, System.Windows.Threading.Dispatcher dispatcher, string operation)
    {
        if (condition())
            return;

        var frame = new System.Windows.Threading.DispatcherFrame();
        var completed = false;
        var checkTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(25)
        };
        var timeoutTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(5)
        };
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
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        checkTimer.Stop();
        timeoutTimer.Stop();
        Assert.True(completed, $"Timed out waiting for {operation}.");
    }

    private static void CloseTestWindows()
    {
        if (Application.Current == null)
            return;
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
            throw new Xunit.Sdk.XunitException($"Command palette interaction UI test failed: {failure}");
    }
}
