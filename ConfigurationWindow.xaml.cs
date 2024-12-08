using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimpleImageViewer
{
    public partial class ConfigurationWindow : Window
    {
        public string SelectedDisplayMode { get; private set; }
        public bool ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle { get; private set; }
        public int SpaceAroundBounds {  get; private set; }
        public bool ResizeWindowToNewImageWhenOpeningThroughApp {  get; private set; }

        public ConfigurationWindow()
        {
            InitializeComponent();

            var currentDisplayMode = JustView.Properties.Settings.Default.DisplayMode;
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

            var currentForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle = JustView.Properties.Settings.Default.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle;
            ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggleCheckbox.IsChecked = currentForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle;
            ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle = currentForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle;

            var currentSpaceAroundBounds = JustView.Properties.Settings.Default.PixelsSpaceAroundBounds;
            SpaceAroundBoundsTextBox.Text = currentSpaceAroundBounds.ToString();
            SpaceAroundBounds = currentSpaceAroundBounds;

            var currentResizeWindowToNewImageWhenOpeningThroughApp = JustView.Properties.Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp;
            ResizeWindowToNewImageWhenOpeningThroughAppCheckbox.IsChecked = currentResizeWindowToNewImageWhenOpeningThroughApp;
            ResizeWindowToNewImageWhenOpeningThroughApp = currentResizeWindowToNewImageWhenOpeningThroughApp;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // If the left mouse button is pressed and it's not on a clickable element (e.g., buttons or ComboBox)
            if (e.ChangedButton == MouseButton.Left && !IsControlClicked(e))
            {
                // Allow the window to be dragged
                this.DragMove();
            }
        }

        // Helper method to determine if the click is on a clickable control like a button or dropdown
        private bool IsControlClicked(MouseButtonEventArgs e)  // TODO move to helper, duplicate code
        {
            var hit = VisualTreeHelper.HitTest(this, e.GetPosition(this));

            // Check if the hit test is on a Button or ComboBox (any other controls you want to exclude)
            if (hit?.VisualHit is Button || hit?.VisualHit is ComboBox || hit?.VisualHit is CheckBox || hit?.VisualHit is TextBox)
            {
                return true;
            }
            return false;
        }

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

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            //DialogResult = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.C)  // TODO user may try to copy text with CTRL C?
            {
                Close();
                e.Handled = true;
                return;
            }
        }
    }
}
