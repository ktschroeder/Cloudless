using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class SecondaryWindowOpacityTests
{
    [Fact]
    public void PreferenceAppliesToSecondaryWindowsButNotMainWindows()
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
            bool originalSetting = settings.SlightlyTransparentSecondaryWindows;
            int originalCurrentPage = settings.CurrentPage;
            MainWindow? mainWindow = null;
            Window? secondaryWindow = null;
            try
            {
                settings.CurrentPage = 1;
                settings.SlightlyTransparentSecondaryWindows = true;

                mainWindow = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                mainWindow.Show();
                var configurationWindow = new ConfigurationWindow(mainWindow);
                secondaryWindow = new AboutWindow("test");
                secondaryWindow.Show();

                Assert.Equal(1.0, mainWindow.Opacity);
                Assert.Equal(0.9, secondaryWindow.Opacity);

                var checkbox = Assert.IsType<CheckBox>(configurationWindow.FindName("SlightlyTransparentSecondaryWindowsCheckbox"));
                Assert.True(checkbox.IsChecked);

                settings.SlightlyTransparentSecondaryWindows = false;
                SecondaryWindowOpacity.ApplyToOpenWindows();
                Assert.Equal(1.0, secondaryWindow.Opacity);
                Assert.Equal(1.0, mainWindow.Opacity);
            }
            finally
            {
                foreach (Window window in app.Windows.OfType<Window>().ToArray())
                    window.Close();
                settings.SlightlyTransparentSecondaryWindows = originalSetting;
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
            throw new Xunit.Sdk.XunitException($"Secondary window opacity UI test failed: {failure}");
    }
}
