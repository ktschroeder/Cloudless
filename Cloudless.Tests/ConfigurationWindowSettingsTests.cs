using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Cloudless.Tests;

[Collection("WPF command palette")]
[Trait("Category", "InteractiveUI")]
public sealed class ConfigurationWindowSettingsTests
{
    private static readonly string[] SettingsUnderTest =
    {
        "DisplayMode", "ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle", "PixelsSpaceAroundBounds",
        "ResizeWindowToNewImageWhenOpeningThroughApp", "BorderOnMainWindow", "SlightlyTransparentSecondaryWindows",
        "ComicModeTopRight", "LoopGifs", "MuteMessages", "AlwaysOnTopByDefault", "MaxCompressedCopySizeMB",
        "Background", "ImageDirectorySortOrder", "CommandPaletteValidCommandIndicator", "DisableSmartZoom", "DisableZenMode", "ImgBBKey",
        "StartOnWindowsStart", "MouseLongPressMS", "PreloadImages", "ComicModeMouseControlScroll",
        "FilmStripCloseAfterward", "FilmStripOpenImageInNewWindow", "StartVideosMuted",
        "ResumeVideosFromPreviousPosition", "UseManualVideoControls", "Theme"
    };

    [Fact]
    public void PreferencesLoadFromAndPersistToUserSettings()
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
            var originalSettings = SettingsUnderTest.ToDictionary(name => name, name => settings[name]);
            int originalCurrentPage = settings.CurrentPage;
            MainWindow? mainWindow = null;
            ConfigurationWindow? configurationWindow = null;
            try
            {
                settings.CurrentPage = 1;
                SetInitialSettings(settings);
                mainWindow = new MainWindow(string.Empty, 800, 600, workspaceLoad: true);
                configurationWindow = new ConfigurationWindow(mainWindow);

                AssertLoadedControlValues(configurationWindow);
                SetEditedControlValues(configurationWindow);
                configurationWindow.ReadSettingsFromControls();
                AssertConfigurationProperties(configurationWindow);

                MainWindow.PersistConfigurationSettings(configurationWindow);
                AssertPersistedSettings(settings);
            }
            finally
            {
                foreach (Window window in app.Windows.OfType<Window>().ToArray())
                    window.Close();
                foreach (var setting in originalSettings)
                    settings[setting.Key] = setting.Value;
                settings.CurrentPage = originalCurrentPage;
                settings.Save();
                app.Shutdown();
                App.SuppressStartupForUiTests = false;
                ResetWpfApplicationSingletonForTestIsolation();
            }
        });
    }

    private static void SetInitialSettings(Cloudless.Properties.Settings settings)
    {
        settings.DisplayMode = "ZoomToFill";
        settings.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle = false;
        settings.PixelsSpaceAroundBounds = 17;
        settings.ResizeWindowToNewImageWhenOpeningThroughApp = true;
        settings.BorderOnMainWindow = false;
        settings.SlightlyTransparentSecondaryWindows = false;
        settings.CommandPaletteValidCommandIndicator = "None";
        settings.ComicModeTopRight = false;
        settings.LoopGifs = true;
        settings.MuteMessages = false;
        settings.AlwaysOnTopByDefault = false;
        settings.MaxCompressedCopySizeMB = 11.5;
        settings.Background = "white";
        settings.ImageDirectorySortOrder = "DateModifiedAscending";
        settings.DisableSmartZoom = false;
        settings.DisableZenMode = true;
        settings.ImgBBKey = "initial-key";
        settings.StartOnWindowsStart = false;
        settings.MouseLongPressMS = 275;
        settings.PreloadImages = false;
        settings.ComicModeMouseControlScroll = false;
        settings.FilmStripCloseAfterward = false;
        settings.FilmStripOpenImageInNewWindow = false;
        settings.StartVideosMuted = false;
        settings.ResumeVideosFromPreviousPosition = true;
        settings.UseManualVideoControls = false;
        settings["Theme"] = "Light";
    }

    private static void AssertLoadedControlValues(ConfigurationWindow window)
    {
        Assert.Equal(1, Get<ComboBox>(window, "DisplayModeDropdown").SelectedIndex);
        Assert.Equal(1, Get<ComboBox>(window, "BackgroundDropdown").SelectedIndex);
        Assert.Equal(2, Get<ComboBox>(window, "SortDropdown").SelectedIndex);
        Assert.Equal(0, Get<ComboBox>(window, "CommandPaletteValidCommandIndicatorDropdown").SelectedIndex);
        Assert.Equal("Light", window.SelectedTheme);
        Assert.Equal("17", Get<TextBox>(window, "SpaceAroundBoundsTextBox").Text);
        Assert.Equal("11.5", Get<TextBox>(window, "MaxCompressedCopySizeMBTextBox").Text);
        Assert.Equal("initial-key", Get<TextBox>(window, "ImgBBKeyTextBox").Text);
        Assert.Equal("275", Get<TextBox>(window, "MouseLongHoldMSTextBox").Text);

        AssertCheckbox(window, "ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggleCheckbox", false);
        AssertCheckbox(window, "ResizeWindowToNewImageWhenOpeningThroughAppCheckbox", true);
        AssertCheckbox(window, "BorderOnMainWindowCheckbox", false);
        AssertCheckbox(window, "SlightlyTransparentSecondaryWindowsCheckbox", false);
        AssertCheckbox(window, "ComicModeTopRightCheckbox", false);
        AssertCheckbox(window, "LoopGifsCheckbox", true);
        AssertCheckbox(window, "MuteMessagesCheckbox", false);
        AssertCheckbox(window, "AlwaysOnTopByDefaultCheckbox", false);
        AssertCheckbox(window, "DisableSmartZoomCheckbox", false);
        AssertCheckbox(window, "DisableZenModeCheckbox", true);
        AssertCheckbox(window, "StartOnWindowsStartCheckbox", false);
        AssertCheckbox(window, "PreloadImagesCheckbox", false);
        AssertCheckbox(window, "ComicModeMouseControlScrollCheckbox", false);
        AssertCheckbox(window, "FilmStripCloseAfterwardCheckbox", false);
        AssertCheckbox(window, "FilmStripOpenImageInNewWindowCheckbox", false);
        AssertCheckbox(window, "StartVideosMutedCheckbox", false);
        AssertCheckbox(window, "ResumeVideosFromPreviousPositionCheckbox", true);
        AssertCheckbox(window, "UseManualVideoControlsCheckbox", false);
    }

    private static void SetEditedControlValues(ConfigurationWindow window)
    {
        Get<ComboBox>(window, "DisplayModeDropdown").SelectedIndex = 3;
        Get<ComboBox>(window, "BackgroundDropdown").SelectedIndex = 2;
        Get<ComboBox>(window, "SortDropdown").SelectedIndex = 3;
        Get<ComboBox>(window, "CommandPaletteValidCommandIndicatorDropdown").SelectedIndex = 2;
        Get<ComboBox>(window, "ThemeDropdown").SelectedIndex = 1;
        Get<TextBox>(window, "SpaceAroundBoundsTextBox").Text = "42";
        Get<TextBox>(window, "MaxCompressedCopySizeMBTextBox").Text = "23.5";
        Get<TextBox>(window, "ImgBBKeyTextBox").Text = "edited-key";
        Get<TextBox>(window, "MouseLongHoldMSTextBox").Text = "480";

        SetCheckbox(window, "ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggleCheckbox", true);
        SetCheckbox(window, "ResizeWindowToNewImageWhenOpeningThroughAppCheckbox", false);
        SetCheckbox(window, "BorderOnMainWindowCheckbox", true);
        SetCheckbox(window, "SlightlyTransparentSecondaryWindowsCheckbox", true);
        SetCheckbox(window, "ComicModeTopRightCheckbox", true);
        SetCheckbox(window, "LoopGifsCheckbox", false);
        SetCheckbox(window, "MuteMessagesCheckbox", true);
        SetCheckbox(window, "AlwaysOnTopByDefaultCheckbox", true);
        SetCheckbox(window, "DisableSmartZoomCheckbox", true);
        SetCheckbox(window, "DisableZenModeCheckbox", false);
        SetCheckbox(window, "StartOnWindowsStartCheckbox", true);
        SetCheckbox(window, "PreloadImagesCheckbox", true);
        SetCheckbox(window, "ComicModeMouseControlScrollCheckbox", true);
        SetCheckbox(window, "FilmStripCloseAfterwardCheckbox", true);
        SetCheckbox(window, "FilmStripOpenImageInNewWindowCheckbox", true);
        SetCheckbox(window, "StartVideosMutedCheckbox", true);
        SetCheckbox(window, "ResumeVideosFromPreviousPositionCheckbox", false);
        SetCheckbox(window, "UseManualVideoControlsCheckbox", true);
    }

    private static void AssertConfigurationProperties(ConfigurationWindow window)
    {
        Assert.Equal("BestFitWithoutZooming", window.SelectedDisplayMode);
        Assert.Equal("transparent", window.SelectedBackground);
        Assert.Equal("DateModifiedDescending", window.SelectedSortOrder);
        Assert.Equal("GreenBorder", window.SelectedCommandPaletteValidCommandIndicator);
        Assert.Equal("Dark", window.SelectedTheme);
        Assert.True(window.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle);
        Assert.Equal(42, window.SpaceAroundBounds);
        Assert.False(window.ResizeWindowToNewImageWhenOpeningThroughApp);
        Assert.True(window.BorderOnMainWindow);
        Assert.True(window.SlightlyTransparentSecondaryWindows);
        Assert.True(window.ComicModeTopRight);
        Assert.False(window.LoopGifs);
        Assert.True(window.MuteMessages);
        Assert.True(window.AlwaysOnTopByDefault);
        Assert.Equal(23.5, window.MaxCompressedCopySizeMB);
        Assert.True(window.DisableSmartZoom);
        Assert.False(window.DisableZenMode);
        Assert.Equal("edited-key", window.ImgBBKey);
        Assert.True(window.StartOnWindowsStart);
        Assert.Equal(480, window.MouseLongHoldMs);
        Assert.True(window.PreloadImages);
        Assert.True(window.ComicModeMouseControlScroll);
        Assert.True(window.FilmStripCloseAfterward);
        Assert.True(window.FilmStripOpenImageInNewWindow);
        Assert.True(window.StartVideosMuted);
        Assert.False(window.ResumeVideosFromPreviousPosition);
        Assert.True(window.UseManualVideoControls);
    }

    private static void AssertPersistedSettings(Cloudless.Properties.Settings settings)
    {
        Assert.Equal("BestFitWithoutZooming", settings.DisplayMode);
        Assert.True(settings.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle);
        Assert.Equal(42, settings.PixelsSpaceAroundBounds);
        Assert.False(settings.ResizeWindowToNewImageWhenOpeningThroughApp);
        Assert.True(settings.BorderOnMainWindow);
        Assert.True(settings.SlightlyTransparentSecondaryWindows);
        Assert.True(settings.ComicModeTopRight);
        Assert.False(settings.LoopGifs);
        Assert.True(settings.MuteMessages);
        Assert.True(settings.AlwaysOnTopByDefault);
        Assert.Equal(23.5, settings.MaxCompressedCopySizeMB);
        Assert.Equal("transparent", settings.Background);
        Assert.Equal("DateModifiedDescending", settings.ImageDirectorySortOrder);
        Assert.Equal("GreenBorder", settings.CommandPaletteValidCommandIndicator);
        Assert.True(settings.DisableSmartZoom);
        Assert.False(settings.DisableZenMode);
        Assert.Equal("edited-key", settings.ImgBBKey);
        Assert.True(settings.StartOnWindowsStart);
        Assert.Equal(480, settings.MouseLongPressMS);
        Assert.True(settings.PreloadImages);
        Assert.True(settings.ComicModeMouseControlScroll);
        Assert.True(settings.FilmStripCloseAfterward);
        Assert.True(settings.FilmStripOpenImageInNewWindow);
        Assert.True(settings.StartVideosMuted);
        Assert.False(settings.ResumeVideosFromPreviousPosition);
        Assert.True(settings.UseManualVideoControls);
        Assert.Equal("Dark", settings["Theme"]);
    }

    private static T Get<T>(ConfigurationWindow window, string name) where T : class =>
        Assert.IsType<T>(window.FindName(name));

    private static void AssertCheckbox(ConfigurationWindow window, string name, bool expected) =>
        Assert.Equal(expected, Get<CheckBox>(window, name).IsChecked);

    private static void SetCheckbox(ConfigurationWindow window, string name, bool value) =>
        Get<CheckBox>(window, name).IsChecked = value;

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
            throw new Xunit.Sdk.XunitException($"Configuration settings UI test failed: {failure}");
    }
}
