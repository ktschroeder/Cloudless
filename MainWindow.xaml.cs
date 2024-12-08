using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SimpleImageViewer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            NoImageMessage.Visibility = Visibility.Visible;
            ImageDisplay.Visibility = Visibility.Collapsed;

            ApplyDisplayMode();
        }

        private void ApplyDisplayMode()
        {
            string displayMode = JustView.Properties.Settings.Default.DisplayMode;

            // Reset Width, Height, and Margin for all modes
            ImageDisplay.Width = Double.NaN; // Reset explicit width
            ImageDisplay.Height = Double.NaN; // Reset explicit height
            ImageDisplay.Margin = new Thickness(0); // Reset margin

            switch (displayMode)
            {
                case "StretchToFit":
                    ImageDisplay.Stretch = System.Windows.Media.Stretch.Fill;
                    break;
                case "ZoomToFill":
                    ImageDisplay.Stretch = System.Windows.Media.Stretch.UniformToFill;
                    break;
                case "BestFit":
                    ImageDisplay.Stretch = System.Windows.Media.Stretch.Uniform;
                    break;
                case "BestFitWithoutZooming":
                    ImageDisplay.Stretch = System.Windows.Media.Stretch.None; // Prevent automatic stretching
                    CenterImageIfNeeded(); // Center and scale the image as needed
                    break;
                default:
                    ImageDisplay.Stretch = System.Windows.Media.Stretch.Uniform; // Default to BestFit
                    break;
            }
        }


        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (JustView.Properties.Settings.Default.DisplayMode == "BestFitWithoutZooming")
            {
                CenterImageIfNeeded();
            }
        }

        private void CenterImageIfNeeded()
        {
            if (ImageDisplay.Source is BitmapSource bitmap)
            {
                double imageWidth = bitmap.PixelWidth;
                double imageHeight = bitmap.PixelHeight;
                double windowWidth = this.ActualWidth;
                double windowHeight = this.ActualHeight;

                // Calculate scaling factor to fit the image within the window
                double scaleX = windowWidth / imageWidth;
                double scaleY = windowHeight / imageHeight;

                // Ensure the image scales down if the window is smaller
                double scale = Math.Min(1, Math.Min(scaleX, scaleY)); // No upscaling

                // Apply scaled dimensions to ImageDisplay
                ImageDisplay.Width = imageWidth * scale;
                ImageDisplay.Height = imageHeight * scale;

                // Center the image display
                double marginX = (windowWidth - ImageDisplay.Width) / 2;
                double marginY = (windowHeight - ImageDisplay.Height) / 2;

                ImageDisplay.Margin = new Thickness(
                    Math.Max(0, marginX),
                    Math.Max(0, marginY),
                    Math.Max(0, marginX),
                    Math.Max(0, marginY)
                );

                // Ensure the image is not clipped by setting Stretch to Uniform
                ImageDisplay.Stretch = System.Windows.Media.Stretch.Uniform;
            }
        }





        private bool isDragging = false;
        private Point initialCursorPosition;

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Ensure the action is triggered only by the left mouse button
            if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                // If we're currently fullscreen, we don't want to exit unless dragging
                if (WindowState == WindowState.Maximized)
                {
                    // Capture the cursor position when clicking in fullscreen
                    initialCursorPosition = e.GetPosition(this);
                    // Flag that dragging has started (this flag will help us track dragging)
                    isDragging = true;
                }
                else
                {
                    // Otherwise, start dragging normally
                    DragMove();
                }
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            // If dragging is enabled (left button is pressed), we can check if fullscreen mode should be exited
            if (isDragging && WindowState == WindowState.Maximized)
            {
                // Calculate the distance moved from the initial cursor position
                Point cursorPosition = e.GetPosition(this);
                //double offsetX = cursorPosition.X - initialCursorPosition.X;
                //double offsetY = cursorPosition.Y - initialCursorPosition.Y;

                ToggleFullscreen();

                // Center the window on the cursor position by updating its Top and Left properties
                this.Left = cursorPosition.X - (this.ActualWidth / 2);
                this.Top = cursorPosition.Y - (this.ActualHeight / 2);

                // Exit fullscreen only when the user moves the mouse after clicking
                //ToggleFullscreen();
                DragMove();

            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // If the user stops dragging, reset the flag
            isDragging = false;
        }


        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var bitmap = new BitmapImage(new Uri(openFileDialog.FileName));
                ImageDisplay.Source = bitmap;

                // Show the image and hide the no-image message
                ImageDisplay.Visibility = Visibility.Visible;
                NoImageMessage.Visibility = Visibility.Collapsed;

                ApplyDisplayMode();
            }
        }

        private void OpenPreferences_Click(object sender, RoutedEventArgs e)
        {
            // Load current preferences
            string currentDisplayMode = JustView.Properties.Settings.Default.DisplayMode;

            var configWindow = new ConfigurationWindow(currentDisplayMode);
            if (configWindow.ShowDialog() == true)
            {
                // Save the new preference
                JustView.Properties.Settings.Default.DisplayMode = configWindow.SelectedDisplayMode;
                JustView.Properties.Settings.Default.Save();

                ApplyDisplayMode();
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                ToggleFullscreen();
            }
        }

        private void ToggleFullscreen()
        {
            if (WindowState == WindowState.Normal)
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Normal;
            }
        }

        // Handle right-click menu -> Exit
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Open context menu on right-click
        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImageContextMenu.IsOpen = true;
        }

        // Fullscreen toggle with F11
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                if (WindowState == WindowState.Normal)
                {
                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Normal;
                }
            }
        }

        // Handle resizing and edge dragging
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCLIENT = 1, HTCAPTION = 2;
            const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTBOTTOM = 15;
            const int HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

            if (msg == WM_NCHITTEST)
            {
                // Convert mouse coordinates
                int x = lParam.ToInt32() & 0xFFFF; // LOWORD
                int y = lParam.ToInt32() >> 16;    // HIWORD
                Point mousePos = new Point(x, y);

                // Get window rectangle
                Rect windowRect = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight);

                const int edgeThreshold = 10; // Edge detection threshold in pixels

                // Left edge
                if (mousePos.X >= windowRect.Left && mousePos.X < windowRect.Left + edgeThreshold)
                {
                    handled = true;
                    if (mousePos.Y >= windowRect.Top && mousePos.Y < windowRect.Top + edgeThreshold)
                        return (IntPtr)HTTOPLEFT;
                    if (mousePos.Y >= windowRect.Bottom - edgeThreshold && mousePos.Y <= windowRect.Bottom)
                        return (IntPtr)HTBOTTOMLEFT;
                    return (IntPtr)HTLEFT;
                }

                // Right edge
                if (mousePos.X >= windowRect.Right - edgeThreshold && mousePos.X <= windowRect.Right)
                {
                    handled = true;
                    if (mousePos.Y >= windowRect.Top && mousePos.Y < windowRect.Top + edgeThreshold)
                        return (IntPtr)HTTOPRIGHT;
                    if (mousePos.Y >= windowRect.Bottom - edgeThreshold && mousePos.Y <= windowRect.Bottom)
                        return (IntPtr)HTBOTTOMRIGHT;
                    return (IntPtr)HTRIGHT;
                }

                // Top edge
                if (mousePos.Y >= windowRect.Top && mousePos.Y < windowRect.Top + edgeThreshold)
                {
                    handled = true;
                    return (IntPtr)HTTOP;
                }

                // Bottom edge
                if (mousePos.Y >= windowRect.Bottom - edgeThreshold && mousePos.Y <= windowRect.Bottom)
                {
                    handled = true;
                    return (IntPtr)HTBOTTOM;
                }

                // Default behavior for client area
                handled = false; // Allow propagation for right-click menu
                return (IntPtr)HTCLIENT;
            }

            return IntPtr.Zero;
        }
    }
}
