using System;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Cloudless
{
    public class OverlayMessageWindow : Window
    {
        private OverlayMessageControl? _control;

        public System.Windows.Controls.StackPanel? MessageStack => _control?.MessageStack;

        public OverlayMessageWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = false; // keep non-topmost so it does not interfere with main window hotkeys
            this.SizeToContent = SizeToContent.WidthAndHeight;

            // Use the OverlayMessageControl (copied from CommandPaletteControl) as content
            _control = new OverlayMessageControl();
            this.Content = _control;
        }

        public void AlignToOwner(Window owner, double desiredLeftOffset = 10, double desiredTopOffset = 10)
        {
            if (owner == null) return;

            this.Owner = owner;

            if (owner.WindowState == WindowState.Maximized)
            {
                IntPtr hwnd = new WindowInteropHelper(owner).Handle;
                var mi = GetMonitorWorkArea(hwnd);
                if (mi != null)
                {
                    double left = mi.Value.Left + desiredLeftOffset;
                    double top = mi.Value.Top + desiredTopOffset;
                    this.Left = left;
                    this.Top = top;
                }
                else
                {
                    var wa = SystemParameters.WorkArea;
                    this.Left = wa.Left + desiredLeftOffset;
                    this.Top = wa.Top + desiredTopOffset;
                }
            }
            else
            {
                this.Left = owner.Left + desiredLeftOffset;
                this.Top = owner.Top + desiredTopOffset;
            }
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
            if (this.Owner != null)
            {
                AlignToOwner(this.Owner);
            }
        }

        private void Owner_StateChanged(object? s, EventArgs e)
        {
            if (this.Owner == null) return;
            if (this.Owner.WindowState == WindowState.Minimized)
            {
                this.Hide();
                return;
            }

            AlignToOwner(this.Owner);
            if (!this.IsVisible)
                this.Show();
        }

        private static RECT? GetMonitorWorkArea(IntPtr hwnd)
        {
            IntPtr mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (mon == IntPtr.Zero) return null;
            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf<MONITORINFO>();
            if (GetMonitorInfo(mon, ref mi))
            {
                return mi.rcWork;
            }
            return null;
        }

        private const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
    }
}
