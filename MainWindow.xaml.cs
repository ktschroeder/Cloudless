using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;
using Point = System.Windows.Point;
using System.Windows.Media;
using Path = System.IO.Path;
using Brushes = System.Windows.Media.Brushes;
using System.Diagnostics;

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        #region Fields
        public const string CURRENT_VERION = "0.5.1";

        private static readonly Mutex recentFilesMutex = new(false, "CloudlessRecentFilesMutex");
        private static readonly string recentFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cloudless",
            "recent_files.json");

        private static readonly string workspaceFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cloudless");

        public IntPtr WindowHandle =>
            new WindowInteropHelper(this).Handle;

        private string? currentDirectory;
        private string[]? imageFiles;
        private int currentImageIndex;
        private string? currentlyDisplayedImagePath;
        private bool autoResizingSpaceIsToggled;
        private bool isExplorationMode;

        private bool isCropMode;
        private double cropModeStartingImagePosX = 0;
        private double cropModeStartingImagePosY = 0;
        private double cropModeStartingWindowWidth = 0;
        private double cropModeStartingWindowHeight = 0;
        private double cropModeStartingWindowTop = 0;
        private double cropModeStartingWindowLeft = 0;
        public bool WorkspaceLoadInProgress = false;

        private OverlayMessageManager? overlayManager;

        private const int MaxRecentFiles = 15;
        private List<string> recentFiles = new();

        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        public List<string> UserCommands = new List<string>(8);

        private Point lastMousePosition;

        private bool isDraggingWindowFromFullscreen = false;
        private bool isPanningImage = false;

        public ScaleTransform? imageScaleTransform = new ScaleTransform();
        public TranslateTransform? imageTranslateTransform = new TranslateTransform();

        private TextBlock? NoImageMessage;

        private ImageAnimationController? gifController;

        private string? initialImagePath = null;
        #endregion

        #region Setup
        public MainWindow(string filePath, double windowW, double windowH)
        {
            initialImagePath = filePath;
            Setup();

            if (initialImagePath == null)
            {
                Zen(true);
            }

            ResizeWindow(windowW, windowH);
            CenterWindow();  // maybe redundant call. at some point look for other redundant calls to improve cleanliness/performance
        }
        public MainWindow(string? filePath)
        {
            //filePath = "C:\\Users\\Admin\\Downloads\\rocket.gif";  // uncomment for debugging as if opening app directly for a file

            initialImagePath = filePath;
            Setup();

            if (initialImagePath == null)
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
            ToggleCropMode(false);
            //NoImageMessage.Visibility = Visibility.Visible;
            ImageDisplay.Visibility = Visibility.Collapsed;
            CompositionTarget.Rendering += UpdateDebugInfo;

            ApplyDisplayMode();  // mostly not needed here but always-top and border and stuff is relevant

            this.KeyDown += Window_KeyDown;

            LoadRecentFiles();
            UpdateContextMenuState();
            PrepareZoomMenu();
            UpdateRecentFilesMenu();

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

        private string BuildWebmHtml(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            string uri = $"https://cloudless.local/{Uri.EscapeDataString(fileName)}";

            return $@"
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset=""utf-8"">
        <style>
            html, body {{
                margin: 0;
                background: black;
                overflow: hidden;
                width: 100%;
                height: 100%;
            }}

            video {{
                width: 100%;
                height: 100%;
                object-fit: contain;
            }}
        </style>
        </head>
        <body>
            <video autoplay loop muted controls>
                <source src=""{uri}"" type=""video/webm"">
            </video>
        </body>
        </html>";
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await WebmView.EnsureCoreWebView2Async();

                var settings = WebmView.CoreWebView2.Settings;
                settings.AreDefaultContextMenusEnabled = false;
                settings.IsZoomControlEnabled = false;

                if (initialImagePath != null)
                {
                    await LoadImage(initialImagePath, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WebView2 init failed: " + ex);
            }
        }

        private string? _mappedWebmFolder;

        private async Task EnsureWebViewForWebmAsync(string filePath)
        {
            await WebmView.EnsureCoreWebView2Async();

            string folder = Path.GetDirectoryName(filePath)!;

            if (!string.Equals(folder, _mappedWebmFolder, StringComparison.OrdinalIgnoreCase))
            {
                // Remove old mapping if any
                if (_mappedWebmFolder != null)
                {
                    WebmView.CoreWebView2.ClearVirtualHostNameToFolderMapping("cloudless.local");
                }

                WebmView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "cloudless.local",
                    folder,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow
                );

                _mappedWebmFolder = folder;
            }
        }

        private async Task<bool> ShowWebm(string filePath)
        {
            await EnsureWebViewForWebmAsync(filePath);

            WebmView.NavigateToString(BuildWebmHtml(filePath));

            WebmView.Visibility = Visibility.Visible;
            ImageDisplay.Visibility = Visibility.Collapsed;
            return true;
        }
    }
}
