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
                _startMouseY = e.GetPosition(win).Y;
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
                double y = e.GetPosition(win).Y;
                double delta = y - _startMouseY;
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

                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i];

                    var border = new Border
                    {
                        Width = 140,
                        Height = 90,
                        Margin = new Thickness(6),
                        Background = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)),
                        CornerRadius = new CornerRadius(4),
                        Tag = path
                    };

                    var img = new Image
                    {
                        Width = 140,
                        Height = 90,
                        Stretch = Stretch.UniformToFill
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

                    border.MouseEnter += (s, e) => border.RenderTransform = new System.Windows.Media.ScaleTransform(1.03, 1.03);
                    border.MouseLeave += (s, e) => border.RenderTransform = new System.Windows.Media.ScaleTransform(1.0, 1.0);

                    PART_Panel.Children.Add(border);

                    BitmapImage? bmp = null;
                    if (preload != null && preload.TryGet(path, out var cached))
                    {
                        bmp = cached;
                    }
                    else
                    {
                        // try light-weight load
                        try
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                try
                                {
                                    var tmp = new BitmapImage();
                                    tmp.BeginInit();
                                    tmp.CacheOption = BitmapCacheOption.OnLoad;
                                    tmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                    tmp.DecodePixelHeight = 120;
                                    tmp.UriSource = new Uri(path);
                                    tmp.EndInit();
                                    tmp.Freeze();
                                    img.Source = tmp;
                                }
                                catch { }
                            });
                        }
                        catch { }
                    }

                    if (bmp != null)
                    {
                        await Dispatcher.InvokeAsync(() => img.Source = bmp);
                    }
                }

                await Dispatcher.InvokeAsync(() => PART_ScrollViewer.ScrollToLeftEnd());
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
