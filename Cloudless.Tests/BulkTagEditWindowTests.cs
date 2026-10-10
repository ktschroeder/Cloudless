using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class BulkTagEditWindowTests
{
    [Fact]
    public void SingleSelectionShowsCurrentTagsAndDoubleClickAddsWithoutDuplicates()
    {
        RunOnStaThread(() => WithEditor((manager, file) =>
        {
            manager.AddTags(file, "travel", "family");
            var editor = new BulkTagEditWindow(new[] { file }, manager);

            var panel = Assert.IsType<StackPanel>(editor.FindName("CurrentTagsPanel"));
            var currentTags = Assert.IsType<ListBox>(editor.FindName("CurrentTagsList"));
            var input = Assert.IsType<TextBox>(editor.FindName("TagsTextBox"));
            Assert.Equal(Visibility.Visible, panel.Visibility);
            Assert.Equal(new[] { "family", "travel" }, currentTags.Items.Cast<string>());

            currentTags.SelectedItem = "family";
            InvokePrivate(editor, "CurrentTagsList_MouseDoubleClick", currentTags,
                new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
            InvokePrivate(editor, "CurrentTagsList_MouseDoubleClick", currentTags,
                new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));

            Assert.Equal("family", input.Text);
        }));
    }

    [Fact]
    public void MultipleSelectionHidesCurrentFileTagReference()
    {
        RunOnStaThread(() => WithEditor((manager, file) =>
        {
            var editor = new BulkTagEditWindow(new[] { file, file + ".other" }, manager);

            var panel = Assert.IsType<StackPanel>(editor.FindName("CurrentTagsPanel"));
            Assert.Equal(Visibility.Collapsed, panel.Visibility);
        }));
    }

    [Fact]
    public void TabAutocompleteFindsSubstringMatchesAndCyclesInBothDirections()
    {
        RunOnStaThread(() => WithEditor((manager, file) =>
        {
            manager.AddTags(file, "travel", "traveler", "velvet");
            var editor = new BulkTagEditWindow(new[] { file, file + ".other" }, manager);
            var input = Assert.IsType<TextBox>(editor.FindName("TagsTextBox"));
            input.Text = "vel";
            input.CaretIndex = input.Text.Length;

            Assert.True(InvokeCycle(editor, reverse: false));
            Assert.Equal("travel", input.Text);
            Assert.True(InvokeCycle(editor, reverse: false));
            Assert.Equal("traveler", input.Text);
            Assert.True(InvokeCycle(editor, reverse: true));
            Assert.Equal("travel", input.Text);
        }));
    }

    [Fact]
    public void TagInputRejectsEmptyAndReservedKeywords()
    {
        RunOnStaThread(() => WithEditor((manager, file) =>
        {
            var editor = new BulkTagEditWindow(new[] { file }, manager);
            var input = Assert.IsType<TextBox>(editor.FindName("TagsTextBox"));
            var validation = Assert.IsType<TextBlock>(editor.FindName("ValidationText"));

            InvokePrivate(editor, "ApplyTags", false);
            Assert.Equal("Enter at least one tag.", validation.Text);
            Assert.Equal(Visibility.Visible, validation.Visibility);

            input.Text = "NOT";
            InvokePrivate(editor, "ApplyTags", false);
            Assert.Equal("'NOT' is reserved and cannot be used as a tag.", validation.Text);
        }));
    }

    private static bool InvokeCycle(BulkTagEditWindow editor, bool reverse)
    {
        var method = typeof(BulkTagEditWindow).GetMethod("CycleTagAutocomplete", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(editor, new object[] { reverse }));
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(target, arguments);
    }

    private static void WithEditor(Action<TagManager, string> action)
    {
        ResetWpfApplicationSingletonForTestIsolation();
        App.SuppressStartupForUiTests = true;
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ThemeManager.ApplyThemeGlobalResources("Dark");

        string testDirectory = Path.Combine(Path.GetTempPath(), "Cloudless.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        string file = Path.Combine(testDirectory, "sample.jpg");
        var manager = new TagManager(Path.Combine(testDirectory, "tags.json"));
        try
        {
            action(manager, file);
        }
        finally
        {
            foreach (Window window in app.Windows.OfType<Window>().ToArray())
                window.Close();
            ThemeManager.ApplyThemeGlobalResources(string.Empty);
            app.Shutdown();
            App.SuppressStartupForUiTests = false;
            ResetWpfApplicationSingletonForTestIsolation();
            Directory.Delete(testDirectory, recursive: true);
        }
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
            throw new Xunit.Sdk.XunitException($"Bulk tag editor UI test failed: {failure}");
    }
}
