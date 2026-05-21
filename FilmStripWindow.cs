using System;
using System.Windows;
using System.Windows.Input;
using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

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
            this.SizeChanged += FilmStripWindow_SizeChanged;
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
                const double margin = 8.0;
                double targetWidth;
                double left;
                double top;
                if (owner.WindowState == WindowState.Maximized)
                {
                    // When maximized, align to the monitor that contains the owner window
                    IntPtr hwnd = new WindowInteropHelper(owner).Handle;
                    var mi = GetMonitorWorkArea(hwnd);
                    if (mi != null)
                    {
                        targetWidth = Math.Max(200, (mi.Value.Right - mi.Value.Left) - margin * 2);
                        left = mi.Value.Left + margin;
                        top = mi.Value.Bottom - desiredHeight - margin;
                    }
                    else
                    {
                        var wa = SystemParameters.WorkArea;
                        targetWidth = Math.Max(200, wa.Width - margin * 2);
                        left = wa.Left + margin;
                        top = wa.Bottom - desiredHeight - margin;
                    }
                }
                else
                {
                    targetWidth = Math.Max(200, owner.ActualWidth - margin * 2);
                    left = owner.Left + margin;
                    top = owner.Top + owner.ActualHeight - desiredHeight - margin;
                }

                this.Width = targetWidth;
                this.Left = left;
                this.Height = desiredHeight;
                if (top < 0) top = 0;
                this.Top = top;
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
                    try { _control.AdjustThumbnailSizes(); } catch { }
                }
            }
            catch { }
        }

        private void Owner_StateChanged(object? s, EventArgs e)
        {
            try
            {
                if (this.Owner == null) return;
                if (this.Owner.WindowState == WindowState.Minimized)
                {
                    this.Hide();
                    return;
                }

                // When owner is restored or maximized, realign and ensure visible
                AlignToOwner(this.Owner, this.Height);
                try { _control.AdjustThumbnailSizes(); } catch { }
                if (!this.IsVisible)
                    this.Show();
            }
            catch { }
        }

        private void FilmStripWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            try { _control.AdjustThumbnailSizes(); } catch { }
        }

        private static RECT? GetMonitorWorkArea(IntPtr hwnd)
        {
            try
            {
                IntPtr mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (mon == IntPtr.Zero) return null;
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf<MONITORINFO>();
                if (GetMonitorInfo(mon, ref mi))
                {
                    return mi.rcWork;
                }
            }
            catch { }
            return null;
        }

        private const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        public bool CloseAfterSelect => _control.CloseAfterSelect;
        public bool OpenInNewWindow => _control.OpenInNewWindow;
    }
}
