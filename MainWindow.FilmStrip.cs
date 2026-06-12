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
        private CommandPaletteWindow? _commandPaletteWindow;

        private void ToggleFilmStrip_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleFilmStrip();
        }

        // Refresh the film strip contents if visible. Safe to call from other parts of MainWindow.
        public void RefreshFilmStrip()
        {
            if (_filmStripWindow != null && _filmStripWindow.IsVisible)
            {
                var files = imageFiles ?? Array.Empty<string>();
                _ = _filmStripWindow.PopulateAsync(files, currentImageIndex, _preloadManager);
            }
        }

        public void ToggleFilmStrip()
        {
            if (_filmStripWindow == null)
            {
                _filmStripWindow = new FilmStripWindow();
                _filmStripWindow.ThumbnailClicked += OnFilmStripThumbnailClicked;
            }

            if (_filmStripWindow.IsVisible)
            {
                _filmStripWindow.Hide();
                _filmStripWindow.DetachOwnerHandlers(this);
            }
            else
            {
                // Align to owner and attach handlers so it follows owner movements
                double desiredHeight = 140;
                _filmStripWindow.AlignToOwner(this, desiredHeight);
                _filmStripWindow.AttachOwnerHandlers(this);
                _filmStripWindow.Show();

                var files = imageFiles ?? Array.Empty<string>();
                _ = _filmStripWindow.PopulateAsync(files, currentImageIndex, _preloadManager);
                    
            }
            
        }

        private void OnFilmStripThumbnailClicked(string path, bool openInNewWindow)
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
                    _ = DisplayImage(idx, openedThroughApp: true);
                }
            }

            if (_filmStripWindow != null && _filmStripWindow.CloseAfterSelect)
            {
                _filmStripWindow.Hide();
            }
        }
    }
}
