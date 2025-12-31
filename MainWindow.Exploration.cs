using System.Windows.Media.Animation;
using System.Windows;
using System.Windows.Input;
using WpfAnimatedGif;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Diagnostics;
using System.IO;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private void EnterExplorationMode()
        {
            var wasExplorationMode = isExplorationMode;

            string displayMode = Cloudless.Properties.Settings.Default.DisplayMode;

            if (!isCropMode)
                switch (displayMode)
                {
                    case "StretchToFit":
                    case "ZoomToFill":
                    case "BestFit":
                        // To clear out weirdness and prepare for zooming/panning, apply display for zoomless best fit.
                        // This is cleaner than doing nothing, pending future work to make this more seamless (particularly for ZoomToFill).
                        ApplyDisplayMode(true);
                        break;
                    default:
                        break;
                }

            isExplorationMode = true;
            ImageDisplay.Stretch = System.Windows.Media.Stretch.Uniform;

            if (!wasExplorationMode)
                Message("Entered Exploration Mode (zoom and pan)");
        }
        private void ApplyDisplayMode(bool simulateZoomlessBestFit = false)
        {
            var wasExplorationMode = isExplorationMode;
            isExplorationMode = false;

            string displayMode = Cloudless.Properties.Settings.Default.DisplayMode;
            if (simulateZoomlessBestFit)
                displayMode = "BestFitWithoutZooming";
            bool loopGifs = Cloudless.Properties.Settings.Default.LoopGifs;
            bool alwaysOnTopByDefault = Cloudless.Properties.Settings.Default.AlwaysOnTopByDefault;

            // These lines allow the non-best fit display modes to render properly.
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
                    // ^^^ this is undone at the end of CenterImageIfNeeded? Sets to Uniform. TODO.
                    // Center and scale the image as needed
                    ScaleImageToWindow();
                    UpdateMargins();
                    // Ensure the image is not clipped by setting Stretch to Uniform // Later note TODO, this contradicts stretch mode given for zoomless best fit, and is only used in this mode too.
                    ImageDisplay.Stretch = Stretch.Uniform;
                    break;
                default:
                    ImageDisplay.Stretch = System.Windows.Media.Stretch.Uniform; // Default to BestFit
                    break;
            }

            UpdateBorderColor();

            if (loopGifs)
            {
                ImageBehavior.SetRepeatBehavior(ImageDisplay, RepeatBehavior.Forever);
            }
            else
            {
                ImageBehavior.SetRepeatBehavior(ImageDisplay, new RepeatBehavior(1));
            }

            Topmost = Cloudless.Properties.Settings.Default.AlwaysOnTopByDefault;

            if (wasExplorationMode)
                Message("Entered Display Mode");
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!isCropMode && Cloudless.Properties.Settings.Default.DisplayMode.StartsWith("Best"))
                ScaleImageToWindow();
            else if (imageTranslateTransform != null)
            {
                var heightDiff = cropModeStartingWindowHeight - this.ActualHeight;
                var widthDiff = cropModeStartingWindowWidth - this.ActualWidth;

                var topDiff = cropModeStartingWindowTop - this.Top;
                var leftDiff = cropModeStartingWindowLeft - this.Left;

                imageTranslateTransform.Y = cropModeStartingImagePosY + heightDiff / 2.0 + topDiff;
                imageTranslateTransform.X = cropModeStartingImagePosX + widthDiff / 2.0 + leftDiff;
                //return;
            }


            if (!isExplorationMode && Cloudless.Properties.Settings.Default.DisplayMode == "BestFitWithoutZooming")
            {
                UpdateMargins();
                // Ensure the image is not clipped by setting Stretch to Uniform // Later note TODO, this contradicts stretch mode given for zoomless best fit, and is only used in this mode too.
                ImageDisplay.Stretch = Stretch.Uniform;
            }
            else
            {
                UpdateMargins();
                if (!isCropMode)
                {
                    ClampTransformToIntuitiveBounds();
                }
            }
        }
        private void ScaleImageToWindow()
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
            }
        }


        private WindowState OldWindowState { get; set; }  // TAG windows_wpf_borders_bad
        protected override void OnStateChanged(EventArgs e)  // TAG windows_wpf_borders_bad
        {
            base.OnStateChanged(e);

            // TODO I guess make this a power user setting with explanation. Windows+WPF is apparently bad at handling border when maximized with AllowsTransparency==true.
            // Code and more discussion: https://stackoverflow.com/questions/29391063/wpf-maximized-window-bigger-than-screen
            //const int HACK_BORDER_GARBAGE = 7;  // was SystemParameters.WindowResizeBorderThickness which got 4
            Thickness HACK_THICKNESS = GetHackBorderSizeWhenFullscreen();
            // 7. magic number. Good on my system. Calculated as "correct" in that the detected window dimensions vary from the ImageDisplay dimensions by these amounts exactly.
            // ...on that note, maybe can reliably calculate these numbers dynamically per user/system. Will return to this. TODO

            this.BorderThickness = this.WindowState switch
            {
                WindowState.Maximized => InflateBorder(HACK_THICKNESS),
                WindowState.Normal or WindowState.Minimized when this.OldWindowState == WindowState.Maximized => DeflateBorder(HACK_THICKNESS),
                _ => this.BorderThickness
            };
            this.OldWindowState = this.WindowState;
        }
        private Thickness InflateBorder(Thickness thickness)  // TAG windows_wpf_borders_bad
        {
            double left = this.BorderThickness.Left + thickness.Left;
            double top = this.BorderThickness.Top + thickness.Top;
            double right = this.BorderThickness.Right + thickness.Right;
            double bottom = this.BorderThickness.Bottom + thickness.Bottom;
            return new Thickness(left, top, right, bottom);
        }
        private Thickness DeflateBorder(Thickness thickness)  // TAG windows_wpf_borders_bad
        {
            double left = this.BorderThickness.Left - thickness.Left;
            double top = this.BorderThickness.Top - thickness.Top;
            double right = this.BorderThickness.Right - thickness.Right;
            double bottom = this.BorderThickness.Bottom - thickness.Bottom;
            return new Thickness(left, top, right, bottom);
        }
        private Thickness GetHackBorderSizeWhenFullscreen()  // TAG windows_wpf_borders_bad
        {
            return new Thickness(7, 7, 7, 7);
        }
        private void UpdateMargins()
        {
            if (ImageDisplay.Source is BitmapSource bitmap)
            {
                // Center the image display
                double windowWidth = this.ActualWidth;
                double windowHeight = this.ActualHeight;

                if (this.WindowState == WindowState.Maximized)
                {
                    var hackThickness = GetHackBorderSizeWhenFullscreen();
                    windowWidth -= hackThickness.Left * 2;
                    windowHeight -= hackThickness.Top * 2;
                }

                double marginX = (windowWidth - ImageDisplay.Width) / 2;
                double marginY = (windowHeight - ImageDisplay.Height) / 2;
                marginX = Double.IsNaN(marginX) ? 0 : marginX;
                marginY = Double.IsNaN(marginY) ? 0 : marginY;

                if (isCropMode)
                {
                    ImageDisplay.Margin = new Thickness(
                    marginX,
                    marginY,
                    marginX,
                    marginY
                    );
                }
                else
                    ImageDisplay.Margin = new Thickness(
                        Math.Max(0, marginX),
                        Math.Max(0, marginY),
                        Math.Max(0, marginX),
                        Math.Max(0, marginY)
                    );
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

            UpdateCropModeInfo();
        }
        private void ResizeWindow(double width, double height)
        {
            if (isExplorationMode) ApplyDisplayMode();  // exit exploration mode
            this.Width = width;  // each of these lines may result in an invocation of window_sizeChange
            this.Height = height;
        }
        private void RepositionWindow(double left, double top)
        {
            this.Left = left;
            this.Top = top;
        }
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

                if (Cloudless.Properties.Settings.Default.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle)
                {
                    if (!autoResizingSpaceIsToggled)
                    {
                        int buffer = Cloudless.Properties.Settings.Default.PixelsSpaceAroundBounds;
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

                // Set the window size
                this.Width = newWidth;
                this.Height = newHeight;
            }
        }
        private void CenterWindow()
        {
            var workingArea = SystemParameters.WorkArea;
            var left = (workingArea.Width - this.Width) / 2 + workingArea.Left;
            var top = (workingArea.Height - this.Height) / 2 + workingArea.Top;
            RepositionWindow(left, top);
        }
        private void ClampTransformToIntuitiveBounds(Vector? delta = null)
        {
            // Get current image dimensions including any scaling (zoom)
            double scaledWidth = ImageDisplay.ActualWidth * imageScaleTransform.ScaleX;
            double scaledHeight = ImageDisplay.ActualHeight * imageScaleTransform.ScaleY;

            // Get bounds of the window or container
            double containerWidth = this.ActualWidth;
            double containerHeight = this.ActualHeight;

            // Calculate new translate values. Include translation from delta, if provided.
            double newTranslateX = imageTranslateTransform.X + delta?.X ?? 0;
            double newTranslateY = imageTranslateTransform.Y + delta?.Y ?? 0;

            if (!Cloudless.Properties.Settings.Default.DisableSmartZoom)
            {
                // Constrain X-axis translation
                double maxTranslateX = Math.Max(0, (scaledWidth - containerWidth) / 2);
                double minTranslateX = -maxTranslateX;
                newTranslateX = Math.Min(Math.Max(newTranslateX, minTranslateX), maxTranslateX);

                // Constrain Y-axis translation
                double maxTranslateY = Math.Max(0, (scaledHeight - containerHeight) / 2);
                double minTranslateY = -maxTranslateY;
                newTranslateY = Math.Min(Math.Max(newTranslateY, minTranslateY), maxTranslateY);
            }

            // Apply constrained translation
            imageTranslateTransform.X = newTranslateX;
            imageTranslateTransform.Y = newTranslateY;

            UpdateCropModeInfo();
        }
        private void ZoomFromCenter(bool zoomIn)
        {
            if (!isExplorationMode) EnterExplorationMode();

            // Get window center relative to the image
            Point windowCenter = new Point(PrimaryWindow.ActualWidth / 2, PrimaryWindow.ActualHeight / 2);

            // Zoom factor
            double zoomDelta = zoomIn ? 1.1 : 1 / 1.1;

            Zoom(windowCenter, zoomDelta: zoomDelta);
        }

        private void ZoomFromCenterToGivenScale(double scale)
        {
            if (!isExplorationMode) EnterExplorationMode();

            // Get window center relative to the image
            Point windowCenter = new Point(PrimaryWindow.ActualWidth / 2, PrimaryWindow.ActualHeight / 2);

            // consider scenario where scale is unintuitive due to window resizing
            if (ImageDisplay != null && ImageDisplay.Source is BitmapSource bitmap)
            {
                double imageWidth = ImageDisplay.ActualWidth;
                double imageTrueWidth = bitmap.PixelWidth;

                scale *= imageTrueWidth / imageWidth;
            }

            Zoom(windowCenter, zoomFinal: scale);
        }
        private void Zoom(Point zoomOrigin, double? zoomDelta = null, double? zoomFinal = null)
        {
            if (zoomDelta == null && zoomFinal == null)
                throw new ArgumentException("Cannot zoom with null delta and null final");
            if (zoomDelta != null && zoomFinal != null)
                throw new ArgumentException("Cannot zoom with both a delta and a final");

            if (!isExplorationMode) EnterExplorationMode();

            double derivedDelta = zoomDelta != null ? (double)zoomDelta : ((double)zoomFinal / imageScaleTransform.ScaleX);

            // Calculate new scale
            double newScaleX = imageScaleTransform.ScaleX * derivedDelta;
            double newScaleY = imageScaleTransform.ScaleY * derivedDelta;

            // Get bounds of the window or container
            double containerWidth = PrimaryWindow.ActualWidth;
            double containerHeight = PrimaryWindow.ActualHeight;

            // Get the image dimensions at 1.0x scale
            double imageOriginalWidth = ImageDisplay.ActualWidth;
            double imageOriginalHeight = ImageDisplay.ActualHeight;

            // Determine the minimum allowable scale based on fitting logic
            double minScaleX = imageOriginalWidth > containerWidth ? containerWidth / imageOriginalWidth : 1.0;
            double minScaleY = imageOriginalHeight > containerHeight ? containerHeight / imageOriginalHeight : 1.0;

            // Use the larger of the two minimum scales to prevent simultaneous black bars
            double minScale = Math.Max(minScaleX, minScaleY);

            if (!Cloudless.Properties.Settings.Default.DisableSmartZoom)
            {
                // Enforce zoom limits
                newScaleX = Math.Max(minScale, Math.Min(10, newScaleX));
                newScaleY = Math.Max(minScale, Math.Min(10, newScaleY));
            }

            // Get current image dimensions including any scaling (zoom)
            double scaledWidth = imageOriginalWidth * newScaleX;
            double scaledHeight = imageOriginalHeight * newScaleY;

            // Adjust translation to zoom around the zoom origin (namely the cursor position or center of window)
            double offsetX = zoomOrigin.X - imageTranslateTransform.X - (PrimaryWindow.ActualWidth / 2);
            double offsetY = zoomOrigin.Y - imageTranslateTransform.Y - (PrimaryWindow.ActualHeight / 2);

            imageTranslateTransform.X -= offsetX * (derivedDelta - 1);
            imageTranslateTransform.Y -= offsetY * (derivedDelta - 1);

            if (!Cloudless.Properties.Settings.Default.DisableSmartZoom)
            {
                // Constrain translations to keep the image bound within the window
                double maxTranslateX = Math.Max(0, (scaledWidth - containerWidth) / 2);
                double minTranslateX = -maxTranslateX;
                imageTranslateTransform.X = Math.Min(Math.Max(imageTranslateTransform.X, minTranslateX), maxTranslateX);

                double maxTranslateY = Math.Max(0, (scaledHeight - containerHeight) / 2);
                double minTranslateY = -maxTranslateY;
                imageTranslateTransform.Y = Math.Min(Math.Max(imageTranslateTransform.Y, minTranslateY), maxTranslateY);
            }

            // Apply new scale
            imageScaleTransform.ScaleX = newScaleX;
            imageScaleTransform.ScaleY = newScaleY;

            UpdateContextMenuState();

            UpdateCropModeInfo();
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
            //UpdateMargins();
            ClampTransformToIntuitiveBounds();
        }
        private void ResetPan()
        {
            StopPanning();
            imageTranslateTransform.X = 0;
            imageTranslateTransform.Y = 0;
        }
        private void RotateImage90Degrees()
        {
            if (ImageDisplay.Source is not BitmapSource bitmapSource)
            {
                Message("No image is currently loaded for you to rotate.");
                return;
            }

            if (currentlyDisplayedImagePath.ToLower().EndsWith(".gif"))
            {
                Message("This app does not support rotating GIFs.");
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
            if (isExplorationMode) ApplyDisplayMode();

            string displayMode = Cloudless.Properties.Settings.Default.DisplayMode;

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
                        newWidth = imageWidth * (this.Height / imageHeight);
                    }
                    else
                    {
                        newHeight = imageHeight * (this.Width / imageWidth);
                    }
                }
            }

            // Center the window
            var top = this.Top + (this.Height - newHeight) / 2;
            var left = this.Left + (this.Width - newWidth) / 2;
            RepositionWindow(left, top);
            // Apply size changes
            ResizeWindow(newWidth, newHeight);

        }

        private void MaximizeVerticalDimension()
        {
            var wa = SystemParameters.WorkArea;
            ResizeWindow(this.Width, wa.Height);
            RepositionWindow(this.Left, wa.Top);
        }

        public void NextBackground()
        {
            string selectedBackground = Properties.Settings.Default.Background;
            string nextBackground;
            if (selectedBackground == "white")
            {
                nextBackground = "transparent";
            }
            else if (selectedBackground == "transparent")
            {
                nextBackground = "black";
            }
            else  // "black"
            {
                nextBackground = "white";
            }
            Properties.Settings.Default.Background = nextBackground;

            SetBackground();
        }

        private void UpdateCropModeInfo()
        {
            if (isCropMode)
            {
                if (imageTranslateTransform != null)
                {
                    cropModeStartingImagePosX = imageTranslateTransform.X;
                    cropModeStartingImagePosY = imageTranslateTransform.Y;
                    cropModeStartingWindowHeight = this.ActualHeight;
                    cropModeStartingWindowWidth = this.ActualWidth;
                    cropModeStartingWindowTop = this.Top;
                    cropModeStartingWindowLeft = this.Left;
                }
            }
        }

        private void ToggleCropMode(bool? setTo = null)
        {
            if (setTo.HasValue)
            {
                if (setTo == isCropMode)
                    return;
                isCropMode = setTo.Value;
            }
            else
                isCropMode = !isCropMode;

            UpdateCropModeInfo();
            UpdateBorderColor();
            string message = isCropMode ? "Entered Cropping Mode" : "Exited Cropping Mode";
            Message(message);
        }

        private void UpdateBorderColor()
        {
            bool useBorder = Cloudless.Properties.Settings.Default.BorderOnMainWindow;
            if (isCropMode)
            {
                this.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                this.BorderThickness = new Thickness(2);
                // TODO for this to have the most ideal effect (red border always visible despite zoom/pan), there needs to be
                // a new window layer that is the image itself being resized etc. but not the top-level window which will have the red border.
                // This could result in some hairy bugs, and is low-priority. Return to this later.
            }
            else
            {
                this.BorderThickness = new Thickness(0);
            }

            if (useBorder)
            {
                MainBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
                MainBorder.BorderThickness = new Thickness(2);
            }
            else
            {
                MainBorder.BorderBrush = null;
                MainBorder.BorderThickness = new Thickness(0);
            }
        }

        public static void RevealImageInExplorer(string imagePath)
        {
            try
            {
                // Validate that the file path exists
                if (!File.Exists(imagePath))
                {
                    MessageBox.Show("File does not exist!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Use Process.Start to reveal the file in File Explorer
                string argument = $"/select,\"{imagePath}\"";
                Process.Start(new ProcessStartInfo("explorer.exe", argument)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Handle unexpected errors
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
