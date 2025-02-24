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
using System.Windows.Media.Animation;
using System.Drawing.Imaging;
using System.Collections.Specialized;
using Path = System.IO.Path;
using Brushes = System.Windows.Media.Brushes;


namespace Cloudless
{
    public partial class MainWindow : Window
    {
        #region Fields
        private static readonly Mutex recentFilesMutex = new(false, "CloudlessRecentFilesMutex");
        private static readonly string recentFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cloudless",
            "recent_files.json");

        private string? currentDirectory;
        private string[]? imageFiles;
        private int currentImageIndex;
        private string? currentlyDisplayedImagePath;
        private bool autoResizingSpaceIsToggled;
        private bool isExplorationMode;

        private OverlayMessageManager? overlayManager;

        private const int MaxRecentFiles = 10;
        private List<string> recentFiles = new();

        private Point lastMousePosition;

        private bool isDraggingWindowFromFullscreen = false;
        private bool isPanningImage = false;

        public ScaleTransform? imageScaleTransform = new ScaleTransform();
        public TranslateTransform? imageTranslateTransform = new TranslateTransform();

        private TextBlock? NoImageMessage;

        private ImageAnimationController? gifController;
        #endregion



        #region Setup
        public MainWindow(string filePath, double windowW, double windowH)
        {
            bool willLoadImage = filePath != null;
            Setup();

            if (willLoadImage)
            {
                LoadImage(filePath, false);
            }
            else
            {
                Zen(true);
            }

            ResizeWindow(windowW, windowH);
            CenterWindow();  // maybe redundant call. at some point look for other redundant calls to improve cleanliness/performance
        }
        public MainWindow(string? filePath)
        {
            //filePath = "C:\\Users\\Admin\\Downloads\\rocket.gif";  // uncomment for debugging as if opening app directly for a file

            bool willLoadImage = filePath != null;
            Setup();

            if (willLoadImage)
            {
                LoadImage(filePath, false);
            }
            else
            {
                Zen(true);
            }

            CenterWindow();
        }
        private void Setup()
        {
            InitializeComponent();

            overlayManager = new OverlayMessageManager(MessageOverlayStack);

            isExplorationMode = false;
            //NoImageMessage.Visibility = Visibility.Visible;
            ImageDisplay.Visibility = Visibility.Collapsed;
            CompositionTarget.Rendering += UpdateDebugInfo;

            ApplyDisplayMode();  // mostly not needed here but always-top and border and stuff is relevant

            this.KeyDown += Window_KeyDown;

            LoadRecentFiles();
            UpdateContextMenuState();
            UpdateRecentFilesMenu();

            RenderOptions.SetBitmapScalingMode(ImageDisplay, BitmapScalingMode.HighQuality);  // Without this, lines can appear jagged, especially for larger images that are scaled down

            InitializeZooming();
            InitializePanning();

            NoImageMessage = new TextBlock
            {
                Name = "NoImageMessage",
                Text = "Welcome to Cloudless.\n\nNo image is loaded. Right click for options.\n\nPress 'z' to toggle Zen.",
                Foreground = Brushes.White,
                FontSize = 20,
                Padding = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Visibility = Visibility.Visible,
            };

            SetBackground();

            InitializeZenMode();
        }

        

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            source.AddHook(WndProc);
        }
        private void InitializeZooming()
        {
            ImageDisplay.RenderTransform = imageScaleTransform;
            ImageDisplay.RenderTransformOrigin = new Point(0.5, 0.5);
            this.PreviewMouseWheel += OnMouseWheelZoom;
        }
        private void InitializePanning()
        {
            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(imageScaleTransform);
            transformGroup.Children.Add(imageTranslateTransform);

            ImageDisplay.RenderTransform = transformGroup;
        }
        #endregion



        #region Sensing
        private bool isDraggingWindow = false;
        private Point initialMouseScreenPosition;
        private Point initialWindowPosition;

        //Window snapping preference:
        // TODO make this a configurable setting (an advanced setting)
        //- option 1 (fluid & snapless): Native Windows snapping is disabled. When starting to drag window, regardless of whether you are holding SHIFT, you may press/release SHIFT at-will during the drag and the window will intuitively switch between constrained drag and free drag modes.
        //- option 2 (default): When you start dragging, you will enter one of two paths during the drag: If you were not initially holding Shift, native Windows snapping will be enabled, but you cannot enter constrained drag mode by then pressing SHIFT. If you were initially holding Shift, you will see behavior as described in option 1 above.
        private bool fluidSnaplessDragPreference = false;
        // It isn't feasible to dynamically switch between DragMove (needed for Windows's native snapping) and constrained movement in the same drag. DragMove prevents hitting Window_MouseMove.

        // MouseDown: Start Dragging
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (!isExplorationMode) EnterExplorationMode();

                    this.Cursor = Cursors.SizeAll;
                    isPanningImage = true;
                    lastMousePosition = e.GetPosition(this);
                    ImageDisplay.CaptureMouse(); // bookmark line. captured mouse position could be different than expected due to subsequent automatic panning such as to center/bound image?
                }
                else if (WindowState == WindowState.Maximized)
                {
                    // TODO avoided some grief by just disabling this functionality until it works fully.
                    // Frankly not needed functionality anyway
                    //isDraggingWindowFromFullscreen = true;

                    // TODO weirdness here that is new? or maybe not.
                    // may help: when going fullscreen and dragging out of it, set tracked "window position" to middle of screen instead of real last position that it would otherwise return to.
                }
                else if (WindowState == WindowState.Normal)
                {
                    isDraggingWindow = true;
                    initialMouseScreenPosition = PointToScreen(e.GetPosition(this)); // Use screen coordinates
                    initialWindowPosition = new Point(this.Left, this.Top);

                    if (fluidSnaplessDragPreference || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        Mouse.Capture(this);  // Capture mouse for consistent dragging
                    else
                        DragMove();
                }
            }
        }

        // MouseMove: Handle Dragging with Axis Constraining
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanningImage)
            {
                if (!isExplorationMode) EnterExplorationMode();

                Point currentMousePosition = e.GetPosition(this);
                Vector delta = currentMousePosition - lastMousePosition;
                ClampTransformToIntuitiveBounds(delta);
                lastMousePosition = currentMousePosition;
            }
            else if (isDraggingWindowFromFullscreen && WindowState == WindowState.Maximized)
            {
                // Handle dragging window out of fullscreen
                Point cursorPosition = e.GetPosition(this);
                ToggleFullscreen();
                isDraggingWindow = true;  // this will then hit the below if-block

                // TODO center window on cursor

                //DragMove();  // TODO exception here if you click and drag from fullscreen, and without releasing, drag to top of display to again enter fullscreen, then release.
            }
            
            if (isDraggingWindow && e.LeftButton == MouseButtonState.Pressed)
            {
                Mouse.Capture(this);  // Capture mouse for consistent dragging

                Point currentMouseScreenPosition = PointToScreen(e.GetPosition(this)); // Screen coordinates
                Vector mouseDelta = currentMouseScreenPosition - initialMouseScreenPosition;

                double newLeft = initialWindowPosition.X + mouseDelta.X;
                double newTop = initialWindowPosition.Y + mouseDelta.Y;

                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    if (Math.Abs(mouseDelta.X) > Math.Abs(mouseDelta.Y))
                    {
                        // Horizontal movement only
                        RepositionWindow(newLeft, initialWindowPosition.Y);
                    }
                    else
                    {
                        // Vertical movement only
                        RepositionWindow(initialWindowPosition.X, newTop);
                    }
                }
                else
                {
                    RepositionWindow(newLeft, newTop);
                }
            }

            if (!isPanningImage && Keyboard.Modifiers == ModifierKeys.Control && ImageDisplay.IsMouseOver)
            {
                this.Cursor = Cursors.Hand;  // could be better custom cursor
            }
        }

        // MouseUp: Stop Dragging
        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isPanningImage)
            {
                StopPanning();
            }

            if (isDraggingWindow)
            {
                Mouse.Capture(null);  // Release mouse capture
            }

            isDraggingWindow = false;
            isDraggingWindowFromFullscreen = false;
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
                    DebugTextBlockBorder.Visibility = DebugTextBlockBorder.Visibility == Visibility.Visible
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
                CenterWindow();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                OpenImage();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Z)
            {
                if (isZen)
                    RemoveZen(true);
                else
                    Zen(isWelcome);
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

            if (e.Key == Key.Space)
            {
                // bandaid fix for issue where controller gets null upon opening app directly for a GIF
                if (gifController == null && currentlyDisplayedImagePath != null && currentlyDisplayedImagePath.ToLower().EndsWith(".gif"))
                    gifController = ImageBehavior.GetAnimationController(ImageDisplay);

                // TODO changing loop preference while a gif is playing causes these controls to stop working until loading another GIF.
                if (gifController != null)
                {
                    ModifierKeys modifiers = Keyboard.Modifiers;
                    if ((modifiers & ModifierKeys.Control) != 0)
                    {
                        gifController.GotoFrame(0);
                    }
                    else
                    {
                        // play or pause GIF
                        if (gifController.IsComplete)
                            gifController.GotoFrame(0);
                        else if (gifController.IsPaused)
                            gifController.Play();
                        else
                            gifController.Pause();
                    }
                }
                
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
        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                ToggleFullscreen();
            }
        }
        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImageContextMenu.IsOpen = true;
        }
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCLIENT = 1;
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
        #endregion



        #region Zoom, Pan, and Window Sizing
        private void EnterExplorationMode()
        {
            var wasExplorationMode = isExplorationMode;

            string displayMode = Cloudless.Properties.Settings.Default.DisplayMode;
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
            bool useBorder = Cloudless.Properties.Settings.Default.BorderOnMainWindow;
            bool loopGifs = Cloudless.Properties.Settings.Default.LoopGifs;
            bool alwaysOnTopByDefault = Cloudless.Properties.Settings.Default.AlwaysOnTopByDefault;

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

            // Apply border if the flag is set
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
            ScaleImageToWindow();

            if (!isExplorationMode && Cloudless.Properties.Settings.Default.DisplayMode == "BestFitWithoutZooming")
            {
                UpdateMargins();
                // Ensure the image is not clipped by setting Stretch to Uniform // Later note TODO, this contradicts stretch mode given for zoomless best fit, and is only used in this mode too.
                ImageDisplay.Stretch = Stretch.Uniform;
            }
            else
            {
                UpdateMargins();
                ClampTransformToIntuitiveBounds();
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
                    windowWidth -= hackThickness.Left*2;
                    windowHeight -= hackThickness.Top*2;
                }

                double marginX = (windowWidth - ImageDisplay.Width) / 2;
                double marginY = (windowHeight - ImageDisplay.Height) / 2;

                ImageDisplay.Margin = new Thickness(  // black bars
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

            // Enforce zoom limits
            newScaleX = Math.Max(minScale, Math.Min(10, newScaleX));
            newScaleY = Math.Max(minScale, Math.Min(10, newScaleY));

            // Get current image dimensions including any scaling (zoom)
            double scaledWidth = imageOriginalWidth * newScaleX;
            double scaledHeight = imageOriginalHeight * newScaleY;

            // Adjust translation to zoom around the zoom origin (namely the cursor position or center of window)
            double offsetX = zoomOrigin.X - imageTranslateTransform.X - (PrimaryWindow.ActualWidth / 2);
            double offsetY = zoomOrigin.Y - imageTranslateTransform.Y - (PrimaryWindow.ActualHeight / 2);

            imageTranslateTransform.X -= offsetX * (zoomDelta - 1);
            imageTranslateTransform.Y -= offsetY * (zoomDelta - 1);

            // Constrain translations to keep the image bound within the window
            double maxTranslateX = Math.Max(0, (scaledWidth - containerWidth) / 2);
            double minTranslateX = -maxTranslateX;
            imageTranslateTransform.X = Math.Min(Math.Max(imageTranslateTransform.X, minTranslateX), maxTranslateX);

            double maxTranslateY = Math.Max(0, (scaledHeight - containerHeight) / 2);
            double minTranslateY = -maxTranslateY;
            imageTranslateTransform.Y = Math.Min(Math.Max(imageTranslateTransform.Y, minTranslateY), maxTranslateY);

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
            RepositionWindow(top, left);
            // Apply size changes
            ResizeWindow(newWidth, newHeight);

        }
        private void MaximizeVerticalDimension()
        {
            var wa = SystemParameters.WorkArea;
            ResizeWindow(this.Width, wa.Height);
            RepositionWindow(this.Left, wa.Top);
        }
        #endregion



        #region Data and Image
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
        private void LoadImage(string? imagePath, bool openedThroughApp)
        {
            try
            {
                if (imagePath == null)
                    throw new Exception("Path not specified for image.");
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

                currentImageIndex = Array.IndexOf(imageFiles, selectedImagePath);
                DisplayImage(currentImageIndex, openedThroughApp);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load the image at path \"{imagePath}\": {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void DisplayImage(int index, bool openedThroughApp)
        {
            RemoveZen();
            autoResizingSpaceIsToggled = false;

            try
            {
                if (index < 0 || imageFiles == null || index >= imageFiles.Length) return;

                var uri = new Uri(imageFiles[index]);

                currentlyDisplayedImagePath = uri.LocalPath;
                AddToRecentFiles(uri.LocalPath);

                if (gifController != null)
                {
                    gifController.Dispose();
                    gifController = null;  // can probably more efficiently reuse this. see https://github.com/XamlAnimatedGif/WpfAnimatedGif/blob/master/WpfAnimatedGif.Demo/MainWindow.xaml.cs
                }
                
                if (uri.AbsolutePath.ToLower().EndsWith(".gif"))
                {
                    var bitmap = new BitmapImage(uri);
                    ImageDisplay.Source = bitmap;  // setting this to the bitmap instead of null enables the window resizing to work properly, else the Source is at first considered null, specifically when a GIF is opened directly.
                    ImageBehavior.SetAnimatedSource(ImageDisplay, bitmap);

                    gifController = ImageBehavior.GetAnimationController(ImageDisplay);  // gets null if the app is opened directly for a GIF
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

                if (!openedThroughApp || Cloudless.Properties.Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp)
                {
                    ResizeWindowToImage();
                    CenterWindow();
                }
                    
                ApplyDisplayMode();
                UpdateContextMenuState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to display image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CopyCompressedImageToClipboardAsJpgFile()
        {
            if (ImageDisplay.Source is not BitmapSource bitmapSource)
            {
                Message("No image is currently loaded for you to copy.");
                return;
            }

            if (currentlyDisplayedImagePath.ToLower().EndsWith(".gif"))
            {
                Message("This app does not support compressing GIFs.");
                return;
            }

            string tempFilePath = GetUniqueCompressedFilePath();
            double maxSizeInMB = Cloudless.Properties.Settings.Default.MaxCompressedCopySizeMB;

            try
            {
                long finalSizeBytes = -1;
                long finalQuality = 100;

                // Define maximum size in bytes
                double maxSizeInBytes = maxSizeInMB * 1024 * 1024;

                // Convert BitmapSource to Bitmap
                using (Bitmap bitmap = BitmapSourceToBitmap(bitmapSource))
                {
                    using (Bitmap compatibleBitmap = new(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(compatibleBitmap))
                        {
                            g.DrawImage(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
                        }

                        const long qualityStep = 5L;
                        long quality = 100L;
                        const long minQuality = 10L; // Minimum quality to avoid over-compression

                        bool compressed = false;

                        while (!compressed)
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
                                    // TODO clean these up so they don't accumulate?
                                    File.WriteAllBytes(tempFilePath, memoryStream.ToArray());
                                    compressed = true;
                                }
                                else
                                {
                                    // Reduce quality for further compression
                                    quality -= qualityStep;
                                    if (quality < minQuality)
                                        throw new Exception("Unable to compress image to fit the size limit");
                                }
                            }
                        }
                    }
                }

                CopyImageAtPathToClipboard(tempFilePath);

                Message($"Copied compressed file to clipboard: {tempFilePath}. Quality: {finalQuality}%. Bytes: {finalSizeBytes}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy compressed image as file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetUniqueCompressedFilePath()
        {
            string directory = Path.GetTempPath();
            string originalFileName = Path.GetFileNameWithoutExtension(currentlyDisplayedImagePath);
            string extension = ".jpg";
            int index = 0;

            string filePath;
            do
            {
                filePath = Path.Combine(directory, $"{originalFileName}-compressed-{index}{extension}");
                index++;
            } while (File.Exists(filePath));

            return filePath;
        }

        private Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            using var ms = new MemoryStream();
            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            return new Bitmap(ms);
        }
        private ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            return ImageCodecInfo.GetImageDecoders().FirstOrDefault(codec => codec.FormatID == format.Guid);
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
        private void CopyImageToClipboard()
        {
            if (ImageDisplay.Source is not BitmapSource bitmapSource)
            {
                Message("No image is currently loaded for you to copy.");
                return;
            }

            try
            {
                if (!File.Exists(currentlyDisplayedImagePath))
                {
                    MessageBox.Show("Image file does not exist!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                    // could just copy bitmap in this case
                }

                CopyImageAtPathToClipboard(currentlyDisplayedImagePath);
                Message("Copied image file to clipboard.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy image to clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyImageAtPathToClipboard(string imagePath)
        {
            var bitmap = new Bitmap(imagePath);
            var dataObject = new DataObject();

            // Add the file path for file-pasting scenarios (e.g., Discord, file explorer)
            var filePaths = new StringCollection
                {
                    imagePath
                };
            dataObject.SetFileDropList(filePaths);

            // Add the bitmap for image-pasting scenarios (e.g., MS Paint)
            dataObject.SetData(DataFormats.Bitmap, bitmap);

            Clipboard.SetDataObject(dataObject, true);
        }

        private void AddToRecentFiles(string filePath)
        {
            // Avoid duplicates
            recentFiles.Remove(filePath);
            recentFiles.Insert(0, filePath);

            // Enforce max size
            if (recentFiles.Count > MaxRecentFiles)
                recentFiles.RemoveAt(recentFiles.Count - 1);

            SaveRecentFiles();
            UpdateRecentFilesMenu();
            
        }
        private void UpdateRecentFilesMenu() // no side effects beyond instance. Reads from static file at this time, does not write to it.
        {
            LoadRecentFiles(); // Always fetch the latest list

            // Clear the existing items
            RecentFilesMenu.Items.Clear();

            // Populate the menu
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

            // Add additional menu items
            if (recentFiles.Count > 0)
            {
                RecentFilesMenu.Items.Add(new Separator());

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

            LoadImage(filePath, true);
        }
        private void SaveRecentFiles()
        {
            if (recentFilesMutex.WaitOne(2000)) // Wait for up to 2 seconds
            {
                try
                {
                    // Ensure the directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(recentFilesPath));

                    File.WriteAllText(recentFilesPath, System.Text.Json.JsonSerializer.Serialize(recentFiles));
                }
                finally
                {
                    recentFilesMutex.ReleaseMutex();
                }
            }
            else
            {
                MessageBox.Show("Unable to write to recent files. Another instance may be busy.");
            }
        }
        private void RecentFilesMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            LoadRecentFiles();
            UpdateRecentFilesMenu();
        }

        private void LoadRecentFiles() // TODO handle exceptions: file corruption, access issues, launching with empty or missing list, manually deleting file outside of or inside of session(s).
        {
            if (recentFilesMutex.WaitOne(2000)) // Wait for up to 2 seconds
            {
                try
                {
                    if (File.Exists(recentFilesPath))
                    {
                        string json = File.ReadAllText(recentFilesPath);
                        recentFiles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    }
                    else
                    {
                        recentFiles = new List<string>();
                    }
                }
                finally
                {
                    recentFilesMutex.ReleaseMutex();
                }
            }
            else
            {
                MessageBox.Show("Unable to access recent files. Another instance may be busy.");
            }
        }

        private void ClearRecentFiles()
        {
            recentFiles.Clear();
            SaveRecentFiles();
            UpdateRecentFilesMenu();
        }
        #endregion



        #region App Navigation / Secondary Windows
        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            OpenImage();
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
                Cloudless.Properties.Settings.Default.DisplayMode = configWindow.SelectedDisplayMode;
                Cloudless.Properties.Settings.Default.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle = configWindow.ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle;
                Cloudless.Properties.Settings.Default.PixelsSpaceAroundBounds = configWindow.SpaceAroundBounds;
                Cloudless.Properties.Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp = configWindow.ResizeWindowToNewImageWhenOpeningThroughApp;
                Cloudless.Properties.Settings.Default.BorderOnMainWindow = configWindow.BorderOnMainWindow;
                Cloudless.Properties.Settings.Default.LoopGifs = configWindow.LoopGifs;
                Cloudless.Properties.Settings.Default.MuteMessages = configWindow.MuteMessages;
                Cloudless.Properties.Settings.Default.AlwaysOnTopByDefault = configWindow.AlwaysOnTopByDefault;
                Cloudless.Properties.Settings.Default.MaxCompressedCopySizeMB = configWindow.MaxCompressedCopySizeMB;
                Cloudless.Properties.Settings.Default.Background = configWindow.SelectedBackground;

                Cloudless.Properties.Settings.Default.Save();

                SetBackground();

                ApplyDisplayMode();
            }
        }
        public void SetBackground()
        {
            string selectedBackground = Properties.Settings.Default.Background;
            if (selectedBackground == "white")
            {
                this.Background = new SolidColorBrush(new System.Windows.Media.Color() { ScR = 1, ScG = 1, ScB = 1, ScA = 1 });
            }
            else if (selectedBackground == "transparent")
            {
                this.Background = new SolidColorBrush(new System.Windows.Media.Color() { ScA = 0 });
            }
            else  // "black"
            {
                this.Background = new SolidColorBrush(new System.Windows.Media.Color() { ScA = 1 });
            }
        }

        private void SetDimensions_Click(object sender, RoutedEventArgs e)
        {
            SetDimensions();
        }
        private void SetDimensions()
        {
            var setDimensionsWindow = new SetDimensionsWindow(this.Width, this.Height);

            // Center the window relative to the main application window
            setDimensionsWindow.Owner = this; // Set the owner to the main window
            setDimensionsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (setDimensionsWindow.ShowDialog() == true)
            {
                ResizeWindow(setDimensionsWindow.NewWidth, setDimensionsWindow.NewHeight);
                CenterWindow();
            }
        }
        private void DuplicateWindow_Click(object sender, RoutedEventArgs e)
        {
            DuplicateWindow();
        }
        private void DuplicateWindow()
        {
            //debugger shows binding errors but may not be real issue, see perhaps https://stackoverflow.com/questions/14526371/menuitem-added-programmatically-causes-binding-error
            var duplicateWindow = new MainWindow(currentlyDisplayedImagePath, this.Width, this.Height);
            // could also include viewing mode, possibly panning/zooming etc. But this can get messy and imperfect unless total state is matched properly (preferences may not be aligned)

            //setDimensionsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            duplicateWindow.Show();
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

            aboutWindow.Show();
        }
        private void HotkeyRef_Click(object sender, RoutedEventArgs e)
        {
            HotkeyRef();
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
        private void OpenMessageHistory_Click(object sender, RoutedEventArgs e)
        {
            var historyWindow = new MessageHistoryWindow(overlayManager);
            historyWindow.Owner = this; // Set the owner to the main window
            historyWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            historyWindow.Show();
        }
        #endregion



        #region Other
        private void UpdateDebugInfo(object? sender, EventArgs e)
        {
            if (ImageDisplay == null || DebugTextBlock.Visibility != Visibility.Visible ) 
                return;

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

            string displayMode = Cloudless.Properties.Settings.Default.DisplayMode;

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
            $"Left Margin: {ImageDisplay.Margin.Left:F2}\n" +
            $"Top Margin: {ImageDisplay.Margin.Top:F2}\n" +
            $"Scale: X={scaleX:F2}, Y={scaleY:F2}\n" +
            $"Translation: X={translateX:F2}, Y={translateY:F2}\n" +
            $"Cursor (Window): X={cursorPosition.X:F2}, Y={cursorPosition.Y:F2}\n" +
            $"Cursor (Image): X={cursorPositionImage.X:F2}, Y={cursorPositionImage.Y:F2}\n" +
            $"Display mode: {displayMode:F2}";
        }
        public void Message(string message, TimeSpan? duration = null)
        {
            duration ??= TimeSpan.FromSeconds(1.5);
            overlayManager.ShowOverlayMessage(message, (TimeSpan)duration);
        }
        #endregion



    }
}
