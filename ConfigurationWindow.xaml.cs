using Cloudless.PluginBase;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Cloudless
{
    public partial class ConfigurationWindow : Window
    {
        public string SelectedDisplayMode { get; private set; }
        public string SelectedBackground { get; private set; }
        public string SelectedSortOrder { get; private set; }
        public bool ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle { get; private set; }
        public int SpaceAroundBounds {  get; private set; }
        public bool ResizeWindowToNewImageWhenOpeningThroughApp {  get; private set; }
        public bool BorderOnMainWindow { get; private set; }
        public bool LoopGifs { get; private set; }
        public bool MuteMessages { get; private set; }
        public bool AlwaysOnTopByDefault { get; private set; }
        public double MaxCompressedCopySizeMB { get; private set; }
        public bool DisableSmartZoom { get; private set; }
        public string ImgBBKey { get; private set; }
        public bool StartOnWindowsStart { get; private set; }
        public int MouseLongHoldMs { get; private set; }

        private MainWindow _mw;

        public ConfigurationWindow(MainWindow mw)
        {
            InitializeComponent();

            _mw = mw;

            var currentDisplayMode = Cloudless.Properties.Settings.Default.DisplayMode;
            // Set the current selection
            if (currentDisplayMode == "StretchToFit")
                DisplayModeDropdown.SelectedIndex = 0;
            else if (currentDisplayMode == "ZoomToFill")
                DisplayModeDropdown.SelectedIndex = 1;
            else if (currentDisplayMode == "BestFit")
                DisplayModeDropdown.SelectedIndex = 2;
            else if (currentDisplayMode == "BestFitWithoutZooming")
                DisplayModeDropdown.SelectedIndex = 3;
            SelectedDisplayMode = currentDisplayMode;

            var currentForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle = Cloudless.Properties.Settings.Default.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle;
            ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggleCheckbox.IsChecked = currentForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle;
            ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle = currentForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle;

            var currentSpaceAroundBounds = Cloudless.Properties.Settings.Default.PixelsSpaceAroundBounds;
            SpaceAroundBoundsTextBox.Text = currentSpaceAroundBounds.ToString();
            SpaceAroundBounds = currentSpaceAroundBounds;

            var currentResizeWindowToNewImageWhenOpeningThroughApp = Cloudless.Properties.Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp;
            ResizeWindowToNewImageWhenOpeningThroughAppCheckbox.IsChecked = currentResizeWindowToNewImageWhenOpeningThroughApp;
            ResizeWindowToNewImageWhenOpeningThroughApp = currentResizeWindowToNewImageWhenOpeningThroughApp;

            var currentBorderOnMainWindow = Cloudless.Properties.Settings.Default.BorderOnMainWindow;
            BorderOnMainWindowCheckbox.IsChecked = currentBorderOnMainWindow;
            BorderOnMainWindow = currentBorderOnMainWindow;

            var currentLoopGifs = Cloudless.Properties.Settings.Default.LoopGifs;
            LoopGifsCheckbox.IsChecked = currentLoopGifs;
            LoopGifs = currentLoopGifs;
            
            var currentMuteMessages = Cloudless.Properties.Settings.Default.MuteMessages;
            MuteMessagesCheckbox.IsChecked = currentMuteMessages;
            MuteMessages = currentMuteMessages;

            var currentAlwaysOnTopByDefault = Cloudless.Properties.Settings.Default.AlwaysOnTopByDefault;
            AlwaysOnTopByDefaultCheckbox.IsChecked = currentAlwaysOnTopByDefault;
            AlwaysOnTopByDefault = currentAlwaysOnTopByDefault;

            var currentDisableSmartZoom = Cloudless.Properties.Settings.Default.DisableSmartZoom;
            DisableSmartZoomCheckbox.IsChecked = currentDisableSmartZoom;
            DisableSmartZoom = currentDisableSmartZoom;

            var currentStartOnWindowsStartCheckbox = Cloudless.Properties.Settings.Default.StartOnWindowsStart;
            StartOnWindowsStartCheckbox.IsChecked = currentStartOnWindowsStartCheckbox;
            StartOnWindowsStart = currentStartOnWindowsStartCheckbox;

            var currentBackground = Cloudless.Properties.Settings.Default.Background;
            // Set the current selection
            if (currentBackground == "black")
                BackgroundDropdown.SelectedIndex = 0;
            else if (currentBackground == "white")
                BackgroundDropdown.SelectedIndex = 1;
            else if (currentBackground == "transparent")
                BackgroundDropdown.SelectedIndex = 2;
            SelectedBackground = currentBackground;

            var currentSortOrder = Cloudless.Properties.Settings.Default.ImageDirectorySortOrder;
            // Set the current selection
            if (currentSortOrder == "FileNameAscending")
                SortDropdown.SelectedIndex = 0;
            else if (currentSortOrder == "FileNameDescending")
                SortDropdown.SelectedIndex = 1;
            else if (currentSortOrder == "DateModifiedAscending")
                SortDropdown.SelectedIndex = 2;
            else if (currentSortOrder == "DateModifiedDescending")
                SortDropdown.SelectedIndex = 3;
            SelectedSortOrder = currentSortOrder;

            var currentMaxCompressedCopySizeMB = Cloudless.Properties.Settings.Default.MaxCompressedCopySizeMB;
            MaxCompressedCopySizeMBTextBox.Text = currentMaxCompressedCopySizeMB.ToString();
            MaxCompressedCopySizeMB = currentMaxCompressedCopySizeMB;

            var currentImgBBKey = Cloudless.Properties.Settings.Default.ImgBBKey;
            ImgBBKeyTextBox.Text = currentImgBBKey;
            ImgBBKey = currentImgBBKey;

            var currentMouseLongHoldMs = Cloudless.Properties.Settings.Default.MouseLongPressMS;
            MouseLongHoldMSTextBox.Text = currentMouseLongHoldMs.ToString();
            MouseLongHoldMs = currentMouseLongHoldMs;

            bool foundWebpPlugin = PluginManager.GetPluginForFiletype("webp") != null;
            bool foundVlcPlugin = PluginManager.GetPluginForFiletype("webm") != null;

            if (foundWebpPlugin)
            {
                WebpStatusText.Text = "WebP support is already installed.";  // TODO add: current version and new version to prompt for update
            }
            if (foundVlcPlugin)
            {
                VlcStatusText.Text = "WebM/MKV/MP4 support is already installed.";
            }
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private void Window_KeyDown(object sender, KeyEventArgs e) { WindowHelper.HandleKeyDown(this, e); }
        private void Cancel_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (DisplayModeDropdown.SelectedIndex == 0)
                SelectedDisplayMode = "StretchToFit";
            else if (DisplayModeDropdown.SelectedIndex == 1)
                SelectedDisplayMode = "ZoomToFill";
            else if (DisplayModeDropdown.SelectedIndex == 2)
                SelectedDisplayMode = "BestFit";
            else if (DisplayModeDropdown.SelectedIndex == 3)
                SelectedDisplayMode = "BestFitWithoutZooming";

            if (BackgroundDropdown.SelectedIndex == 0)
                SelectedBackground = "black";
            if (BackgroundDropdown.SelectedIndex == 1)
                SelectedBackground = "white";
            if (BackgroundDropdown.SelectedIndex == 2)
                SelectedBackground = "transparent";

            if (SortDropdown.SelectedIndex == 0)
                SelectedSortOrder = "FileNameAscending";
            else if (SortDropdown.SelectedIndex == 1)
                SelectedSortOrder = "FileNameDescending";
            else if (SortDropdown.SelectedIndex == 2)
                SelectedSortOrder = "DateModifiedAscending";
            else if (SortDropdown.SelectedIndex == 3)
                SelectedSortOrder = "DateModifiedDescending";


            ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle = ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggleCheckbox.IsChecked ?? false;

            var parsed = int.TryParse(SpaceAroundBoundsTextBox.Text.Trim(), out int space);
            SpaceAroundBounds = parsed ? space : 0;

            ResizeWindowToNewImageWhenOpeningThroughApp = ResizeWindowToNewImageWhenOpeningThroughAppCheckbox.IsChecked ?? false;

            BorderOnMainWindow = BorderOnMainWindowCheckbox.IsChecked ?? false;

            LoopGifs = LoopGifsCheckbox.IsChecked ?? false;
            MuteMessages = MuteMessagesCheckbox.IsChecked ?? false;
            AlwaysOnTopByDefault = AlwaysOnTopByDefaultCheckbox.IsChecked ?? false;
            DisableSmartZoom = DisableSmartZoomCheckbox.IsChecked ?? false;
            StartOnWindowsStart = StartOnWindowsStartCheckbox.IsChecked ?? false;

            var parsedSize = double.TryParse(MaxCompressedCopySizeMBTextBox.Text.Trim(), out double size);
            MaxCompressedCopySizeMB = parsedSize ? size : 10.0;
            ImgBBKey = ImgBBKeyTextBox.Text.Trim();
            if (int.TryParse(MouseLongHoldMSTextBox.Text, out int ms))
                MouseLongHoldMs = ms;

            DialogResult = true;
            Close();
        }

        private void SetDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            OpenDefaultAppsSettings();
        }

        private void ManageGifCache_Click(object sender, RoutedEventArgs e)
        {
            string directory = Path.GetTempPath();
            string cloudlessTempPath = Path.Combine(directory, "CloudlessTempData");
            _mw.RevealDirectoryInExplorer(cloudlessTempPath);
        }

        private void OpenDefaultAppsSettings()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:defaultapps",
                UseShellExecute = true
            });
        }

        private async void InstallWebP_Click(object sender, RoutedEventArgs e)
        {
            WebpInstallButton.IsEnabled = false;

            var progress = new Progress<string>(msg =>
            {
                WebpStatusText.Text = msg; // TextBlock in UI
            });

            var success = await PluginManager.InstallPluginAsync(
                pluginName: "WebP",
                downloadUrl: "https://raw.github.com/ktschroeder/Cloudless/master/Cloudless.WebpPlugin/HostedPlugin/WebPPlugin.zip",
                progress: progress);

            if (success)
            {
                WebpStatusText.Text = "WebP support installed!";

                // Optional: load plugin immediately
                //PluginManager.LoadPlugins();
            }
            else
            {
                //WebpStatusText.Text = "Installation failed: " + progress.ToString();
            }

            WebpInstallButton.IsEnabled = true;
        }

        private async void InstallVlc_Click(object sender, RoutedEventArgs e)
        {
            VlcInstallButton.IsEnabled = false;

            var progress = new Progress<string>(msg =>
            {
                VlcStatusText.Text = msg; // TextBlock in UI
            });

            var success = await PluginManager.InstallPluginAsync(
                pluginName: "Vlc",
                downloadUrl: "https://raw.github.com/ktschroeder/Cloudless/master/Cloudless.VlcPlugin/HostedPlugin/VlcPlugin_0.zip",
                progress: progress);

            // TODO clean this up
            success = await PluginManager.InstallPluginAsync(
                pluginName: "Vlc",
                downloadUrl: "https://raw.github.com/ktschroeder/Cloudless/master/Cloudless.VlcPlugin/HostedPlugin/VlcPlugin_1.zip",
                progress: progress,
                continuingInstallInParts: true);  // this tells the installer to not delete the plugin folder

            if (success)
            {
                VlcStatusText.Text = "WebM/MKV/MP4 support installed!";

                // Optional: load plugin immediately
                //PluginManager.LoadPlugins();
            }
            else
            {
                //VlcStatusText.Text = "Installation failed: " + progress.ToString();
            }

            VlcInstallButton.IsEnabled = true;
        }
    }
}
