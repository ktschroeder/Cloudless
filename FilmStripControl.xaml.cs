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
        }

        private bool _isResizing = false;
        private double _startMouseY = 0;
        private double _startWindowTop = 0;
        private double _startWindowHeight = 0;

        private void FilmStripControl_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                var drag = this.FindName("PART_DragHandle") as FrameworkElement;
                if (drag != null)
                {
                    drag.MouseLeftButtonDown += Drag_MouseLeftButtonDown;
                    drag.MouseMove += Drag_MouseMove;
                    drag.MouseLeftButtonUp += Drag_MouseLeftButtonUp;
                }
            }
            catch { }
        }

        private void Drag_MouseLeftButtonDown(object? sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
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
            catch { }
        }

        private void Drag_MouseMove(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            try
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
            catch { }
        }

        private void Drag_MouseLeftButtonUp(object? sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                _isResizing = false;
                var fe = sender as FrameworkElement;
                fe?.ReleaseMouseCapture();
            }
            catch { }
        }

        private static double GetCursorScreenY()
        {
            try
            {
                if (GetCursorPos(out POINT p))
                {
                    return p.Y;
                }
            }
            catch { }
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
            this.Visibility = Visibility.Visible;
        }

        public void HideFilmStrip()
        {
            this.Visibility = Visibility.Collapsed;
        }

        internal async Task PopulateAsync(string[] files, int currentIndex, PreloadManager? preload)
        {
            try
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
                        try
                        {
                            bool openNew = PART_OpenInNewWindow.IsChecked == true;
                            ThumbnailClicked?.Invoke(path, openNew);
                        }
                        catch { }
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
                        try
                        {
                            // load a decoded image suitable for thumbnailing on UI thread
                            src = await Dispatcher.InvokeAsync(() =>
                            {
                                try
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
                                }
                                catch { }
                                return null;
                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }
                        catch { }
                    }

                    if (src != null)
                    {
                        try
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
                        catch { }
                    }
                }

                await Dispatcher.InvokeAsync(() => PART_ScrollViewer.ScrollToLeftEnd());
            }
            catch { }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            try
            {
                AdjustThumbnailSizes();
            }
            catch { }
        }

        internal void AdjustThumbnailSizes()
        {
            try
            {
                var sv = PART_ScrollViewer;
                if (sv == null) return;

                // Preserve visual center ratio of the content so expansion happens around current view
                double oldExtent = PART_Panel.ActualWidth;
                double oldCenter = sv.HorizontalOffset + sv.ViewportWidth / 2.0;
                double oldCenterRatio = (oldExtent > 0) ? (oldCenter / oldExtent) : 0.5;

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

                // After layout updates, reposition scroll so center remains consistent
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        double newExtent = PART_Panel.ActualWidth;
                        double newCenter = newExtent * oldCenterRatio;
                        double newOffset = newCenter - sv.ViewportWidth / 2.0;
                        if (newOffset < 0) newOffset = 0;
                        double maxOffset = Math.Max(0, newExtent - sv.ViewportWidth);
                        if (newOffset > maxOffset) newOffset = maxOffset;
                        sv.ScrollToHorizontalOffset(newOffset);
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch { }
        }

        public void ScrollByOffset(double offset)
        {
            try
            {
                var sv = PART_ScrollViewer;
                if (sv == null) return;
                double target = sv.HorizontalOffset + offset;
                if (target < 0) target = 0;
                sv.ScrollToHorizontalOffset(target);
            }
            catch { }
        }

        public bool CloseAfterSelect => PART_CloseAfterSelect.IsChecked == true;
        public bool OpenInNewWindow => PART_OpenInNewWindow.IsChecked == true;
    }
}
