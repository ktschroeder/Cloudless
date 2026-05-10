//using WpfAnimatedGif;
using AnimatedImage;
using AnimatedImage.Wpf;
using Cloudless.PluginBase;
using Cloudless.Properties;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Path = System.IO.Path;
using Point = System.Windows.Point;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        public const string CURRENT_VERSION = "0.7.4";
        // RemoveBeforeFlight
        public const bool LOCAL_DEV = true;



        #region Fields

        private static readonly Mutex recentFilesMutex = new(false, "CloudlessRecentFilesMutex");
        private static readonly string recentFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cloudless",
            "recent_files.json");

        public static readonly string workspaceFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cloudless", "workspaces");

        //public static readonly string systemWorkspaceFilesPath = Path.Combine(
        //    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        //    "Cloudless", "workspaces", "system");

        public static readonly string pluginsFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cloudless", "plugins");

        public static readonly string droppedInFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cloudless", "dropped_in_files");

        public IntPtr WindowHandle =>
            new WindowInteropHelper(this).Handle;

        private string? currentDirectory;
        private string[]? imageFiles;
        private int currentImageIndex;
        private bool autoResizingSpaceIsToggled;
        private bool isExplorationMode;
        private string? currentlyDisplayedImagePath;

        private bool isCropMode;
        private double cropModeStartingImagePosX = 0;
        private double cropModeStartingImagePosY = 0;
        private double cropModeStartingWindowWidth = 0;
        private double cropModeStartingWindowHeight = 0;
        private double cropModeStartingWindowTop = 0;
        private double cropModeStartingWindowLeft = 0;
        public bool WorkspaceLoadInProgress = false;
        public string? imageOriginalWorkspaceName;

        private OverlayMessageManager? overlayManager;

        private const int MaxRecentFilesInGallery = 30;
        private const int MaxRecentFilesInContextWindow = 5;
        private List<string> recentFiles = new();

        public List<string> CommandHistory = new();
        public int CommandHistoryIndex = -1;
        public List<string> UserCommands = new List<string>(8);

        private Point lastMousePosition;

        private bool isDraggingWindowFromFullscreen = false;
        private bool isPanningImage = false;

        public ScaleTransform? imageScaleTransform = new ScaleTransform();
        public TranslateTransform? imageTranslateTransform = new TranslateTransform();

        private TextBlock? NoImageMessage = null;

        private ImageAnimationController? animationController;

        private string? initialImageToLoad;

        private CloudlessWindowState? stateUponMinimizing = null;
        #endregion

        #region Setup
        public MainWindow(string filePath, double windowW, double windowH)
        {
            initialImageToLoad = filePath;
            Setup();

            if ((Path.GetExtension(filePath) ?? "").ToLower().Equals(".cloudless"))
            {
                return;
            }

            if (string.IsNullOrEmpty(initialImageToLoad))
            {
                Zen(true);
            }

            ResizeWindow(windowW, windowH);
            CenterWindowForStartup();  // maybe redundant call. at some point look for other redundant calls to improve cleanliness/performance
        }
        public MainWindow(string? filePath, bool startUp = false)
        {
            //filePath = "C:\\Users\\Admin\\Downloads\\rocket.gif";  // uncomment for debugging as if opening app directly for a file
            initialImageToLoad = filePath;
            Setup(startUp: startUp);

            if ((Path.GetExtension(filePath) ?? "").ToLower().Equals(".cloudless"))
            {
                return;
            }

            if (string.IsNullOrEmpty(initialImageToLoad))
            {
                Zen(true);
            }

            CenterWindowForStartup();
        }
        private async Task ExecuteCloudlessFile(string filePath)
        {
            // TODO if things get too complicated here, we can just open a blank instance as normal, then execute merge command as normal. Consider whether there is already a cloudless instance open.
            string workspaceName = Path.GetFileNameWithoutExtension(filePath);
            await LoadWorkspace(workspaceName);  // TODO consider whether to merge instead here. Maybe user config to choose?
        }
        private void OnClose()
        {
            // Dispose of media elements to protect against memory leaks

            if (VideoHost.Content is Cloudless.PluginBase.IVideoPlayer videoPlayer)
            {
                videoPlayer.Dispose();
            }

            // bandaid fix for issue where controller gets null upon opening app directly for a GIF
            //if (gifController == null && currentlyDisplayedImagePath != null && currentlyDisplayedImagePath.ToLower().EndsWith(".gif"))
            animationController = ImageBehavior.GetAnimationController(ImageDisplay);  // gets null when there isn't one
            if (animationController != null)  // weirdly, this is somehow null sometimes when closing a window that has a GIF loaded. Could contribute to memory leak danger.
            {
                animationController.Dispose();
                animationController = null;
            }

            if (ImageDisplay.Source is BitmapImage bi)
            {
                bi.StreamSource?.Dispose();
            }

            var animatedSource = ImageBehavior.GetAnimatedSource(ImageDisplay);

            if (animatedSource is ImageSource src)
            {
                // TODO
            }

            ImageBehavior.SetAnimatedSource(ImageDisplay, null);

            ImageDisplay.Source = null;

            System.GC.Collect();
            //System.GC.WaitForPendingFinalizers();
        }
        private void Setup(bool startUp = false)
        {
            InitializeComponent();

            Closing += (sender, e) => OnClose();

            try
            {
                PluginManager.InitializePlugins();
            }
            catch (Exception ex)
            {
                Message("Error preparing plugins: " + ex.Message);
            }
            
            
            overlayManager = new OverlayMessageManager(MessageOverlayStack);
            if (startUp)
                overlayManager.ClearMessageHistory();

            isExplorationMode = false;
            ToggleCropMode(false);
            //NoImageMessage.Visibility = Visibility.Visible;
            ImageDisplay.Visibility = Visibility.Collapsed;
            CompositionTarget.Rendering += UpdateDebugInfo;

            ApplyDisplayMode();  // mostly not needed here but always-top and border and stuff is relevant

            this.KeyDown += Window_KeyDown;

            LoadRecentFiles();
            
            RenderOptions.SetBitmapScalingMode(ImageDisplay, BitmapScalingMode.HighQuality);  // Without this, lines can appear jagged, especially for larger images that are scaled down

            InitializeZooming();
            InitializePanning();

            NoImageMessage = new TextBlock
            {
                Name = "NoImageMessage",
                Text = "Welcome to Cloudless.\n\nNo image is loaded.\nRight click or press 'x' for options.\n\nPress 'z' to toggle Zen.",
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

            _ = CheckForUpdatesAsync();  // fire and forget check for newer app version
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await UpdateContextMenuState();
            PrepareZoomMenu();

            await UpdateRecentFilesMenu(isStartUp: true);

            if ((Path.GetExtension(initialImageToLoad) ?? "").ToLower().Equals(".cloudless"))
            {
                await ExecuteCloudlessFile(initialImageToLoad);
            }
            else if (!string.IsNullOrEmpty(initialImageToLoad) && WorkspaceLoadInProgress == false)
            {
                await LoadImage(initialImageToLoad, false);
                Activate();
            }

            

            //Topmost = true;
            //Topmost = false;
            //Focus();
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
            double? scaleX = imageScaleTransform?.ScaleX;
            double? scaleY = imageScaleTransform?.ScaleY;

            // Image translation
            double? translateX = imageTranslateTransform?.X;
            double? translateY = imageTranslateTransform?.Y;

            // Cursor position relative to the window
            Point cursorPosition = Mouse.GetPosition(this);

            // Cursor position relative to the image
            Point cursorPositionImage = Mouse.GetPosition(ImageDisplay);

            string displayMode = Cloudless.Properties.Settings.Default.DisplayMode;

            double? imageTrueWidth = null;
            double? imageTrueHeight = null;
            double? realScale = null;
            double? tloX = null;  // Top Left Origin, as opposed to central origin. These values are with respect to coordinates on the original image in original dimensions.
            double? tloY = null;  // This TLO debug info is to help sort out the advanced wallpaper feature
            double? tloWidth = null;
            double? tloHeight = null;
            if (ImageDisplay.Source is BitmapSource bitmap)
            {
                imageTrueWidth = bitmap.PixelWidth;
                imageTrueHeight = bitmap.PixelHeight;
                realScale = imageWidth / (double)imageTrueWidth * (double)scaleX;  // ignores nuance if x and y scales don't match, i.e. stretching

                if (WindowState == WindowState.Maximized)
                {
                    var diff = GetHackBorderSizeWhenFullscreen().Left * 2;
                    windowHeight -= diff;
                    windowWidth -= diff;
                }

                // evil graphics math
                tloX = imageTrueWidth / 2 - (translateX + windowWidth / 2) / realScale;
                tloY = imageTrueHeight / 2 - (translateY + windowHeight / 2) / realScale;
                tloWidth = windowWidth / realScale;
                tloHeight = windowHeight / realScale;
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
            $"Real Zoom: {realScale ?? 0:F2}\n" +
            $"Translation: X={translateX:F2}, Y={translateY:F2}\n" +
            $"Effective crop rect with TLO: X={tloX ?? 0:F2}, Y={tloY ?? 0:F2}\n" +
            $"Effective crop rect with TLO: Width={tloWidth ?? 0:F2}, Height={tloHeight ?? 0:F2}\n" +
            $"Cursor (Window): X={cursorPosition.X:F2}, Y={cursorPosition.Y:F2}\n" +
            $"Cursor (Image): X={cursorPositionImage.X:F2}, Y={cursorPositionImage.Y:F2}\n" +
            $"Display mode: {displayMode:F2}";
        }
        public void Message(string message, TimeSpan? duration = null)
        {
            duration ??= TimeSpan.FromSeconds(1.5);
            overlayManager?.ShowOverlayMessage(message, (TimeSpan)duration);
        }

        private void ShowLoadingOverlay(string text1 = "Loading...", string text2 = "")
        {
            LoadingText1.Text = text1;
            LoadingText2.Text = text2;
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingOverlay.UpdateLayout();   // force immediate render
        }

        private void HideLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private static void ZenOrUnzenAllWindows()  // a bit slow for larger workspaces. make concurrent probs.
        {
            int zenCount = 0;
            var windows = Application.Current.Windows.OfType<MainWindow>();
            foreach (var window in windows)
            {
                if (window.isZen)
                    zenCount++;
            }

            if (zenCount == windows.Count())  // then all windows are currently zen. So un-zen them all.
            {
                foreach (var window in windows)
                {
                    window.RemoveZen();
                }
            }
            else
            {
                foreach (var window in windows)
                {
                    window.Zen(false);
                }
            }
        }

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "Cloudless";

        public void SetStartup(bool enable)
        {
            if (LOCAL_DEV)
                return;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);

                if (key == null)
                    return;

                if (enable)
                {
                    string exePath = Process.GetCurrentProcess().MainModule!.FileName!;
                    key.SetValue(AppName, $"\"{exePath}\"  --background");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Message("Error setting startup: " + ex.Message);
            }
        }

        public bool IsStartupEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);

            var value = key?.GetValue(AppName) as string;
            return !string.IsNullOrEmpty(value);
        }

        private DispatcherTimer _rightClickHoldTimer;
        private DispatcherTimer _middleClickHoldTimer;
        public bool SkipNextContextMenu = false;

        private void Window_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TimeSpan RIGHT_CLICK_LONG_HOLD = TimeSpan.FromMilliseconds(Settings.Default.MouseLongPressMS);

            _rightClickHoldTimer = new DispatcherTimer { Interval = RIGHT_CLICK_LONG_HOLD };
            
            _rightClickHoldTimer.Tick += (s, args) =>
            {
                _rightClickHoldTimer.Stop();
                ToggleMouseControlMode();
                SkipNextContextMenu = true;
                e.Handled = true;
            };
            _rightClickHoldTimer.Start();
        }

        private void Window_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _rightClickHoldTimer?.Stop();
        }

        private void Window_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (SkipNextContextMenu)
                e.Handled = true;

            SkipNextContextMenu = false;
        }

        public bool MouseControlMode = false;
        public bool MouseCommandMode = false;
        private void ToggleMouseControlMode()
        {
            if (MouseControlMode)
            {
                this.Cursor = Cursors.Arrow;
                MouseControlMode = false;
            }
            else if (!MouseControlMode) 
            {
                this.Cursor = Cursors.ScrollAll;
                MouseControlMode = true;
                MouseCommandMode = false;
            }
        }
        private void ToggleMouseCommandMode()
        {
            if (MouseCommandMode)
            {
                this.Cursor = Cursors.Arrow;
                MouseCommandMode = false;
            }
            else if (!MouseCommandMode)
            {
                var streamInfo = Application.GetResourceStream(new Uri("custom_cursor.cur", UriKind.Relative));
                this.Cursor = new Cursor(streamInfo.Stream);

                //this.Cursor = Cursors.Cross;
                MouseControlMode = false;
                MouseCommandMode = true;
            }
        }
    }
}
