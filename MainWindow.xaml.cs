using Microsoft.Win32;
using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;
using Point = System.Windows.Point;
using System.Drawing;
using WebP.Net;
using System.Windows.Media;
using System.Net;
using System.Windows.Media.Animation;
using System.Drawing.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using System.Runtime.InteropServices;
using System.Collections.Specialized;
using System.Reflection;

namespace SimpleImageViewer
{
    public partial class MainWindow : Window
    {
        private string? currentDirectory;
        private string[]? imageFiles;
        private int currentImageIndex;
        private string? currentlyDisplayedImagePath;
        private string? currentlyDisplayedImagePathBackslashes;
        private bool autoResizingSpaceIsToggled;
        private bool isExplorationMode;

        private OverlayMessageManager overlayManager;

        private const int MaxRecentFiles = 10;
        private readonly List<string> recentFiles = new();


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

            overlayManager = new OverlayMessageManager(MessageOverlayStack);

            isExplorationMode = false;
            NoImageMessage.Visibility = Visibility.Visible;
            ImageDisplay.Visibility = Visibility.Collapsed;
            CompositionTarget.Rendering += UpdateDebugInfo;

            ApplyDisplayMode();

            this.KeyDown += Window_KeyDown;

            LoadRecentFiles();
            UpdateContextMenuState();

            RenderOptions.SetBitmapScalingMode(ImageDisplay, BitmapScalingMode.HighQuality);  // Without this, lines can appear jagged, especially for larger images that are scaled down
        
            InitializeZooming();
            InitializePanning();

            
        }

        private void UpdateDebugInfo(object? sender, EventArgs e)
        {
            if (ImageDisplay == null) return;

            // Window dimensions
            double windowWidth = this.ActualWidth;
            double windowHeight = this.ActualHeight;

            // Image dimensions
            double imageWidth = ImageDisplay.ActualWidth;
            double imageHeight = ImageDisplay.ActualHeight;

            // Image scale
            double scaleX = imageScaleTransform.ScaleX;
            double scaleY = imageScaleTransform.ScaleY;

            // Image translation
            double translateX = imageTranslateTransform.X;
            double translateY = imageTranslateTransform.Y;

            // Cursor position relative to the window
            Point cursorPosition = Mouse.GetPosition(this);

            // Cursor position relative to the image
            Point cursorPositionImage = Mouse.GetPosition(ImageDisplay);

            string displayMode = JustView.Properties.Settings.Default.DisplayMode;

            double? imageTrueWidth = null;
            double? imageTrueHeight = null;
            if (ImageDisplay.Source is BitmapSource bitmap)
            {
                imageTrueWidth = bitmap.PixelWidth;
                imageTrueHeight = bitmap.PixelHeight;
            }

                // Debug text
                DebugTextBlock.Text =
                $"Window Dimensions: {windowWidth:F2} x {windowHeight:F2}\n" +
                $"Image Rendered Dimensions: {imageWidth:F2} x {imageHeight:F2}\n" +
                (imageTrueWidth.HasValue && imageTrueHeight.HasValue
                    ? $"Image True Dimensions: {imageTrueWidth:F2} x {imageTrueHeight:F2}\n" 
                    : "Image True Dimensions: N/A\n"
                ) +
                $"Scale: X={scaleX:F2}, Y={scaleY:F2}\n" +
                $"Translation: X={translateX:F2}, Y={translateY:F2}\n" +
                $"Cursor (Window): X={cursorPosition.X:F2}, Y={cursorPosition.Y:F2}\n" +
                $"Cursor (Image): X={cursorPositionImage.X:F2}, Y={cursorPositionImage.Y:F2}\n" +
                $"Display mode: {displayMode:F2}";
        }

