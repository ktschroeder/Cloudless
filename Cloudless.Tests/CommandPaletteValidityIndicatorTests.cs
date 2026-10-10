using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class CommandPaletteValidityIndicatorTests
{
    [Fact]
    public void SelectedIndicatorAppearsForValidCommandsAndDisappearsForInvalidInput()
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
            string originalIndicator = settings.CommandPaletteValidCommandIndicator;
            int originalCurrentPage = settings.CurrentPage;
            MainWindow? mainWindow = null;
            try
            {
                settings.CurrentPage = 1;
                mainWindow = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                mainWindow.Show();
                mainWindow.SimulateKeyEvent(System.Windows.Input.Key.OemSemicolon, shift: false, control: false, alt: false).GetAwaiter().GetResult();
                var palette = Assert.IsType<CommandPaletteWindow>(typeof(MainWindow)
                    .GetField("_commandPaletteWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(mainWindow));
                var control = palette.Control!;
                var textBox = control.CommandTextBoxControl;
                var checkmark = Assert.IsType<TextBlock>(control.FindName("CommandValidityCheckmark"));

                settings.CommandPaletteValidCommandIndicator = "TextGreen";
                textBox.Text = "close all";
                textBox.CaretIndex = textBox.Text.Length;
                mainWindow.UpdateCommandSuggestion();
                Brush greenTextBrush = textBox.Foreground;
                textBox.Text = "asdf";
                mainWindow.UpdateCommandSuggestion();
                Assert.NotSame(greenTextBrush, textBox.Foreground);

                settings.CommandPaletteValidCommandIndicator = "GreenBorder";
                textBox.Text = "goto start";
                mainWindow.UpdateCommandSuggestion();
                var validityBorder = Assert.IsType<Border>(control.FindName("CommandValidityBorder"));
                Assert.Equal(new Thickness(3), validityBorder.BorderThickness);
                Brush greenBorderBrush = validityBorder.BorderBrush;
                textBox.Text = "goto";
                mainWindow.UpdateCommandSuggestion();
                Assert.Equal(new Thickness(3), validityBorder.BorderThickness);
                Assert.NotSame(greenBorderBrush, validityBorder.BorderBrush);

                settings.CommandPaletteValidCommandIndicator = "Checkmark";
                textBox.Text = "sort name asc";
                mainWindow.UpdateCommandSuggestion();
                Assert.Equal(Visibility.Visible, checkmark.Visibility);
                textBox.Text = "sort name";
                mainWindow.UpdateCommandSuggestion();
                Assert.Equal(Visibility.Collapsed, checkmark.Visibility);

                settings.CommandPaletteValidCommandIndicator = "None";
                textBox.Text = "close all";
                mainWindow.UpdateCommandSuggestion();
                Assert.Equal(new Thickness(3), validityBorder.BorderThickness);
                Assert.Equal(Visibility.Collapsed, validityBorder.Visibility);
                Assert.NotSame(greenBorderBrush, validityBorder.BorderBrush);
                Assert.Equal(Visibility.Collapsed, checkmark.Visibility);
            }
            finally
            {
                foreach (Window window in app.Windows.OfType<Window>().ToArray())
                    window.Close();
                settings.CommandPaletteValidCommandIndicator = originalIndicator;
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
            }
        });
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
            throw new Xunit.Sdk.XunitException($"Command palette indicator UI test failed: {failure}");
    }
}
