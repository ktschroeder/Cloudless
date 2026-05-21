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
            ShowActivated = false;

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

        public void AlignToOwner(Window owner, double desiredHeight)
        {
            if (owner == null) return;
            try
            {
                this.Owner = owner;
                // Position and size to match owner width and bottom-aligned
                this.Width = owner.ActualWidth;
                this.Left = owner.Left;
                this.Height = desiredHeight;
                this.Top = owner.Top + owner.ActualHeight - this.Height;
            }
            catch { }
        }

        public void AttachOwnerHandlers(Window owner)
        {
            if (owner == null) return;
            owner.LocationChanged += Owner_LocationOrSizeChanged;
            owner.SizeChanged += Owner_LocationOrSizeChanged;
            owner.StateChanged += Owner_StateChanged;
        }

        public void DetachOwnerHandlers(Window owner)
        {
            if (owner == null) return;
            owner.LocationChanged -= Owner_LocationOrSizeChanged;
            owner.SizeChanged -= Owner_LocationOrSizeChanged;
            owner.StateChanged -= Owner_StateChanged;
        }

        private void Owner_LocationOrSizeChanged(object? s, EventArgs e)
        {
            try
            {
                if (this.Owner != null)
                {
                    AlignToOwner(this.Owner, this.Height);
                }
            }
            catch { }
        }

        private void Owner_StateChanged(object? s, EventArgs e)
        {
            try
            {
                if (this.Owner != null && this.Owner.WindowState == WindowState.Minimized)
                    this.Hide();
            }
            catch { }
        }

        public bool CloseAfterSelect => _control.CloseAfterSelect;
        public bool OpenInNewWindow => _control.OpenInNewWindow;
    }
}
