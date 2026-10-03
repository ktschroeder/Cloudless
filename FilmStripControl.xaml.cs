using Cloudless.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cloudless
{
    public partial class FilmStripControl : UserControl
    {
        public event Action<string, bool>? ThumbnailClicked;

        private CancellationTokenSource? _populateCts;
        private ToolTip? _activeThumbnailToolTip;

        // Static retain storage - global in-memory filmstrip contents retained across instances
        private static string[]? _retainedFilmstripContents;
        private static HashSet<FilmStripControl> _allInstances = new HashSet<FilmStripControl>();

        public FilmStripControl()
        {
            InitializeComponent();
            this.Loaded += FilmStripControl_Loaded;

            UpdateCheckboxesFromSettings();

            // Initialize retain checkbox to unchecked (it will be set to checked only if retain is actually active)
            PART_RetainContents.IsChecked = false;

            // Register this instance globally
            lock (_allInstances)
            {
                _allInstances.Add(this);
            }
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
                sv.ScrollChanged += (s, ev) =>
                {
                    UpdateOverflowIndicators();
                    if (ev.HorizontalChange != 0)
                        HideActiveThumbnailToolTip();
                };
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
            // Cancel any previous population operation
            if (_populateCts != null)
            {
                _populateCts.Cancel();
                _populateCts.Dispose();
            }

            _populateCts = new CancellationTokenSource();
            var cancellationToken = _populateCts.Token;

            PART_Panel.Children.Clear();
            if (files == null || files.Length == 0) return;

            // Wait a bit for layout to stabilize so we can measure available height
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

            // Check if cancelled before continuing
            if (cancellationToken.IsCancellationRequested)
                return;

            double availableHeight = PART_ScrollViewer.ActualHeight;
            if (availableHeight < 40) availableHeight = 90; // default
            double thumbHeight = Math.Max(48, availableHeight - 12);
            double thumbWidth = Math.Round(thumbHeight * 1.6);

            for (int i = 0; i < files.Length; i++)
            {
                // Check for cancellation at each iteration
                if (cancellationToken.IsCancellationRequested)
                    return;

                string path = files[i];

                var tooltip = new ToolTip { Content = GetTooltipFileName(path) };
                tooltip.Opened += (s, e) => _activeThumbnailToolTip = tooltip;
                tooltip.Closed += (s, e) =>
                {
                    if (ReferenceEquals(_activeThumbnailToolTip, tooltip))
                        _activeThumbnailToolTip = null;
                };

                var border = new Border
                {
                    Width = thumbWidth,
                    Height = thumbHeight,
                    Margin = new Thickness(6),
                    Background = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)),
                    CornerRadius = new CornerRadius(6),
                    Tag = path,
                    ToolTip = tooltip
                };
                ToolTipService.SetInitialShowDelay(border, 500);
                ToolTipService.SetBetweenShowDelay(border, 0);

                var img = new Image
                {
                    Width = thumbWidth,
                    Height = thumbHeight,
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    SnapsToDevicePixels = true
                };

                var thumbnailGrid = new Grid
                {
                    Width = thumbWidth,
                    Height = thumbHeight
                };
                thumbnailGrid.Children.Add(img);
                thumbnailGrid.Children.Add(new VideoThumbnailOverlay
                {
                    IsVideo = VideoThumbnailOverlay.IsVlcVideoPath(path)
                });
                var animatedOverlay = new AnimatedThumbnailOverlay();
                thumbnailGrid.Children.Add(animatedOverlay);
                border.Child = thumbnailGrid;

                if (AnimatedImageDetector.IsSupportedAnimatedImagePath(path))
                    _ = SetAnimatedOverlayAsync(path, animatedOverlay, cancellationToken);

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

                    var typesInNamespace = Assembly.GetExecutingAssembly().GetTypes()
                        .Where(t => t.IsClass && t.Namespace == "Cloudless.FileTypes").ToList();

                    // TODO

                    //FileType? fileType = FileTypeManager.GetFileTypes().FirstOrDefault(ft => ft.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase));

                    bool isVideo = (ext == ".webm" || ext == ".mkv" || ext == ".mp4");

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

                // Check again before updating UI with loaded image
                if (cancellationToken.IsCancellationRequested)
                    return;

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
                        await Dispatcher.InvokeAsync(() => {
                            if (!cancellationToken.IsCancellationRequested)
                                img.Source = cb;
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() => {
                            if (!cancellationToken.IsCancellationRequested)
                                img.Source = src;
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(() => PART_ScrollViewer.ScrollToLeftEnd());
                UpdateOverflowIndicators();
            }
        }

        private async Task SetAnimatedOverlayAsync(string path, AnimatedThumbnailOverlay overlay, CancellationToken cancellationToken)
        {
            bool isAnimated = await AnimatedImageDetector.IsAnimatedAsync(path);
            if (cancellationToken.IsCancellationRequested)
                return;

            await Dispatcher.InvokeAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                    overlay.IsAnimated = isAnimated;
            }, System.Windows.Threading.DispatcherPriority.Background);
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
                if (child is Border b && b.Child is Grid thumbnailGrid && thumbnailGrid.Children.OfType<Image>().FirstOrDefault() is Image img)
                {
                    b.Width = thumbWidth;
                    b.Height = thumbHeight;
                    thumbnailGrid.Width = thumbWidth;
                    thumbnailGrid.Height = thumbHeight;
                    img.Width = thumbWidth;
                    img.Height = thumbHeight;
                }
            }

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
            double target = Math.Clamp(sv.HorizontalOffset + offset, 0, sv.ScrollableWidth);
            if (target != sv.HorizontalOffset)
                HideActiveThumbnailToolTip();
            sv.ScrollToHorizontalOffset(target);
        }

        public bool IsPointerOverOptions => PART_OptionsScrollViewer.IsMouseOver;

        public void ScrollOptionsByOffset(double offset)
        {
            var scrollViewer = PART_OptionsScrollViewer;
            double target = Math.Clamp(scrollViewer.HorizontalOffset + offset, 0, scrollViewer.ScrollableWidth);
            if (target != scrollViewer.HorizontalOffset)
                HideActiveThumbnailToolTip();
            scrollViewer.ScrollToHorizontalOffset(target);
        }

        private void PART_OptionsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange != 0)
                HideActiveThumbnailToolTip();

            bool optionsFit = e.ExtentWidth <= e.ViewportWidth + 1;
            PART_OptionsPanel.HorizontalAlignment = optionsFit ? HorizontalAlignment.Center : HorizontalAlignment.Left;

            if (optionsFit && PART_OptionsScrollViewer.HorizontalOffset > 0)
                PART_OptionsScrollViewer.ScrollToHorizontalOffset(0);
        }

        private void HideActiveThumbnailToolTip()
        {
            if (_activeThumbnailToolTip?.IsOpen == true)
                _activeThumbnailToolTip.IsOpen = false;
        }

        public bool CloseAfterSelect => PART_CloseAfterSelect.IsChecked == true;
        public bool OpenInNewWindow => PART_OpenInNewWindow.IsChecked == true;
        public bool ResizeWindow => PART_Resize.IsChecked == true;

        /// <summary>
        /// Returns true if there are retained filmstrip contents available globally.
        /// </summary>
        public static bool HasRetainedContents => _retainedFilmstripContents?.Length > 0;

        /// <summary>
        /// Gets the retained filmstrip contents, or null if none are retained.
        /// </summary>
        public static string[]? GetRetainedContents => _retainedFilmstripContents;

        /// <summary>
        /// Gets the current filmstrip file list by examining child borders in PART_Panel.
        /// </summary>
        private string[] GetCurrentFilmstripFiles()
        {
            var panel = this.FindName("PART_Panel") as StackPanel;
            if (panel == null) return Array.Empty<string>();

            return panel.Children.OfType<Border>()
                .Where(b => b.Tag is string)
                .Select(b => (string)b.Tag)
                .ToArray();
        }

        internal static string GetTooltipFileName(string path)
        {
            const int maxLength = 48;
            string fileName = Path.GetFileName(path);
            if (fileName.Length <= maxLength)
                return fileName;

            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
                return fileName.Substring(0, maxLength - 1) + "…";

            string suffix = $"… [{extension}]";
            int stemLength = Math.Max(0, maxLength - suffix.Length);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            return stem.Substring(0, Math.Min(stem.Length, stemLength)) + suffix;
        }

        private void PART_RetainContents_Checked(object sender, RoutedEventArgs e)
        {
            // Save current filmstrip contents and mark this instance as the last retained
            _retainedFilmstripContents = GetCurrentFilmstripFiles();

            // Clear the retain checkbox in all other instances globally
            ClearRetainCheckboxInAllOtherInstances(this);
        }

        private void PART_RetainContents_Unchecked(object sender, RoutedEventArgs e)
        {
            // Clear retained contents from memory and uncheck all instances globally
            _retainedFilmstripContents = null;

            // Clear retain checkbox in all instances (including this one, but SetRetainCheckboxCheckedSilently
            // will prevent recursive event triggers)
            ClearRetainCheckboxInAllInstances();
        }

        /// <summary>
        /// Sets the retain checkbox state for this instance without triggering the Checked/Unchecked events.
        /// </summary>
        public void SetRetainCheckboxCheckedSilently(bool isChecked)
        {
            // Ensure we're on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetRetainCheckboxCheckedSilently(isChecked));
                return;
            }

            CheckBox? checkbox = null;

            // Try to find the checkbox by name
            try
            {
                checkbox = this.FindName("PART_RetainContents") as CheckBox;
            }
            catch
            {
                // FindName might fail in some scenarios
            }

            // If FindName didn't work, search the visual tree directly
            if (checkbox == null)
            {
                checkbox = FindVisualChild<CheckBox>(this, "PART_RetainContents");
            }

            if (checkbox != null)
            {
                // Temporarily disable event handlers
                checkbox.Checked -= PART_RetainContents_Checked;
                checkbox.Unchecked -= PART_RetainContents_Unchecked;

                try
                {
                    // Set the property using SetCurrentValue to avoid property change notifications
                    checkbox.SetCurrentValue(ToggleButton.IsCheckedProperty, isChecked);
                }
                finally
                {
                    // Re-add event handlers even if setting the value failed
                    checkbox.Checked += PART_RetainContents_Checked;
                    checkbox.Unchecked += PART_RetainContents_Unchecked;
                }
            }
        }

        /// <summary>
        /// Helper to find a CheckBox in the visual tree by name.
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t && t.Name == name)
                {
                    return t;
                }

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Marks this instance as displaying retained contents.
        /// </summary>
        public void MarkAsDisplayingRetainedContents()
        {
            SetRetainCheckboxCheckedSilently(true);
        }

        /// <summary>
        /// Clears the retain checkbox in the specified instance without triggering events.
        /// </summary>
        private static void ClearCheckboxInInstance(FilmStripControl instance)
        {
            if (instance != null)
            {
                instance.SetRetainCheckboxCheckedSilently(false);
            }
        }

        /// <summary>
        /// Clears the retain checkbox in all instances except the specified one.
        /// </summary>
        public static void ClearRetainCheckboxInAllOtherInstances(FilmStripControl exceptInstance)
        {
            lock (_allInstances)
            {
                foreach (var instance in _allInstances)
                {
                    if (instance != exceptInstance)
                    {
                        ClearCheckboxInInstance(instance);
                    }
                }
            }
        }

        /// <summary>
        /// Clears the retain checkbox in all instances globally.
        /// </summary>
        private static void ClearRetainCheckboxInAllInstances()
        {
            lock (_allInstances)
            {
                foreach (var instance in _allInstances)
                {
                    ClearCheckboxInInstance(instance);
                }
            }
        }

        /// <summary>
        /// Clears the retain checkbox on this specific instance only, without affecting global retain state or other windows.
        /// Used when an explicit filmstrip target is sent to this window.
        /// </summary>
        public void ClearRetainCheckboxOnThisInstanceOnly()
        {
            SetRetainCheckboxCheckedSilently(false);
        }

        /// <summary>
        /// Returns a public accessor to the FilmStripControl instance from a FilmStripWindow.
        /// </summary>
        public static FilmStripControl? GetControlFromWindow(Window? window)
        {
            if (window is FilmStripWindow fsw)
            {
                return fsw.Content as FilmStripControl;
            }
            return null;
        }
    }
}
