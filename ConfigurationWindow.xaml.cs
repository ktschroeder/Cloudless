using System.Windows;

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
                StretchToFit.IsChecked = true;
            else if (currentDisplayMode == "ZoomToFill")
                ZoomToFill.IsChecked = true;
            else if (currentDisplayMode == "BestFit")
                BestFit.IsChecked = true;
            else if (currentDisplayMode == "BestFitWithoutZooming")
                BestFitWithoutZooming.IsChecked = true;

            SelectedDisplayMode = currentDisplayMode;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (StretchToFit.IsChecked == true)
                SelectedDisplayMode = "StretchToFit";
            else if (ZoomToFill.IsChecked == true)
                SelectedDisplayMode = "ZoomToFill";
            else if (BestFit.IsChecked == true)
                SelectedDisplayMode = "BestFit";
            else if (BestFitWithoutZooming.IsChecked == true)
                SelectedDisplayMode = "BestFitWithoutZooming";

            DialogResult = true;
            Close();
        }
    }
}
