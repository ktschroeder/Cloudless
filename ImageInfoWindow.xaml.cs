using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimpleImageViewer
{
    public partial class ImageInfoWindow : Window
    {
        private readonly string _imagePath;

        public ImageInfoWindow(string imagePath)
        {
            InitializeComponent();
            _imagePath = imagePath;
            LoadImageInfo();
        }

        private void LoadImageInfo()
        {
            if (string.IsNullOrEmpty(_imagePath))
                return;

            // Set file info
            var fileInfo = new FileInfo(_imagePath);
            FilenameText.Text = $"Filename: {Path.GetFileName(_imagePath)}";
            PathText.Text = $"Path: {_imagePath}";
            SizeText.Text = $"Size: {fileInfo.Length / 1024.0:0.##} KB";

            // Set format and dimensions
            var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(_imagePath));
            DimensionsText.Text = $"Dimensions: {bitmap.PixelWidth} x {bitmap.PixelHeight}";
            FormatText.Text = $"Format: {fileInfo.Extension.ToUpperInvariant().TrimStart('.')}";
        }

        private async void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            // Copy the path to the clipboard
            Clipboard.SetText(_imagePath);

            // Get the button and change its text
            Button? button = sender as Button;
            if (button != null)
            {
                string originalContent = button.Content.ToString() ?? "";
                button.Content = "Copied!";

                // Wait for 1 second (1000ms)
                await Task.Delay(1000);

                // Restore the original text
                button.Content = originalContent;
            }
        }


        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.C)
            {
                Close();
                e.Handled = true;
                return;
            }
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
    }
}
