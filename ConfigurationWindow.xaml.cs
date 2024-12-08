using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimpleImageViewer
{
    public partial class ConfigurationWindow : Window
    {
        public string SelectedDisplayMode { get; private set; }

        public ConfigurationWindow(string currentDisplayMode)
        {
            InitializeComponent();

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
        private bool IsControlClicked(MouseButtonEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(this, e.GetPosition(this));

            // Check if the hit test is on a Button or ComboBox (any other controls you want to exclude)
            if (hit?.VisualHit is Button || hit?.VisualHit is ComboBox)
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

            DialogResult = true;
            Close();
        }



        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            //DialogResult = true;
            Close();
        }
    }
}
