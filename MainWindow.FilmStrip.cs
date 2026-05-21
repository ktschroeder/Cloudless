using System;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows;

namespace Cloudless
{
    public partial class MainWindow
    {
        public static readonly RoutedUICommand ToggleFilmStripCommand = new RoutedUICommand("ToggleFilmStrip", "ToggleFilmStrip", typeof(MainWindow));

        private FilmStripWindow? _filmStripWindow;

        private void ToggleFilmStrip_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleFilmStrip();
        }

        private void ToggleFilmStrip()
        {
            try
            {
                if (_filmStripWindow == null)
                {
                    _filmStripWindow = new FilmStripWindow();
                    _filmStripWindow.Owner = this;
                    _filmStripWindow.ThumbnailClicked += OnFilmStripThumbnailClicked;
                }

                if (_filmStripWindow.IsVisible)
                {
                    _filmStripWindow.Hide();
                }
                else
                {
                    _filmStripWindow.Show();
                    try
                    {
                        var files = imageFiles ?? Array.Empty<string>();
                        _ = _filmStripWindow.PopulateAsync(files, currentImageIndex, _preloadManager);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void OnFilmStripThumbnailClicked(string path, bool openInNewWindow)
        {
            try
            {
                if (openInNewWindow)
                {
                    var w = new MainWindow(path);
                    w.Show();
                }
                else
                {
                    int idx = Array.IndexOf(imageFiles ?? Array.Empty<string>(), path);
                    if (idx >= 0)
                    {
                        _ = DisplayImage(idx, openedThroughApp: false);
                    }
                }

                if (_filmStripWindow != null && _filmStripWindow.CloseAfterSelect)
                {
                    _filmStripWindow.Hide();
                }
            }
            catch { }
        }
    }
}
