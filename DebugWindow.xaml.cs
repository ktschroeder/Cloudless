//add hotkeys TODO

using SimpleImageViewer;
using System.Windows;

namespace JustView
{
    public partial class DebugWindow : Window
    {
        private readonly MainWindow mainWindow;

        public DebugWindow(MainWindow main)
        {
            InitializeComponent();
            mainWindow = main;

            // Initialize inputs with current values
            WindowWidthInput.Text = mainWindow.ActualWidth.ToString("F2");
            WindowHeightInput.Text = mainWindow.ActualHeight.ToString("F2");
            ImageWidthInput.Text = mainWindow.ImageDisplay.ActualWidth.ToString("F2");
            ImageHeightInput.Text = mainWindow.ImageDisplay.ActualHeight.ToString("F2");
            LeftMarginInput.Text = mainWindow.ImageDisplay.Margin.Left.ToString("F2");
            TopMarginInput.Text = mainWindow.ImageDisplay.Margin.Top.ToString("F2");
            ScaleXInput.Text = mainWindow.imageScaleTransform.ScaleX.ToString("F2");
            ScaleYInput.Text = mainWindow.imageScaleTransform.ScaleY.ToString("F2");
            TranslateXInput.Text = mainWindow.imageTranslateTransform.X.ToString("F2");
            TranslateYInput.Text = mainWindow.imageTranslateTransform.Y.ToString("F2");
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Apply window dimensions
                mainWindow.Width = double.Parse(WindowWidthInput.Text);
                mainWindow.Height = double.Parse(WindowHeightInput.Text);

                // Apply image dimensions (via scaling or resizing logic)
                mainWindow.ImageDisplay.Width = double.Parse(ImageWidthInput.Text);
                mainWindow.ImageDisplay.Height = double.Parse(ImageHeightInput.Text);

                // Apply margins
                mainWindow.ImageDisplay.Margin = new Thickness(
                    double.Parse(LeftMarginInput.Text),
                    double.Parse(TopMarginInput.Text),
                    double.Parse(LeftMarginInput.Text),
                    double.Parse(TopMarginInput.Text)
                );

                // Apply scale
                mainWindow.imageScaleTransform.ScaleX = double.Parse(ScaleXInput.Text);
                mainWindow.imageScaleTransform.ScaleY = double.Parse(ScaleYInput.Text);

                // Apply translation
                mainWindow.imageTranslateTransform.X = double.Parse(TranslateXInput.Text);
                mainWindow.imageTranslateTransform.Y = double.Parse(TranslateYInput.Text);

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying values: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
