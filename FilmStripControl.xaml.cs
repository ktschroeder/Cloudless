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
