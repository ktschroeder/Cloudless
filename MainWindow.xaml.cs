using Microsoft.Win32;
using System;
using System.IO;
using System.Net.Http;
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
        private string? currentDirectory;
        private string[]? imageFiles;
        private int currentImageIndex;
        private string? currentlyDisplayedImagePath;
        private bool autoResizingSpaceIsToggled;

        public MainWindow(string? filePath)
        {
            Setup();

            if (filePath != null)
            {
                LoadImage(filePath, false);
                ResizeWindowToImage();
            }
        }

        public MainWindow()
        {
            Setup();
        }

        private void Setup()
        {
            InitializeComponent();

            NoImageMessage.Visibility = Visibility.Visible;
            ImageDisplay.Visibility = Visibility.Collapsed;

            ApplyDisplayMode();

            this.KeyDown += Window_KeyDown;

            UpdateContextMenuState();
        }

        private void ApplyDisplayMode()
        {
            string displayMode = JustView.Properties.Settings.Default.DisplayMode;
            bool useBorder = JustView.Properties.Settings.Default.BorderOnMainWindow;

            // Reset Width, Height, and Margin for all modes
            ImageDisplay.Width = Double.NaN; // Reset explicit width
            ImageDisplay.Height = Double.NaN; // Reset explicit height
            ImageDisplay.Margin = new Thickness(0); // Reset margin

            // Apply display mode for stretching
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

            // Apply border if the flag is set
            if (useBorder)
            {
                PrimaryWindow.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black); // Set the border color
                PrimaryWindow.BorderThickness = new Thickness(2); // Set border thickness
            }
            else
            {
                PrimaryWindow.BorderBrush = null; // Remove border
                PrimaryWindow.BorderThickness = new Thickness(0); // Remove border thickness
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
            OpenImage();
        }

        //TODO handle additional (general?) image types, including GIF
        private void OpenImage()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadImage(openFileDialog.FileName, true);
            }
        }

        private void LoadImage(string imagePath, bool openedThroughApp)
        {
            try
            {
                string selectedImagePath = imagePath;
                currentDirectory = Path.GetDirectoryName(selectedImagePath) ?? "";
                imageFiles = Directory.GetFiles(currentDirectory, "*.*")
                                      .Where(s => s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                                      .ToArray();

                // Find the index of the selected image
                currentImageIndex = Array.IndexOf(imageFiles, selectedImagePath);

                DisplayImage(currentImageIndex, openedThroughApp);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load the image at path \"{imagePath}\": {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // TODO this always sends image to first screen; probably easy fix but does it always get WorkArea from main monitor or what? May be better to be more flexible.
        private void ResizeWindowToImage()
        {
            if (ImageDisplay.Source is BitmapImage bitmap)
            {
                // Get the dimensions of the image
                double imageWidth = bitmap.PixelWidth;
                double imageHeight = bitmap.PixelHeight;

                // Get the screen's working area (excluding taskbar)
                var workingArea = System.Windows.SystemParameters.WorkArea;
                double screenWidth = workingArea.Width;
                double screenHeight = workingArea.Height;

                if (JustView.Properties.Settings.Default.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle)
                {
                    if (!autoResizingSpaceIsToggled)
                    {
                        int buffer = JustView.Properties.Settings.Default.PixelsSpaceAroundBounds;
                        screenWidth -= 2 * buffer;
                        screenHeight -= 2 * buffer;
                    }
                    
                }

                // Calculate the window size, ensuring it does not exceed the screen size
                // double newWidth = Math.Min(imageWidth, screenWidth);
                // double newHeight = Math.Min(imageHeight, screenHeight);
                bool widerThanScreen = imageWidth > screenWidth;
                
                double newWidth = imageWidth;
                double newHeight = imageHeight;

                if (widerThanScreen)
                {
                    newWidth = screenWidth;
                    newHeight *= screenWidth / imageWidth;
                }

                // even after adjusting when too wide, it may still be too tall, so check afterward.
                // for this to be the case, the image must be more portrait-oriented than the screen.
                bool tallerThanScreen = newHeight > screenHeight;
                if (tallerThanScreen)
                {
                    double tempWidth = newWidth;
                    double tempHeight = newHeight;

                    newWidth = tempWidth * (screenHeight / tempHeight);
                    newHeight = screenHeight;
                }


                // Set the window size and center it
                this.Width = newWidth;
                this.Height = newHeight;
                this.Left = (workingArea.Width - newWidth) / 2 + workingArea.Left;
                this.Top = (workingArea.Height - newHeight) / 2 + workingArea.Top;
            }
        }



        private void DisplayImage(int index, bool openedThroughApp)
        {
            autoResizingSpaceIsToggled = false;

            try
            {
                if (index < 0 || imageFiles == null || index >= imageFiles.Length) return;

                var uri = new Uri(imageFiles[index]);
                var bitmap = new BitmapImage(uri);
                ImageDisplay.Source = bitmap;

                currentlyDisplayedImagePath = uri.AbsolutePath;  // used for displaying image details

                // Optionally hide the no-image message if an image is loaded
                ImageDisplay.Visibility = Visibility.Visible;
                NoImageMessage.Visibility = Visibility.Collapsed;

                if (openedThroughApp && JustView.Properties.Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp)
                    ResizeWindowToImage();

                ApplyDisplayMode();
                UpdateContextMenuState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to display image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (WindowState == WindowState.Maximized)
                {
                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Normal;
                }
                e.Handled = true;
                return;
            }

            // set window dimensions to image if possible
            if (e.Key == Key.F)
            {
                autoResizingSpaceIsToggled = !autoResizingSpaceIsToggled;
                ResizeWindowToImage();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                OpenImage();
                e.Handled = true;
                return;
            }

            
            if (e.Key == Key.C)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    CopyImageToClipboard();
                }
                else
                {
                    Close();
                }
                
                e.Handled = true;
                return;
            }

            if (e.Key == Key.P)
            {
                OpenPreferences();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.A)
            {
                About();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.I)
            {
                ImageInfo();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.M)
            {
                MinimizeWindow();
                e.Handled = true;
                return;
            }

            // navigating in directory
            if (imageFiles != null && imageFiles.Length != 0)
            {
                if (e.Key == Key.Left)
                {
                    // Go to the previous image
                    currentImageIndex = (currentImageIndex == 0) ? imageFiles.Length - 1 : currentImageIndex - 1;
                    DisplayImage(currentImageIndex, true);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Right)
                {
                    // Go to the next image
                    currentImageIndex = (currentImageIndex == imageFiles.Length - 1) ? 0 : currentImageIndex + 1;
                    DisplayImage(currentImageIndex, true);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // Handle file drop
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length > 0 && IsSupportedImageFile(files[0]))
                    {
                        LoadImage(files[0], true);
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    // Handle URL drop
                    string url = (string)e.Data.GetData(DataFormats.Text);
                    if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && IsSupportedImageUri(uri))
                    {
                        DownloadAndLoadImage(uri);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load the dragged content: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsSupportedImageFile(string filePath)
        {
            string? extension = Path.GetExtension(filePath)?.ToLower();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp";
        }

        private bool IsSupportedImageUri(Uri uri)
        {
            string? extension = Path.GetExtension(uri.LocalPath)?.ToLower();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp";
        }

        private async void DownloadAndLoadImage(Uri uri)
        {
            try
            {
                using HttpClient client = new HttpClient();
                byte[] imageData = await client.GetByteArrayAsync(uri);
                using MemoryStream stream = new MemoryStream(imageData);

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                ImageDisplay.Source = bitmap;

                // Show the image and hide the no-image message
                ImageDisplay.Visibility = Visibility.Visible;
                NoImageMessage.Visibility = Visibility.Collapsed;

                ApplyDisplayMode();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image from URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }





        private void CopyImageToClipboard()
        {
            if (ImageDisplay.Source is BitmapSource bitmapSource)
            {
                try
                {
                    Clipboard.SetImage(bitmapSource);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy image to clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void MinimizeWindow()
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            MinimizeWindow();
        }

        private void OpenPreferences_Click(object sender, RoutedEventArgs e)
        {
            OpenPreferences();
        }

        private void OpenPreferences()
        {
            var configWindow = new ConfigurationWindow();

            // Center the window relative to the main application window
            configWindow.Owner = this; // Set the owner to the main window
            configWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (configWindow.ShowDialog() == true)
            {
                // Save the new preference
                JustView.Properties.Settings.Default.DisplayMode = configWindow.SelectedDisplayMode;
                JustView.Properties.Settings.Default.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle = configWindow.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle;
                JustView.Properties.Settings.Default.PixelsSpaceAroundBounds = configWindow.SpaceAroundBounds;
                JustView.Properties.Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp = configWindow.ResizeWindowToNewImageWhenOpeningThroughApp;
                JustView.Properties.Settings.Default.BorderOnMainWindow = configWindow.BorderOnMainWindow;

                JustView.Properties.Settings.Default.Save();

                ApplyDisplayMode();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            About();
        }

        private void About()
        {
            var aboutWindow = new AboutWindow();

            // Center the window relative to the main application window
            aboutWindow.Owner = this; // Set the owner to the main window
            aboutWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var _ = aboutWindow.ShowDialog();
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

        private void ImageInfo_Click(object sender, RoutedEventArgs e)
        {
            ImageInfo();
        }

        private void ImageInfo()
        {
            if (string.IsNullOrEmpty(currentlyDisplayedImagePath))
                return;

            var imageInfoWindow = new ImageInfoWindow(currentlyDisplayedImagePath)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            imageInfoWindow.ShowDialog();
        }

        private void UpdateContextMenuState()
        {
            // Enable/disable "Image Info" based on loaded image
            var imageInfoMenuItem = ImageContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header.ToString() == "Image Info");
            if (imageInfoMenuItem != null)
                imageInfoMenuItem.IsEnabled = !string.IsNullOrEmpty(currentlyDisplayedImagePath);
        }

        // Open context menu on right-click
        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImageContextMenu.IsOpen = true;
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
