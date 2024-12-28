using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Cloudless
{
    public partial class ConfigurationWindow : Window
    {
        public string SelectedDisplayMode { get; private set; }
        public bool ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle { get; private set; }
        public int SpaceAroundBounds {  get; private set; }
        public bool ResizeWindowToNewImageWhenOpeningThroughApp {  get; private set; }
        public bool BorderOnMainWindow { get; private set; }
        public bool LoopGifs { get; private set; }
        public bool MuteMessages { get; private set; }
        public bool MakeBackgroundTransparent { get; private set; }
        public bool AlwaysOnTopByDefault { get; private set; }
        public double MaxCompressedCopySizeMB { get; private set; }

        public ConfigurationWindow()
        {
            InitializeComponent();

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
            
            var currentMakeBackgroundTransparent = Cloudless.Properties.Settings.Default.MakeBackgroundTransparent;
            MakeBackgroundTransparentCheckbox.IsChecked = currentMakeBackgroundTransparent;
            MakeBackgroundTransparent = currentMakeBackgroundTransparent;
            
            var currentMaxCompressedCopySizeMB = Cloudless.Properties.Settings.Default.MaxCompressedCopySizeMB;
            MaxCompressedCopySizeMBTextBox.Text = currentMaxCompressedCopySizeMB.ToString();
            MaxCompressedCopySizeMB = currentMaxCompressedCopySizeMB;
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

            ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle = ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggleCheckbox.IsChecked ?? false;

            var parsed = int.TryParse(SpaceAroundBoundsTextBox.Text.Trim(), out int space);
            SpaceAroundBounds = parsed ? space : 0;

            ResizeWindowToNewImageWhenOpeningThroughApp = ResizeWindowToNewImageWhenOpeningThroughAppCheckbox.IsChecked ?? false;

            BorderOnMainWindow = BorderOnMainWindowCheckbox.IsChecked ?? false;

            LoopGifs = LoopGifsCheckbox.IsChecked ?? false;
            MuteMessages = MuteMessagesCheckbox.IsChecked ?? false;
            MakeBackgroundTransparent = MakeBackgroundTransparentCheckbox.IsChecked ?? false;
            AlwaysOnTopByDefault = AlwaysOnTopByDefaultCheckbox.IsChecked ?? false;

            var parsedSize = double.TryParse(MaxCompressedCopySizeMBTextBox.Text.Trim(), out double size);
            MaxCompressedCopySizeMB = parsedSize ? size : 10.0;

            DialogResult = true;
            Close();
        }
    }
}