        // mode that enables zooming/panning and disables behavior associated with standard display modes and window resizing
        private void EnterExplorationMode()
        {
            var wasExplorationMode = isExplorationMode;

            string displayMode = JustView.Properties.Settings.Default.DisplayMode;
            switch (displayMode)
            {
                case "StretchToFit":
                case "ZoomToFill":
                case "BestFit":
                    // To clear out weirdness and prepare for zooming/panning, apply display for zoomless best fit.
                    // This is cleaner than doing nothing, pending future work to make this more seamless (particularly for ZoomToFill).
                    ApplyDisplayMode(true);
                    break;
                //case "BestFitWithoutZooming":
                default:
                    break;
            }
              
            isExplorationMode = true;
            ImageDisplay.Stretch = System.Windows.Media.Stretch.None;

            //bool useBorder = JustView.Properties.Settings.Default.BorderOnMainWindow;

            if (!wasExplorationMode)
                Message("Entered Exploration Mode (zoom and pan)");
        }

        private void ApplyDisplayMode(bool simulateZoomlessBestFit = false)
        {
            var wasExplorationMode = isExplorationMode;
            isExplorationMode = false;

            string displayMode = JustView.Properties.Settings.Default.DisplayMode;
            if (simulateZoomlessBestFit)
                displayMode = "BestFitWithoutZooming";
            bool useBorder = JustView.Properties.Settings.Default.BorderOnMainWindow;
            bool loopGifs = JustView.Properties.Settings.Default.LoopGifs;
            bool alwaysOnTopByDefault = JustView.Properties.Settings.Default.AlwaysOnTopByDefault;

            // Reset Width, Height, and Margin for all modes
            ImageDisplay.Width = Double.NaN; // Reset explicit width
            ImageDisplay.Height = Double.NaN; // Reset explicit height
            ImageDisplay.Margin = new Thickness(0); // Reset margin

            ResetPan();
            ResetZoom();
            

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

            if (loopGifs)
            {
                ImageBehavior.SetRepeatBehavior(ImageDisplay, RepeatBehavior.Forever);
            }
            else
            {
                ImageBehavior.SetRepeatBehavior(ImageDisplay, new RepeatBehavior(1));
            }

            Topmost = JustView.Properties.Settings.Default.AlwaysOnTopByDefault;

            if (wasExplorationMode)
                Message("Entered Display Mode");
        }



        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!isExplorationMode && JustView.Properties.Settings.Default.DisplayMode == "BestFitWithoutZooming")
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

        //private bool isDragging = false;
        private Point initialCursorPosition;

        private bool isDraggingWindow = false;
        private bool isPanningImage = false;

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (!isExplorationMode) EnterExplorationMode();

                    this.Cursor = Cursors.SizeAll; // Replace with a custom gripping hand cursor if desired. TODO
                    isPanningImage = true;
                    lastMousePosition = e.GetPosition(this);
                    ImageDisplay.CaptureMouse(); // bookmark line. captured mouse position could be different than expected due to subsequent automatic panning such as to center/bound image?
                }
                else if (WindowState == WindowState.Maximized)
                {
                    // Dragging window in fullscreen mode
                    initialCursorPosition = e.GetPosition(this);
                    isDraggingWindow = true;
                }
                else
                {
                    // Dragging window in normal mode
                    DragMove();
                }
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanningImage)
            {
                if (!isExplorationMode) EnterExplorationMode();

                // Current mouse position
                Point currentMousePosition = e.GetPosition(this);

                // Calculate the movement delta
                Vector delta = currentMousePosition - lastMousePosition;

                // Get current image dimensions including any scaling (zoom)
                double scaledWidth = ImageDisplay.ActualWidth * imageScaleTransform.ScaleX;
                double scaledHeight = ImageDisplay.ActualHeight * imageScaleTransform.ScaleY;

                // Get bounds of the window or container
                double containerWidth = this.ActualWidth;
                double containerHeight = this.ActualHeight;

                // Calculate new translate values
                double newTranslateX = imageTranslateTransform.X + delta.X;
                double newTranslateY = imageTranslateTransform.Y + delta.Y;

                // Constrain X-axis translation
                double maxTranslateX = Math.Max(0, (scaledWidth - containerWidth) / 2);
                double minTranslateX = -maxTranslateX;
                newTranslateX = Math.Min(Math.Max(newTranslateX, minTranslateX), maxTranslateX);

                // Constrain Y-axis translation
                double maxTranslateY = Math.Max(0, (scaledHeight - containerHeight) / 2);
                double minTranslateY = -maxTranslateY;
                newTranslateY = Math.Min(Math.Max(newTranslateY, minTranslateY), maxTranslateY);

                // Apply constrained translation
                imageTranslateTransform.X = newTranslateX;
                imageTranslateTransform.Y = newTranslateY;

                // Update last mouse position
                lastMousePosition = currentMousePosition;
            }
            else if (isDraggingWindow && WindowState == WindowState.Maximized)
            {
                // Handle dragging window out of fullscreen
                Point cursorPosition = e.GetPosition(this);
                ToggleFullscreen();

                this.Left = cursorPosition.X - (this.ActualWidth / 2);
                this.Top = cursorPosition.Y - (this.ActualHeight / 2);

                DragMove();
            }

            if (!isPanningImage && Keyboard.Modifiers == ModifierKeys.Control && ImageDisplay.IsMouseOver)
            {
                this.Cursor = Cursors.Hand;  // TODO could be better custom cursor
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isPanningImage)
            {
                StopPanning();
            }

            if (isDraggingWindow)
            {
                isDraggingWindow = false;
            }
        }

        private void StopPanning()
        {
            // Revert to open hand cursor when mouse is released, if still applicable
            if (Keyboard.Modifiers == ModifierKeys.Control && ImageDisplay.IsMouseOver)
                this.Cursor = Cursors.Hand;
            // Otherwise return to default
            else
                this.Cursor = Cursors.Arrow;
            isPanningImage = false;
            ImageDisplay.ReleaseMouseCapture();
        }


        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            OpenImage();
        }

        private void OpenImage()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp, *.gif, *.webp, *.jfif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.jfif"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadImage(openFileDialog.FileName, true);
                Message("File loaded from dialog.");
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
                                                 s.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".jfif", StringComparison.OrdinalIgnoreCase) ||
                                                 s.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
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
            if (isExplorationMode) ApplyDisplayMode();  // exit exploration mode

            if (ImageDisplay.Source is BitmapSource bitmap)
            {
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


                // Set the window size and center it  // TODO may want to not center it in some cases
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

                // TODO currentlyDisplayedImagePath can possibly be replaced with the backslashes version
                currentlyDisplayedImagePath = WebUtility.UrlDecode(uri.AbsolutePath);
                currentlyDisplayedImagePathBackslashes = uri.LocalPath;
                AddToRecentFiles(uri.LocalPath);

                if (uri.AbsolutePath.ToLower().EndsWith(".gif"))
                {
                    // TODO can have RepeatBehavior (whether to loop) be a config
                    var bitmap = new BitmapImage(uri);
                    ImageDisplay.Source = null;
                    ImageBehavior.SetAnimatedSource(ImageDisplay, bitmap);
                }
                else if (uri.AbsolutePath.ToLower().EndsWith(".webp"))
                {
                    byte[] webpBytes = File.ReadAllBytes(imageFiles[index]);
                    using var webp = new WebPObject(webpBytes);
                    var webpImage = webp.GetImage();
                    BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                        ((Bitmap)webpImage).GetHbitmap(),
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );
                    ImageBehavior.SetAnimatedSource(ImageDisplay, null);
                    ImageDisplay.Source = bitmapSource;
                }
                else
                {
                    var bitmap = new BitmapImage(uri);
                    ImageBehavior.SetAnimatedSource(ImageDisplay, null);
                    ImageDisplay.Source = bitmap;
                }
                
                // hide the no-image message if an image is loaded
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

            
            if (e.Key == Key.D)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    DebugTextBlock.Visibility = DebugTextBlock.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                }
                else
                {
                    ApplyDisplayMode();
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

            if (e.Key == Key.H)
            {
                HotkeyRef();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V && !(Keyboard.Modifiers == ModifierKeys.Control))
            {
                MaximizeVerticalDimension();
                e.Handled = true;
                return;
            }


            if (e.Key == Key.C)
            {
                ModifierKeys modifiers = Keyboard.Modifiers;

                if ((modifiers & ModifierKeys.Control) != 0 && (modifiers & ModifierKeys.Alt) != 0)
                {
                    CopyCompressedImageToClipboardAsJpgFile();
                }
                else if ((modifiers & ModifierKeys.Control) != 0)
                {
                    // TODO allows paste into file explorer or Discord (file copied) but not, say, MS Paint (image copied)?
                    CopyImageFileToClipboard();
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

            // Toggle Topmost (always-on-top) for this window
            if (e.Key == Key.T)
            {
                Topmost = !Topmost;
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

            if (e.Key == Key.R)
            {
                RotateImage90Degrees();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.B)
            {
                ResizeWindowToRemoveBestFitBars();
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

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.OemPlus || e.Key == Key.Add) // Zoom In
                {
                    ZoomFromCenter(true);
                    e.Handled = true;
                }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) // Zoom Out
                {
                    ZoomFromCenter(false);
                    e.Handled = true;
                }
                else if (e.Key == Key.D0) // Reset to Best Fit
                {
                    ResetPan();
                    ResetZoom();
                    e.Handled = true;
                }
                else if (e.Key == Key.D9) // True Resolution (100%)
                {
                    //ResetPan();
                    ResetZoomToTrueResolution();
                    e.Handled = true;
                }
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                if (!isPanningImage && ImageDisplay.IsMouseOver)
                {
                    this.Cursor = Cursors.Hand;
                }
            }
        }

        
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                // Revert to the default cursor when Ctrl is released
                if (!isPanningImage)
                {
                    this.Cursor = Cursors.Arrow;
                }
            }
        }

        // TODO if compression attempts take significant time, show message to user: "Finding best compression..."
        private void CopyCompressedImageToClipboardAsJpgFile()
        {
            // TODO configure these
            var tempFilePath = "temp.jpg";
            double maxSizeInMB = 10;

            try
            {
                if (ImageDisplay.Source is not BitmapSource bitmapSource)
                    return;

                long finalSizeBytes = -1;
                long finalQuality = 100;

                // Define maximum size in bytes
                double maxSizeInBytes = maxSizeInMB * 1024 * 1024;

                // Ensure the temp file has a valid path
                tempFilePath = Path.Combine(Path.GetTempPath(), tempFilePath);

                // Convert BitmapSource to Bitmap
                using (Bitmap bitmap = BitmapSourceToBitmap(bitmapSource))
                {
                    // Ensure Bitmap is in a compatible format
                    using (Bitmap compatibleBitmap = new(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(compatibleBitmap))
                        {
                            g.DrawImage(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                        }

                        long quality = 75L; // Start at medium quality
                        const long minQuality = 10L; // Minimum quality to avoid over-compression

                        // TODO review this method as it may be possible to increase speed/efficiency. Also iterations should compress the original file, not the result of previous compression.
                        while (true)
                        {
                            using (MemoryStream memoryStream = new())
                            {
                                ImageCodecInfo? jpegCodec = GetEncoder(ImageFormat.Jpeg);
                                if (jpegCodec == null)
                                    throw new Exception("Failed to get JPEG codec");

                                EncoderParameters encoderParams = new(1);
                                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

                                compatibleBitmap.Save(memoryStream, jpegCodec, encoderParams);

                                // Check the file size
                                if (memoryStream.Length <= maxSizeInBytes)
                                {
                                    finalSizeBytes = memoryStream.Length;
                                    finalQuality = quality;
                                    // Save the compressed image to the temporary file
                                    File.WriteAllBytes(tempFilePath, memoryStream.ToArray());
                                    break;
                                }

                                // Reduce quality for further compression
                                quality -= 5L;
                                if (quality < minQuality)
                                    throw new Exception("Unable to compress image to fit the size limit");
                            }
                        }
                    }
                }

                // Copy file path to clipboard
                StringCollection filePaths = new();
                filePaths.Add(tempFilePath);
                Clipboard.SetFileDropList(filePaths);

                Message("Copied compressed file to clipboard. Quality: " + finalQuality + "%. Bytes: " + finalSizeBytes);
                //MessageBox.Show("Compressed image file copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy compressed image as file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        //TODO remove pop-ups after debugging



        // Helper methods to convert and resize images
        private Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            using var ms = new MemoryStream();
            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            return new Bitmap(ms);
        }

        private Bitmap ResizeImage(Bitmap image, int width, int height)
        {
            Bitmap resized = new(width, height);
            using var g = Graphics.FromImage(resized);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, 0, 0, width, height);
            return resized;
        }

        private ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            return ImageCodecInfo.GetImageDecoders().FirstOrDefault(codec => codec.FormatID == format.Guid);
        }

        private BitmapSource BitmapToBitmapSource(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;
            return BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
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
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp" || extension == ".gif" || extension == ".webp" || extension == ".jfif";
        }

        private bool IsSupportedImageUri(Uri uri)
        {
            string? extension = Path.GetExtension(uri.LocalPath)?.ToLower();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp" || extension == ".gif" || extension == ".webp" || extension == ".jfif";
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


        private ScaleTransform imageScaleTransform = new ScaleTransform();

        private void InitializeZooming()
        {
            ImageDisplay.RenderTransform = imageScaleTransform;
            ImageDisplay.RenderTransformOrigin = new Point(0.5, 0.5);
            this.PreviewMouseWheel += OnMouseWheelZoom;
        }


        private void OnMouseWheelZoom(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (!isExplorationMode) EnterExplorationMode();

                // Get current mouse position relative to the image
                Point cursorPosition = e.GetPosition(PrimaryWindow);

                // Zoom factor
                double zoomDelta = e.Delta > 0 ? 1.1 : 1 / 1.1;

                Zoom(zoomDelta, cursorPosition);

                e.Handled = true;
            }
        }

        private void ZoomFromCenter(bool zoomIn)
        {
            if (!isExplorationMode) EnterExplorationMode();

            // Get window center relative to the image
            Point windowCenter = new Point(PrimaryWindow.ActualWidth / 2, PrimaryWindow.ActualHeight / 2);

            // Zoom factor
            double zoomDelta = zoomIn ? 1.1 : 1 / 1.1;

            Zoom(zoomDelta, windowCenter);
        }

        private void Zoom(double zoomDelta, Point zoomOrigin)
        {
            if (!isExplorationMode) EnterExplorationMode();

            // Calculate new scale
            double newScaleX = imageScaleTransform.ScaleX * zoomDelta;
            double newScaleY = imageScaleTransform.ScaleY * zoomDelta;

            // Limit zoom levels (optional)
            newScaleX = Math.Max(0.1, Math.Min(10, newScaleX));
            newScaleY = Math.Max(0.1, Math.Min(10, newScaleY));

            // Adjust translation to zoom around the cursor
            double offsetX = zoomOrigin.X - imageTranslateTransform.X - (PrimaryWindow.ActualWidth / 2);
            double offsetY = zoomOrigin.Y - imageTranslateTransform.Y - (PrimaryWindow.ActualHeight / 2);

            imageTranslateTransform.X -= offsetX * (zoomDelta - 1);
            imageTranslateTransform.Y -= offsetY * (zoomDelta - 1);

            // Apply new scale
            imageScaleTransform.ScaleX = newScaleX;
            imageScaleTransform.ScaleY = newScaleY;
        }

        private void ResetZoom()
        {
            imageScaleTransform.ScaleX = 1.0;
            imageScaleTransform.ScaleY = 1.0;
        }

        private void ResetZoomToTrueResolution()
        {
            if (!isExplorationMode) EnterExplorationMode();

            if (ImageDisplay.Source is BitmapSource bitmap)
            {
                imageScaleTransform.ScaleX = 1.0 / ImageDisplay.ActualWidth * bitmap.PixelWidth;
                imageScaleTransform.ScaleY = 1.0 / ImageDisplay.ActualHeight * bitmap.PixelHeight;
            }
        }

        private void ResetPan()
        {
            StopPanning();
            imageTranslateTransform.X = 0;
            imageTranslateTransform.Y = 0;
        }

        private TranslateTransform imageTranslateTransform = new TranslateTransform();
        private Point lastMousePosition;

        private void InitializePanning()
        {
            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(imageScaleTransform);
            transformGroup.Children.Add(imageTranslateTransform);

            ImageDisplay.RenderTransform = transformGroup;

            //ImageDisplay.MouseDown += OnMouseDownStartPanning;
            //ImageDisplay.MouseMove += OnMouseMovePan;
            //ImageDisplay.MouseUp += OnMouseUpEndPanning;
        }

        

        public void Message(string message, TimeSpan? duration = null)
        {
            duration ??= TimeSpan.FromSeconds(1.5);
            overlayManager.ShowOverlayMessage(message, (TimeSpan)duration);
        }

        private void CopyImageFileToClipboard()
        {
            try
            {
                StringCollection filePaths = new();
                filePaths.Add(currentlyDisplayedImagePath);
                Clipboard.SetFileDropList(filePaths);

                Message("Copied image file to clipboard.");

                //MessageBox.Show("File reference copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy file reference: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }


            //if (ImageDisplay.Source is BitmapSource bitmapSource)
            //{
            //    try
            //    {
            //        Clipboard.SetImage(bitmapSource);
            //    }
            //    catch (Exception ex)
            //    {
            //        MessageBox.Show($"Failed to copy image to clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //    }
            //}
        }

        // could consider adding another hotkey for counter-clockwise, or making the rotation direction a toggleable setting.
        private void RotateImage90Degrees()
        {
            if (ImageDisplay.Source is not BitmapSource bitmapSource)
            {
                MessageBox.Show("No image is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Create a TransformedBitmap to apply the rotation
            TransformedBitmap rotatedBitmap = new TransformedBitmap();
            rotatedBitmap.BeginInit();
            rotatedBitmap.Source = bitmapSource;
            rotatedBitmap.Transform = new RotateTransform(90); // Rotate by 90 degrees
            rotatedBitmap.EndInit();

            // Set the rotated image as the source
            ImageDisplay.Source = rotatedBitmap;
            ApplyDisplayMode();  // could add an option to also ResizeWindowToImage() when rotating, but realistically may be rarely desired
            // No need to reset explicitly when changing images, since Source is just reassigned.
        }


        private void ResizeWindowToRemoveBestFitBars()
        {
            if (isExplorationMode) ApplyDisplayMode(); // TODO possibly just disable/return here

            string displayMode = JustView.Properties.Settings.Default.DisplayMode;

            switch (displayMode)
            {
                case "BestFit":
                case "BestFitWithoutZooming":
                    break;
                default:
                    return;
            }

            var newHeight = ImageDisplay.Height;
            var newWidth = ImageDisplay.Width;

            if (displayMode.Equals("BestFit"))
            {
                if (ImageDisplay.Source is BitmapSource bitmap)
                {
                    double imageWidth = bitmap.PixelWidth;
                    double imageHeight = bitmap.PixelHeight;

                    bool windowIsMoreLandscapeThanImage = (this.Width / this.Height) > (imageWidth / imageHeight);

                    if (windowIsMoreLandscapeThanImage)
                    {
                        newWidth = imageWidth * (this.Height/imageHeight);
                    }
                    else
                    {
                        newHeight = imageHeight * (this.Width/imageWidth);
                    }
                }
            }

            // Temporarily disable layout updates by deferring them to the next frame. TODO use this concept to make other things smoother as well.
            // TODO also this actually does not work (regarding smoother change)
            this.Dispatcher.Invoke(new Action(() =>
            {
                // Apply the calculated size and position adjustments all at once
                this.Top += (this.Height - newHeight) / 2;
                this.Left += (this.Width - newWidth) / 2;

                this.Height = newHeight;
                this.Width = newWidth;

                this.InvalidateMeasure();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        


        // Maximize vertical dimension and align window with it vertically
        private void MaximizeVerticalDimension()
        {
            var wa = SystemParameters.WorkArea;
            this.Height = wa.Height;
            this.Top = wa.Top;
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
                JustView.Properties.Settings.Default.LoopGifs = configWindow.LoopGifs;
                JustView.Properties.Settings.Default.MuteMessages = configWindow.MuteMessages;
                JustView.Properties.Settings.Default.AlwaysOnTopByDefault = configWindow.AlwaysOnTopByDefault;

                JustView.Properties.Settings.Default.Save();

                ApplyDisplayMode();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            About();
        }

        private void HotkeyRef_Click(object sender, RoutedEventArgs e)
        {
            HotkeyRef();
        }

        private void About()
        {
            var aboutWindow = new AboutWindow();

            // Center the window relative to the main application window
            aboutWindow.Owner = this; // Set the owner to the main window
            aboutWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            aboutWindow.Show();
        }

        private void HotkeyRef()
        {
            var hkrWindow = new HotkeyRefWindow();

            // Center the window relative to the main application window
            hkrWindow.Owner = this; // Set the owner to the main window
            // could make this not stay above main window, by not setting the owner.
            hkrWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            hkrWindow.Show();
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
            if (string.IsNullOrEmpty(currentlyDisplayedImagePathBackslashes))
                return;

            var imageInfoWindow = new ImageInfoWindow(currentlyDisplayedImagePathBackslashes)
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

        private void SaveRecentFiles()
        {
            System.Collections.Specialized.StringCollection collection = new();
            collection.AddRange(recentFiles.ToArray());

            JustView.Properties.Settings.Default.RecentFiles = collection;
            JustView.Properties.Settings.Default.Save();
        }


        private void AddToRecentFiles(string filePath)
        {
            // Avoid duplicates
            recentFiles.Remove(filePath);
            recentFiles.Insert(0, filePath);

            // Enforce max size
            if (recentFiles.Count > MaxRecentFiles)
                recentFiles.RemoveAt(recentFiles.Count - 1);

            UpdateRecentFilesMenu();
            SaveRecentFiles();
        }


        private void UpdateRecentFilesMenu()
        {
            // Clear the existing items
            RecentFilesMenu.Items.Clear();

            // Add recent files
            foreach (string file in recentFiles)
            {
                MenuItem fileItem = new MenuItem
                {
                    Header = System.IO.Path.GetFileName(file),
                    ToolTip = file,
                    Tag = file
                };
                fileItem.Click += (s, e) => OpenRecentFile((string)((MenuItem)s).Tag);
                RecentFilesMenu.Items.Add(fileItem);
            }

            // Add a separator if there are recent files
            if (recentFiles.Count > 0)
            {
                RecentFilesMenu.Items.Add(new Separator());

                // Add "Clear History" item
                MenuItem clearHistoryItem = new MenuItem
                {
                    Header = "Clear History",
                    ToolTip = "Clear the list of recent files."
                };
                clearHistoryItem.Click += (s, e) => ClearRecentFiles();
                RecentFilesMenu.Items.Add(clearHistoryItem);
            }
            else
            {
                MenuItem noRecentFilesItem = new()
                {
                    Header = "No Recent Files",
                    IsEnabled = false
                };
                RecentFilesMenu.Items.Add(noRecentFilesItem);
            }
        }


        private void OpenRecentFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"File not found: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Your logic to open the file
            LoadImage(filePath, true);
        }

        private void LoadRecentFiles()
        {
            try
            {
                var savedFiles = JustView.Properties.Settings.Default.RecentFiles;

                if (savedFiles != null)
                {
                    recentFiles.Clear();
                    recentFiles.AddRange(savedFiles.Cast<string>().ToList());
                }
            }
            catch
            {
                // Log or handle the error, e.g., recreate the list
                recentFiles.Clear();
                throw;
            }

            UpdateRecentFilesMenu();
        }

        // TODO not yet used. Probably should be a button in Preferences. Consider also adding icons of images (behind a feature flag setting).
        private void ClearRecentFiles()
        {
            recentFiles.Clear();
            SaveRecentFiles();
            UpdateRecentFilesMenu();
        }


    }
}


//// Optionally subscribe to a property-changed event if the setting can be changed at runtime
//JustView.Properties.Settings.Default.PropertyChanged += Settings_PropertyChanged;
//private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
//{
//    if (e.PropertyName == nameof(JustView.Properties.Settings.Default.AlwaysOnTopByDefault))
//    {
//        ApplyAlwaysOnTopSetting();
//    }
//}


