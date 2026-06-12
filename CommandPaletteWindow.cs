using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Cloudless
{
    public class CommandPaletteWindow : Window
    {
        public CommandPaletteControl? Control { get; set; }
        //private MainWindow _mw;

        public CommandPaletteWindow(MainWindow mw)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = false; // keep non-topmost so it does not interfere with main window hotkeys
            ShowActivated = false;
            this.SizeToContent = SizeToContent.WidthAndHeight;

            Control = new CommandPaletteControl(mw);
            this.Content = Control;
        }

        public void AlignToOwner(Window owner, double desiredLeftOffset = 7, double desiredBottomOffset = 7, double? contentHeight = null)
        {
            if (owner == null) return;

            this.Owner = owner;
            const double margin = 1.0; // inner margin around film strip; palette will use desired offsets

            double heightToUse = contentHeight ?? this.ActualHeight;
            if (owner.WindowState == WindowState.Maximized)
            {
                IntPtr hwnd = new WindowInteropHelper(owner).Handle;
                var mi = GetMonitorWorkArea(hwnd);
                if (mi != null)
                {
                    double left = mi.Value.Left + desiredLeftOffset;
                    double top = mi.Value.Bottom - heightToUse - desiredBottomOffset;
                    this.Left = left;
                    this.Top = top;
                }
                else
                {
                    var wa = SystemParameters.WorkArea;
                    this.Left = wa.Left + desiredLeftOffset;
                    double top = wa.Bottom - heightToUse - desiredBottomOffset;
                    this.Top = top;
                }
            }
            else
            {
                this.Left = owner.Left + desiredLeftOffset;
                double top = owner.Top + owner.ActualHeight - heightToUse - desiredBottomOffset;
                this.Top = top;
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
            try
            {
                if (this.Owner != null)
                {
                    AlignToOwner(this.Owner);
                }
            }
            catch { }
        }

        public void ShowAndFocus(UIElement focusTarget, Window owner)
        {
            try
            {
                // Measure content so we can position before showing to avoid flicker
                double desiredHeight = this.ActualHeight;
                if (this.Content is FrameworkElement fe)
                {
                    try
                    {
                        fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        var ds = fe.DesiredSize;
                        if (!double.IsNaN(ds.Height) && ds.Height > 0)
                            desiredHeight = ds.Height;
                    }
                    catch { }
                }

                AlignToOwner(owner, 7, 7, desiredHeight);
                if (!this.IsVisible)
                    this.Show();

                // Focus the provided element synchronously so input is accepted immediately
                if (focusTarget != null)
                {
                    try
                    {
                        focusTarget.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                focusTarget.Focus();
                                System.Windows.Input.Keyboard.Focus(focusTarget);
                            }
                            catch { }
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    }
                    catch { }
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

                AlignToOwner(this.Owner);
                if (!this.IsVisible)
                    this.Show();
            }
            catch { }
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

        // SetWindowPos removed to avoid interfering with input routing.
    }
}
