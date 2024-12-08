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
        private readonly string _originalCopyButtonText;
        private CancellationTokenSource? _copyAnimationCancellationTokenSource;

        public ImageInfoWindow(string imagePath)
        {
            InitializeComponent();
            _imagePath = imagePath;
            _originalCopyButtonText = CopyButton.Content.ToString() ?? "";
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
            // Cancel any previous timer if the button is clicked again quickly
            _copyAnimationCancellationTokenSource?.Cancel();
            _copyAnimationCancellationTokenSource = new CancellationTokenSource();

            // Copy the path to the clipboard
            Clipboard.SetText(_imagePath);

            // Get the button and change its text
            Button? button = sender as Button;
            if (button != null)
            {
                button.Content = "Copied!";

                try
                {
                    // Wait for 1 second or until the cancellation is requested
                    await Task.Delay(1000, _copyAnimationCancellationTokenSource.Token);

                    // Restore the original text
                    button.Content = _originalCopyButtonText;
                }
                catch (TaskCanceledException)
                {
                    // Task was canceled, so just return and don't change the text back
                }
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
