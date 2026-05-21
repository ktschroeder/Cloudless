using System;
using System.Windows;
using System.Windows.Input;

namespace Cloudless
{
    public class FilmStripWindow : Window
    {
        private FilmStripControl _control;

        public event Action<string, bool>? ThumbnailClicked;

        public FilmStripWindow()
        {
            Width = 800;
            Height = 160;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = false;

            _control = new FilmStripControl();
            _control.ThumbnailClicked += (p, open) => ThumbnailClicked?.Invoke(p, open);
            Content = _control;

            // Basic key handling to allow arrow keys to scroll
            this.PreviewKeyDown += FilmStripWindow_PreviewKeyDown;
            this.PreviewMouseWheel += FilmStripWindow_PreviewMouseWheel;
        }

        private void FilmStripWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                _control.ScrollByOffset(-e.Delta);
                e.Handled = true;
            }
            catch { }
        }

        private void FilmStripWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Left) { _control.ScrollByOffset(-200); e.Handled = true; }
                else if (e.Key == Key.Right) { _control.ScrollByOffset(200); e.Handled = true; }
            }
            catch { }
        }

        public System.Threading.Tasks.Task PopulateAsync(string[] files, int currentIndex, PreloadManager? preload)
        {
            return _control.PopulateAsync(files, currentIndex, preload);
        }

        public bool CloseAfterSelect => _control.CloseAfterSelect;
        public bool OpenInNewWindow => _control.OpenInNewWindow;
    }
}
