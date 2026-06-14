using Cloudless.Properties;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cloudless
{
    public partial class FilmStripControl : UserControl
    {
        public event Action<string, bool>? ThumbnailClicked;

        public FilmStripControl()
        {
            InitializeComponent();
            this.Loaded += FilmStripControl_Loaded;

            UpdateCheckboxesFromSettings();
        }

        private bool _isResizing = false;
        private double _startMouseY = 0;
        private double _startWindowTop = 0;
        private double _startWindowHeight = 0;

        private void UpdateCheckboxesFromSettings()
        {
            PART_CloseAfterSelect.IsChecked = Settings.Default.FilmStripCloseAfterward;
            PART_OpenInNewWindow.IsChecked = Settings.Default.FilmStripOpenImageInNewWindow;
            PART_Resize.IsChecked = Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp;
        }

        private void UpdateSettingsFromCheckboxes()
        {
            Settings.Default.FilmStripCloseAfterward = PART_CloseAfterSelect.IsChecked == true;
            Settings.Default.FilmStripOpenImageInNewWindow = PART_OpenInNewWindow.IsChecked == true;
            Settings.Default.ResizeWindowToNewImageWhenOpeningThroughApp = PART_Resize.IsChecked == true;
            Settings.Default.Save();
        }

        private void PART_CloseAfterSelect_Checked(object sender, RoutedEventArgs e) => UpdateSettingsFromCheckboxes();
        private void PART_CloseAfterSelect_Unchecked(object sender, RoutedEventArgs e) => UpdateSettingsFromCheckboxes();
        private void PART_OpenInNewWindow_Checked(object sender, RoutedEventArgs e) => UpdateSettingsFromCheckboxes();
        private void PART_OpenInNewWindow_Unchecked(object sender, RoutedEventArgs e) => UpdateSettingsFromCheckboxes();
        private void PART_Resize_Checked(object sender, RoutedEventArgs e) => UpdateSettingsFromCheckboxes();
        private void PART_Resize_Unchecked(object sender, RoutedEventArgs e) => UpdateSettingsFromCheckboxes();

        private void FilmStripControl_Loaded(object? sender, RoutedEventArgs e)
        {
            var drag = this.FindName("PART_DragHandle") as FrameworkElement;
            if (drag != null)
            {
                drag.MouseLeftButtonDown += Drag_MouseLeftButtonDown;
                drag.MouseMove += Drag_MouseMove;
                drag.MouseLeftButtonUp += Drag_MouseLeftButtonUp;
            }
                
            var sv = this.FindName("PART_ScrollViewer") as ScrollViewer;
            if (sv != null)
            {
                sv.ScrollChanged += (s, ev) => UpdateOverflowIndicators();
            }
                
        }

        private void Drag_MouseLeftButtonDown(object? sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var win = Window.GetWindow(this);
            if (win == null) return;
            _isResizing = true;
            // Use screen coordinates for smooth resizing independent of window movement
            _startMouseY = GetCursorScreenY();
            _startWindowTop = win.Top;
            _startWindowHeight = win.Height;
            ((FrameworkElement)sender).CaptureMouse();
        }

        private void Drag_MouseMove(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isResizing) return;
            var win = Window.GetWindow(this);
            if (win == null) return;
            double currentY = GetCursorScreenY();
            double delta = currentY - _startMouseY;
            double newHeight = _startWindowHeight - delta;
            double newTop = _startWindowTop + delta;
            if (newHeight < 80) // minimum
            {
                newHeight = 80;
                newTop = _startWindowTop + (_startWindowHeight - 80);
            }
            win.Height = newHeight;
            win.Top = newTop;
        }

        private void Drag_MouseLeftButtonUp(object? sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isResizing = false;
            var fe = sender as FrameworkElement;
            fe?.ReleaseMouseCapture();
        }

        private static double GetCursorScreenY()
        {
            if (GetCursorPos(out POINT p))
            {
                return p.Y;
            }
            return 0;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public void ShowFilmStrip()
        {
            UpdateCheckboxesFromSettings();
            this.Visibility = Visibility.Visible;
        }

        public void HideFilmStrip()
        {
            this.Visibility = Visibility.Collapsed;
        }

        internal async Task PopulateAsync(string[] files, int currentIndex, PreloadManager? preload)
        {
            PART_Panel.Children.Clear();
            if (files == null || files.Length == 0) return;

            // Wait a bit for layout to stabilize so we can measure available height
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            double availableHeight = PART_ScrollViewer.ActualHeight;
            if (availableHeight < 40) availableHeight = 90; // default
            double thumbHeight = Math.Max(48, availableHeight - 12);
            double thumbWidth = Math.Round(thumbHeight * 1.6);

            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];

                var border = new Border
                {
                    Width = thumbWidth,
                    Height = thumbHeight,
                    Margin = new Thickness(6),
                    Background = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)),
                    CornerRadius = new CornerRadius(6),
                    Tag = path
                };

                var img = new Image
                {
                    Width = thumbWidth,
                    Height = thumbHeight,
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    SnapsToDevicePixels = true
                };

                border.Child = img;

                border.MouseLeftButtonUp += (s, e) =>
                {
                        bool openNew = PART_OpenInNewWindow.IsChecked == true;
                        ThumbnailClicked?.Invoke(path, openNew);
                };

                // hover handled via XAML style/animation; no code-behind transform here

                PART_Panel.Children.Add(border);

                // ensure clipping within rounded border
                border.ClipToBounds = true;

                BitmapSource? src = null;
                if (preload != null && preload.TryGet(path, out var cached))
                {
                    src = cached;
                }
                else
                {
                    string ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
                    bool isVideo = ext == ".webm" || ext == ".mkv" || ext == ".mp4" || ext == ".avi" || ext == ".mov";

                    if (isVideo)
                    {
                        try
                        {
                            // Ask ThumbnailService for a cached/generated thumbnail
                            src = await ThumbnailService.GetThumbnailAsync(path, (int)thumbWidth, (int)thumbHeight);
                        }
                        catch
                        {
                            src = null;
                        }
                        if (src == null)
                        {
                            string failPath = Path.Combine(AppContext.BaseDirectory, "no-thumbnail.png");
                            if (File.Exists(failPath))
                            {
                                src = new BitmapImage(new Uri(failPath));
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            // load a decoded image suitable for thumbnailing on UI thread
                            src = await Dispatcher.InvokeAsync(() =>
                            {
                                    var tmp = new BitmapImage();
                                    tmp.BeginInit();
                                    tmp.CacheOption = BitmapCacheOption.OnLoad;
                                    tmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                    // request a decoded height somewhat larger than target for quality
                                    tmp.DecodePixelHeight = (int)Math.Max(64, thumbHeight * 2);
                                    tmp.UriSource = new Uri(path);
                                    tmp.EndInit();
                                    tmp.Freeze();
                                    return (BitmapSource)tmp;
                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }
                        catch 
                        { 
                            string failPath = Path.Combine(AppContext.BaseDirectory, "no-thumbnail.png");
                            if (File.Exists(failPath))
                            {
                                src = new BitmapImage(new Uri(failPath));
                            }
                        }
                    }
                }

                if (src != null)
                {
                    // center-crop the source to match thumbnail aspect ratio
                    int sw = src.PixelWidth;
                    int sh = src.PixelHeight;
                    if (sw > 0 && sh > 0)
                    {
                        double targetRatio = thumbWidth / thumbHeight;
                        int cropW, cropH, cropX, cropY;
                        double srcRatio = (double)sw / (double)sh;
                        if (srcRatio > targetRatio)
                        {
                            // source is wider -> crop width
                            cropH = sh;
                            cropW = (int)Math.Round(sh * targetRatio);
                            cropX = (sw - cropW) / 2;
                            cropY = 0;
                        }
                        else
                        {
                            // source is taller -> crop height
                            cropW = sw;
                            cropH = (int)Math.Round(sw / targetRatio);
                            cropX = 0;
                            cropY = (sh - cropH) / 2;
                        }

                        var cb = new CroppedBitmap(src, new Int32Rect(cropX, cropY, Math.Max(1, cropW), Math.Max(1, cropH)));
                        await Dispatcher.InvokeAsync(() => img.Source = cb, System.Windows.Threading.DispatcherPriority.Background);
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() => img.Source = src, System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
            }

            await Dispatcher.InvokeAsync(() => PART_ScrollViewer.ScrollToLeftEnd());
            UpdateOverflowIndicators();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
                AdjustThumbnailSizes();
        }

        internal void AdjustThumbnailSizes()
        {
            var sv = PART_ScrollViewer;
            if (sv == null) return;

            // Determine which child is currently at the visual center of the viewport so
            // we can keep that child visually stable during resize.
            double viewportCenter = sv.HorizontalOffset + sv.ViewportWidth / 2.0;
            FrameworkElement? centerChild = null;
            double cumulative = 0;
            for (int i = 0; i < PART_Panel.Children.Count; i++)
            {
                if (PART_Panel.Children[i] is FrameworkElement fe)
                {
                    double w = fe.ActualWidth;
                    double left = cumulative;
                    double right = left + w;
                    if (viewportCenter >= left && viewportCenter <= right)
                    {
                        centerChild = fe;
                        break;
                    }
                    cumulative += w;
                }
            }

            double availableHeight = PART_ScrollViewer.ActualHeight;
            if (availableHeight < 40) availableHeight = 90;
            double thumbHeight = Math.Max(48, availableHeight - 12);
            double thumbWidth = Math.Round(thumbHeight * 1.6);

            foreach (var child in PART_Panel.Children)
            {
                if (child is Border b && b.Child is Image img)
                {
                    b.Width = thumbWidth;
                    b.Height = thumbHeight;
                    img.Width = thumbWidth;
                    img.Height = thumbHeight;
                }
            }
            //// After layout updates, reposition scroll so the centerChild remains centered
            //if (centerChild != null)
            //{
            //    Dispatcher.BeginInvoke(new Action(() =>
            //    {
            //        if (centerChild == null) return;
            //        var transform = centerChild.TransformToVisual(PART_Panel);
            //        var pt = transform.Transform(new Point(0, 0));
            //        double centerX = pt.X + centerChild.ActualWidth / 2.0;
            //        double newOffset = centerX - sv.ViewportWidth / 2.0;
            //        if (newOffset < 0) newOffset = 0;
            //        double maxOffset = Math.Max(0, PART_Panel.ActualWidth - sv.ViewportWidth);
            //        if (newOffset > maxOffset) newOffset = maxOffset;
            //        sv.ScrollToHorizontalOffset(newOffset);
            //    }), System.Windows.Threading.DispatcherPriority.Background);
            //}
            //else
            //{
                // fallback: keep leftmost offset
                Dispatcher.BeginInvoke(new Action(() => { sv.ScrollToHorizontalOffset(0); }), System.Windows.Threading.DispatcherPriority.Background);
            //}

            UpdateOverflowIndicators();
        }

        private void UpdateOverflowIndicators()
        {
            var sv = PART_ScrollViewer;
            if (sv == null) return;
            var leftRect = this.FindName("PART_LeftFade") as FrameworkElement;
            var rightRect = this.FindName("PART_RightFade") as FrameworkElement;
            if (leftRect == null || rightRect == null) return;

            bool canScrollLeft = sv.HorizontalOffset > 1.0;
            bool canScrollRight = sv.HorizontalOffset < (sv.ExtentWidth - sv.ViewportWidth - 1.0);

            leftRect.Visibility = canScrollLeft ? Visibility.Visible : Visibility.Collapsed;
            rightRect.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ScrollByOffset(double offset)
        {
            var sv = PART_ScrollViewer;
            if (sv == null) return;
            double target = sv.HorizontalOffset + offset;
            if (target < 0) target = 0;
            sv.ScrollToHorizontalOffset(target);
        }

        public bool CloseAfterSelect => PART_CloseAfterSelect.IsChecked == true;
        public bool OpenInNewWindow => PART_OpenInNewWindow.IsChecked == true;
        public bool ResizeWindow => PART_Resize.IsChecked == true;
    }
}